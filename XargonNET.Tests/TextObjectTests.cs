using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;
using Xunit;

namespace XargonNET.Tests;

public class TextObjectTests
{
    private static (TextRenderer text, VgaScreen screen, ShapeManager shapes) Fixture()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        return (new TextRenderer(shapes), screen, shapes);
    }

    private static HashSet<byte> Colours(VgaScreen s, int x, int y, int w, int h)
    {
        var set = new HashSet<byte>();
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++) set.Add(s.Pixels[j * VgaScreen.Width + i]);
        return set;
    }

    private static HashSet<byte> Render(int hi, int back, string s, int fill = 100)
    {
        var (text, screen, _) = Fixture();
        screen.Clear((byte)fill);
        text.Draw(screen, screen.Main, 10, 10, TextRenderer.Font8, hi, back, s);
        var c = Colours(screen, 10, 10, 8 * s.Length, 8);
        c.Remove((byte)fill);
        return c;
    }

    [Fact]
    public void HiZeroMapsTo11()
    {
        var c = Render(0, -1, "H");
        Assert.Contains((byte)11, c);
        Assert.Single(c);
    }

    [Fact]
    public void BackZeroMapsTo248()
    {
        // The space glyph is made of pixel-3 (background) cells.
        var c = Render(15, 0, "H ");
        Assert.Contains((byte)248, c);
    }

    [Fact]
    public void HiEightIsTwoToneCursor()
    {
        var c = Render(8, -1, "HIEA");
        Assert.Contains((byte)6, c);
        Assert.Contains((byte)14, c);
        Assert.DoesNotContain((byte)8, c);
    }

    [Fact]
    public void PlainHiUsesOneColourAndTransparentBack()
    {
        var c = Render(15, -1, "H ");
        Assert.Equal(new HashSet<byte> { 15 }, c);
    }

    [Fact]
    public void ExplicitBackIsUsed()
    {
        var c = Render(15, 33, "H ");
        Assert.Contains((byte)33, c);
    }

    [Fact]
    public void SetObjSizeUsesStringLength()
    {
        var t6 = new Obj { Kind = ObjectKind.Text6, Inside = "HELLO" };
        var t8 = new Obj { Kind = ObjectKind.Text8, Inside = "HELLO" };
        var none = new Obj { Kind = ObjectKind.Text8, Inside = null };
        ObjectManager.SetObjSize(t6); ObjectManager.SetObjSize(t8); ObjectManager.SetObjSize(none);
        Assert.Equal(30, t6.Xl);
        Assert.Equal(40, t8.Xl);
        Assert.Equal(0, none.Xl);
    }

    [Fact]
    public void CounterKillsOnThirdUpdate()
    {
        var w = TestWorld.Load(1, "STORY");
        int n = w.AddObj(ObjectKind.Text8, 0, 0, 15, -1);
        w.Obj(n).Inside = "X";
        w.Obj(n).Counter = 3;
        Assert.Equal(0, w.Dispatch(n, Msg.Update, 0));
        Assert.Equal(0, w.Dispatch(n, Msg.Update, 0));
        Assert.Equal(ObjectKind.Text8, w.Obj(n).Kind);
        Assert.Equal(1, w.Dispatch(n, Msg.Update, 0));
        Assert.Equal(ObjectKind.Killme, w.Obj(n).Kind);
    }

    [Fact]
    public void ZeroCounterNeverKills()
    {
        var w = TestWorld.Load(1, "STORY");
        int n = w.AddObj(ObjectKind.Text6, 0, 0, 15, -1);
        for (int i = 0; i < 5; i++) Assert.Equal(0, w.Dispatch(n, Msg.Update, 0));
        Assert.Equal(ObjectKind.Text6, w.Obj(n).Kind);
    }

    [Fact]
    public void StoryBoardTextDraws()
    {
        var w = TestWorld.Load(1, "STORY");
        int n = -1;
        for (int i = 0; i < w.Objects.Count; i++)
            if (w.Objects[i].Kind == ObjectKind.Text8 && w.Objects[i].Inside == "PRESENTS") n = i;
        Assert.True(n > 0);
        var o = w.Obj(n);
        Assert.Equal(64, o.Xl);

        var vp = new Viewport(0, 2, VgaScreen.Width, 158) { OriginX = o.X - 40, OriginY = 0 };
        w.Text = new TextRenderer(new ShapeManager(AssetSet.Mount(AssetSet.FindAssetsDir(), 1).Graphics));
        w.Screen.Clear(1);
        w.DrawBack(w.Screen, vp);
        w.DrawFore(w.Screen, vp);
        int sx = vp.X + 40, sy = vp.Y + o.Y;
        var c = Colours(w.Screen, sx, sy, o.Xl, o.Yl);
        c.Remove(1);
        Assert.NotEmpty(c);
    }

    [Fact]
    public void NullRendererDrawsNothing()
    {
        var w = TestWorld.Load(1, "STORY");
        w.Text = null;
        w.Screen.Clear(1);
        w.DrawBack(w.Screen, TestWorld.Vp);
        w.DrawFore(w.Screen, TestWorld.Vp);
    }

    [Fact]
    public void WideStringDoesNotThrow()
    {
        var w = TestWorld.Load(1, "STORY");
        int n = w.AddObj(ObjectKind.Text8, 300, 20, 15, 0);
        w.Obj(n).Inside = new string('W', 200);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158) { OriginX = 280, OriginY = 0 };
        w.DrawBack(w.Screen, vp);
        w.DrawFore(w.Screen, vp);
    }
}
