using Xunit;
using XargonNET.Assets;
using XargonNET.Objects;
using XargonNET.Text;
using XargonNET.Ui;
using XargonNET.Video;

namespace XargonNET.Tests;

/// <summary>Task 5: bottom status line (txt/upd_botmsg), HUD rendering and fidget messages (xargon.c:174-185, 235-238; x_player.c:80-87).</summary>
public class StatusLineTests
{
    private const string Default = "DEFAULT";

    [Fact]
    public void InitialStateMatchesZeroedCGlobals()
    {
        var s = new StatusLine();
        Assert.Equal("", s.Message);
        Assert.Equal(0, s.Time);
        Assert.Equal(0, s.Flag);
        s.Update(Default); // bottime==0 && text_flg==0 -> txt(v_msg,4,1)
        Assert.Equal(Default, s.Message);
        Assert.Equal(4, s.Color);
        Assert.Equal(1, s.Flag);
    }

    [Fact]
    public void TransientTxtSetsTimeTo100()
    {
        var s = new StatusLine();
        s.Txt("X", 3, 0);
        Assert.Equal(100, s.Time);
        Assert.Equal("X", s.Message);
        Assert.Equal(3, s.Color);
    }

    [Fact]
    public void TransientMessageRevertsToDefaultAfter100Updates()
    {
        var s = new StatusLine();
        s.Txt("X", 3, 0);
        for (int i = 0; i < 99; i++) s.Update(Default);
        Assert.Equal("X", s.Message);
        s.Update(Default);
        Assert.Equal(Default, s.Message);
        Assert.Equal(4, s.Color);
        Assert.Equal(1, s.Flag);
    }

    [Fact]
    public void LongMessageIsTruncatedTo49Characters()
    {
        var s = new StatusLine();
        s.Txt(new string('A', 60), 7, 0);
        Assert.Equal(49, s.Message.Length);
    }

    [Fact]
    public void PersistentMessageNeverExpires()
    {
        var s = new StatusLine();
        s.Txt("STAY", 2, 1);
        for (int i = 0; i < 1000; i++) s.Update(Default);
        Assert.Equal("STAY", s.Message);
        Assert.Equal(2, s.Color);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void WorldStartsWithEpisodeDefaultMessage(int episode)
    {
        var w = TestWorld.Load(episode, "BOARD_01");
        Assert.Equal(GameStrings.VMsg(episode), w.Status.Message);
        Assert.Equal(4, w.Status.Color);
        Assert.Equal(1, w.Status.Flag);
        Assert.Equal(GameStrings.VMsg(episode), w.DefaultStatusMessage);
    }

    [Fact]
    public void UpdBotMsgExpiresTransientToEpisodeDefault()
    {
        var w = TestWorld.Load(2, "BOARD_01");
        w.Txt("HI", 6, 0);
        for (int i = 0; i < 100; i++) w.UpdBotMsg();
        Assert.Equal(GameStrings.VMsg(2), w.Status.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void StandingHeroFidgetsAtStateCount258(int episode)
    {
        var w = TestWorld.Load(episode, "BOARD_01");
        var h = w.Obj(0);
        h.State = 0; h.Xd = 0; h.Yd = 0; h.Info1 = 1; h.StateCount = 258;
        var p = new Player(w, TestWorld.Vp);
        p.Update(new Controls());
        var set = Enumerable.Range(0, 4).Select(i => GameStrings.FidgetMsg(episode, i)).ToArray();
        Assert.Contains(w.Status.Message, set);
        Assert.Equal(6, w.Status.Color);
        Assert.Equal(0, w.Status.Flag);
        Assert.Equal(100, w.Status.Time);
    }

    private static (VgaScreen screen, ShapeManager shapes, TextRenderer text, PlayerState pl) HudRig()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        return (screen, shapes, new TextRenderer(shapes), new PlayerState { Health = 5 });
    }

    private static int Count(VgaScreen s, int x0, int x1, int y0, int y1, byte c)
    {
        int n = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (s.Pixels[y * VgaScreen.Width + x] == c) n++;
        return n;
    }

    [Fact]
    public void HudDrawsColour6MessageInStripAndFills121()
    {
        var (screen, shapes, text, pl) = HudRig();
        var st = new StatusLine();
        st.Txt("HELLO", 6, 1);
        Hud.Draw(screen, shapes, text, pl, st, 0);
        Assert.True(Count(screen, 12, 308, 190, 198, 6) > 0, "no colour-6 text pixels in strip");
        Assert.True(Count(screen, 12, 308, 190, 198, 121) > 0, "strip not filled with 121");
        // Text is centred about x=160: x = 12 + 148 - 3*5 = 145 .. 175; nothing at the far left of the strip.
        Assert.Equal(0, Count(screen, 12, 140, 190, 198, 6));
    }

    [Theory]
    [InlineData(0, 7, 4)]
    [InlineData(1, 4, 7)]
    [InlineData(2, 7, 4)]
    [InlineData(3, 4, 7)]
    public void Colour3FlashAlternatesWithGamecountParity(int gamecount, int expected, int other)
    {
        var (screen, shapes, text, pl) = HudRig();
        var st = new StatusLine();
        st.Txt("KEY", 3, 1);
        Hud.Draw(screen, shapes, text, pl, st, gamecount);
        Assert.True(Count(screen, 12, 308, 190, 198, (byte)expected) > 0);
        Assert.Equal(0, Count(screen, 12, 308, 190, 198, (byte)other));
    }

    [Fact]
    public void HudDoesNotBreakBarFrameAtY188()
    {
        var (screen, shapes, text, pl) = HudRig();
        var st = new StatusLine();
        st.Txt("HELLO", 6, 1);
        Hud.Draw(screen, shapes, text, pl, new StatusLine(), 0);
        var empty = (byte[])screen.Pixels.Clone();
        Hud.Draw(screen, shapes, text, pl, st, 0);
        for (int y = 188; y < 190; y++)
            for (int x = 0; x < 320; x++)
                Assert.Equal(empty[y * 320 + x], screen.Pixels[y * 320 + x]);
    }
}
