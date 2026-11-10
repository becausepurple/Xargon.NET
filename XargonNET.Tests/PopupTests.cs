using System.Collections.Generic;
using System.Linq;
using XargonNET.Assets;
using XargonNET.Engine;
using XargonNET.Objects;
using XargonNET.Ui;
using XargonNET.Video;
using Xunit;

namespace XargonNET.Tests;

/// <summary>
/// Task 6: the popup host seam (dotextmsg), the sign (kind 71) and map banner (kind 19) handlers,
/// the first-time hint flags, and the pure layout/scroll/dismiss logic of the modal loop.
/// The real SDL modal loop is not exercised here (no window); only its pure decision helpers and the
/// object-side wiring (through a recording <see cref="IPopupHost"/>) are tested.
/// </summary>
public class PopupTests
{
    private sealed class RecordingHost : IPopupHost
    {
        public readonly List<(int N, PopupStyle Style)> Text = new();
        public readonly List<(int N, bool Eagle)> Dialog = new();
        public void TextMessage(int n, PopupStyle style) => Text.Add((n, style));
        public void DialogMessage(int n, bool eagle) => Dialog.Add((n, eagle));
    }

    private static (ObjectManager w, RecordingHost rec) World(int ep, string board)
    {
        var w = TestWorld.Load(ep, board);
        var rec = new RecordingHost();
        w.Popups = rec;
        return (w, rec);
    }

    // ---- pure layout/scroll logic ----

    [Theory]
    [InlineData(1, 48)]  // (96 - 6*0)/2 = 48 (title only, no body)
    [InlineData(5, 36)]  // (96 - 24)/2 = 36
    [InlineData(16, 3)]  // (96 - 90)/2 = 3  (16 = textys -> still short, Review Focus 2)
    public void ShortFormLayoutCentresBody(int numLines, int startY)
    {
        var layout = PopupWindows.Layout(numLines);
        Assert.False(layout.Scroll);
        Assert.Equal(startY, layout.StartY);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(184)]
    public void OverSixteenLinesScrolls(int numLines)
    {
        var layout = PopupWindows.Layout(numLines);
        Assert.True(layout.Scroll);
        Assert.Equal(0, layout.StartY);
        Assert.Equal(16, layout.VisibleRows);
    }

    [Fact]
    public void ScrollTargetClampsToRange()
    {
        // 16 lines -> max offset 0: no scroll possible.
        Assert.Equal(0, PopupWindows.ScrollTarget(0, 16, ScrollKey.Down));
        // 17 lines -> one step, max offset 1 (last page shows one trailing blank row).
        Assert.Equal(1, PopupWindows.ScrollTarget(0, 17, ScrollKey.Down));
        Assert.Equal(1, PopupWindows.ScrollTarget(1, 17, ScrollKey.Down)); // clamp at max
        Assert.Equal(0, PopupWindows.ScrollTarget(1, 17, ScrollKey.Up));
        Assert.Equal(0, PopupWindows.ScrollTarget(0, 17, ScrollKey.Up));   // clamp at 0
        // a longer message steps normally.
        Assert.Equal(5, PopupWindows.ScrollTarget(4, 100, ScrollKey.Down));
    }

    [Fact]
    public void DismissTablesMatchC()
    {
        // Short form: Space / Enter / Fire1(Shift) dismiss; Esc and Fire2 do NOT.
        Assert.True(PopupWindows.DismissShort(PopupInput.Space));
        Assert.True(PopupWindows.DismissShort(PopupInput.Enter));
        Assert.True(PopupWindows.DismissShort(PopupInput.Fire1));
        Assert.False(PopupWindows.DismissShort(PopupInput.Escape));
        Assert.False(PopupWindows.DismissShort(PopupInput.Fire2));
        Assert.False(PopupWindows.DismissShort(PopupInput.Up));
        // Scroll form: Space / Enter / Esc / Fire1 dismiss; Fire2 does not.
        Assert.True(PopupWindows.DismissScroll(PopupInput.Space));
        Assert.True(PopupWindows.DismissScroll(PopupInput.Enter));
        Assert.True(PopupWindows.DismissScroll(PopupInput.Escape));
        Assert.True(PopupWindows.DismissScroll(PopupInput.Fire1));
        Assert.False(PopupWindows.DismissScroll(PopupInput.Fire2));
    }

    [Fact]
    public void ReleaseWaitBlocksOnNavAndFireKeys()
    {
        foreach (var k in new[] { PopupInput.Up, PopupInput.Down, PopupInput.Space, PopupInput.Enter,
                                   PopupInput.Fire1, PopupInput.Fire2, PopupInput.Escape })
            Assert.True(PopupWindows.IsReleaseBlocking(k));
        Assert.False(PopupWindows.IsReleaseBlocking(PopupInput.None));
        Assert.False(PopupWindows.IsReleaseBlocking(PopupInput.Other));
    }

    [Fact]
    public void ToScrollMapsUpDown()
    {
        Assert.Equal(ScrollKey.Up, PopupWindows.ToScroll(PopupInput.Up));
        Assert.Equal(ScrollKey.Down, PopupWindows.ToScroll(PopupInput.Down));
        Assert.Equal(ScrollKey.None, PopupWindows.ToScroll(PopupInput.Space));
    }

    // ---- sign (kind 71) handler ----

    /// <summary>The real ep1 BOARD_01 kind-71 sign at (944,176): state 0, counter 11.</summary>
    private static int FindRealSign(ObjectManager w)
    {
        int n = -1;
        for (int i = 0; i < w.Objects.Count; i++)
            if (w.Obj(i).Kind == ObjectKind.TxtMsg) { Assert.Equal(-1, n); n = i; }
        Assert.True(n > 0, "ep1 BOARD_01 should hold exactly one kind-71 sign");
        var o = w.Obj(n);
        Assert.Equal((944, 176, 0, 11), (o.X, o.Y, o.State, o.Counter));
        return n;
    }

    [Fact]
    public void SignStateZeroWithUpShowsTextMessage()
    {
        var (w, rec) = World(1, "BOARD_01");
        int n = FindRealSign(w);
        w.InputDy = -1; // pressing Up
        w.Dispatch(n, Msg.Touch, 0);
        Assert.Equal(new[] { (11, PopupStyle.Plain) }, rec.Text.ToArray());
        Assert.NotEqual(ObjectKind.Killme, (int)w.Obj(n).Kind); // a sign is not killed
    }

    [Fact]
    public void SignStateZeroWithoutUpOnlyPostsHint()
    {
        var (w, rec) = World(1, "BOARD_01");
        int n = FindRealSign(w);
        w.InputDy = 0; // not pressing Up
        w.Dispatch(n, Msg.Touch, 0);
        Assert.Empty(rec.Text);
        Assert.Equal("Press UP arrow key to read the sign", w.Status.Message);
        Assert.Equal(7, w.Status.Color);
    }

    [Fact]
    public void AutoSignStateOneTriggersOnceAndIsKilled()
    {
        var (w, rec) = World(1, "MAP");
        int n = -1;
        for (int i = 0; i < w.Objects.Count; i++)
            if (w.Obj(i).Kind == ObjectKind.TxtMsg && w.Obj(i).Counter == 21) { n = i; break; }
        Assert.True(n > 0, "ep1 MAP should hold the state-1 sign (counter 21) at (304,208)");
        Assert.Equal(1, w.Obj(n).State);
        w.InputDy = 0; // a state-1 sign fires regardless of Up
        w.Dispatch(n, Msg.Touch, 0);
        Assert.Equal(new[] { (21, PopupStyle.Plain) }, rec.Text.ToArray());
        Assert.Equal(ObjectKind.Killme, (int)w.Obj(n).Kind);
    }

    // ---- first-time hint flags ----

    [Fact]
    public void FirstBoxShowsMessageThreeOnlyOnce()
    {
        var (w, rec) = World(1, "BOARD_01");
        Assert.True(w.Hints.Box);
        int b1 = w.AddObj(ObjectKind.Box, 100, 100, 0, 0);
        w.Obj(b1).SubState = 1;
        w.Dispatch(b1, Msg.Touch, 0); // player touches a landed box
        int b2 = w.AddObj(ObjectKind.Box, 200, 200, 0, 0);
        w.Obj(b2).SubState = 1;
        w.Dispatch(b2, Msg.Touch, 0);
        Assert.Equal(new[] { (3, PopupStyle.Plain) }, rec.Text.ToArray());
        Assert.False(w.Hints.Box);
    }

    [Fact]
    public void FirstFruitShowsMessageThirtyEightOnlyOnce()
    {
        var (w, rec) = World(1, "BOARD_01");
        Assert.True(w.Hints.Fruit);
        int f1 = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        w.Obj(f1).State = 2; // grapes
        w.Dispatch(f1, Msg.Touch, 0);
        int f2 = w.AddObj(ObjectKind.Bonus, 200, 200, 0, 0);
        w.Obj(f2).State = 6; // cherries
        w.Dispatch(f2, Msg.Touch, 0);
        Assert.Equal(new[] { (38, PopupStyle.Plain) }, rec.Text.ToArray());
        Assert.False(w.Hints.Fruit);
    }

    [Fact]
    public void JournalBonusUsesDialogStyleAndIsKilled()
    {
        var (w, rec) = World(1, "BOARD_01");
        int j = w.AddObj(ObjectKind.Bonus, 100, 100, 0, 0);
        w.Obj(j).State = 13; // journal entry
        w.Obj(j).Counter = 31;
        w.Dispatch(j, Msg.Touch, 0);
        Assert.Equal(new[] { (31, PopupStyle.Dialog) }, rec.Text.ToArray());
        Assert.Equal(ObjectKind.Killme, (int)w.Obj(j).Kind);
    }

    [Fact]
    public void IconsShowPowerMessageBeforeAddingToInventory()
    {
        var (w, rec) = World(1, "BOARD_01");
        int ic = w.AddObj(ObjectKind.Icons, 100, 100, 0, 0);
        w.Obj(ic).State = 1; // power object #2
        w.Dispatch(ic, Msg.Touch, 0);
        Assert.Equal(new[] { (9, PopupStyle.Plain) }, rec.Text.ToArray());
        Assert.Equal(1, w.InvCount(Inv.Icon2));
    }

    // ---- Review Focus 1: an absent message number never throws ----

    [Fact]
    public void AbsentMessageStillInvokesHostAndRealPopupGuardsNull()
    {
        var (w, rec) = World(3, "BOARD_01");
        w.TextMsg(37); // ep3 message 37 is absent in the data
        Assert.Equal(new[] { (37, PopupStyle.Plain) }, rec.Text.ToArray());

        // The real PopupWindows must guard a null message and return without drawing.
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 3);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var text = new TextRenderer(shapes);
        var pw = new PopupWindows(screen, () => shapes, () => text, () => set.Text,
            presenter: null, clock: null, controls: null, keys: null, onQuit: null);
        var ex = Record.Exception(() => pw.TextMessage(37, PopupStyle.Plain));
        Assert.Null(ex);
    }

    // ---- map banner (kind 19) placement ----

    [Fact]
    public void MapDemoPinsToViewportOriginPlusOffset()
    {
        var w = TestWorld.Load(1, "MAP");
        var vp = new Viewport(0, 2, VgaScreen.Width, 158) { OriginX = 100, OriginY = 50 };
        w.UpdateObjects(vp, touch: false);
        var md = w.Objects.FirstOrDefault(o => o.Kind == ObjectKind.MapDemo);
        Assert.NotNull(md);
        Assert.Equal((116, 60), (md!.X, md.Y)); // origin + (16, 10)
    }

    // ---- Task 7: dialogmsg geometry/helpers ----

    [Theory]
    [InlineData(false, 0, 0x4414)]
    [InlineData(false, 15, 0x4423)]
    [InlineData(true, 0, 0x470b)]
    [InlineData(true, 15, 0x471a)]
    public void FaceShapeIds(bool eagle, int c, int expected) =>
        Assert.Equal(expected, PopupWindows.FaceShape(eagle, c));

    [Fact]
    public void DialogWindowGeometry()
    {
        var h = PopupWindows.DialogWindow(false);
        Assert.Equal((16, 16, 240, 92), (h.Border.X, h.Border.Y, h.Border.W, h.Border.H));
        Assert.Equal((96, 32, 152, 64), (h.Inside.X, h.Inside.Y, h.Inside.W, h.Inside.H));
        Assert.Equal((24, 32, 64, 64), (h.TopLeft.X, h.TopLeft.Y, h.TopLeft.W, h.TopLeft.H));
        var e = PopupWindows.DialogWindow(true);
        Assert.Equal((64, 52), (e.Border.X, e.Border.Y));
        Assert.Equal((144, 68), (e.Inside.X, e.Inside.Y));
        Assert.Equal((72, 68), (e.TopLeft.X, e.TopLeft.Y));
    }

    [Theory]
    [InlineData(5, 20)]  // (64 - 24)/2
    [InlineData(1, 32)]
    public void DialogStartYCentresBody(int numLines, int y) => Assert.Equal(y, PopupWindows.DialogStartY(numLines));

    [Fact]
    public void ResetHeroColorsOnlyTouchesHeroEntries()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 1);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        uint orig207 = screen.Palette[207], orig233 = screen.Palette[233];
        screen.SetColor6(207, 63, 63, 63);
        screen.SetColor6(233, 0, 0, 63);
        screen.SetColor6(162, 1, 2, 3);
        screen.SetColor6(250, 9, 9, 9);
        uint mod162 = screen.Palette[162], mod250 = screen.Palette[250];
        Effects.ResetHeroColors(screen);
        Assert.Equal(orig207, screen.Palette[207]);
        Assert.Equal(orig233, screen.Palette[233]);
        Assert.Equal(mod162, screen.Palette[162]);
        Assert.Equal(mod250, screen.Palette[250]);
    }

    [Fact]
    public void AbsentDialogMessageDoesNothing()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 3);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var text = new TextRenderer(shapes);
        var pw = new PopupWindows(screen, () => shapes, () => text, () => set.Text, null, null, null, null, null);
        var before = (byte[])screen.Pixels.Clone();
        Assert.Null(Record.Exception(() => pw.DialogMessage(37, false)));
        Assert.Equal(before, screen.Pixels);
    }

    [Fact]
    public void LatchedQuitSkipsPopupsAndBannersWithoutDrawing()
    {
        var set = AssetSet.Mount(AssetSet.FindAssetsDir(), 3);
        var screen = new VgaScreen();
        set.LoadPalette(screen);
        var shapes = new ShapeManager(set.Graphics);
        var text = new TextRenderer(shapes);
        bool appQuit = true; // GameApp._quit already latched by an earlier window close
        var pw = new PopupWindows(screen, () => shapes, () => text, () => set.Text,
            null, null, null, null, null, () => appQuit);
        int n = Enumerable.Range(0, 200).First(i => set.Text.Get(i) is not null);
        var before = (byte[])screen.Pixels.Clone();
        // Unlatched, each of these would enter a modal loop with no auto-dismiss; latched they must return at once.
        Assert.True(pw.QuitLatched);
        pw.TextMessage(n, PopupStyle.Plain);
        pw.DialogMessage(n, false);
        long t = pw.ShowLevelBanner(3, 1);
        pw.WaitLevelBanner(t);
        Assert.Equal(before, screen.Pixels);
    }

    // ---- Task 7: eagle (kind 23) ----

    private static (ObjectManager w, RecordingHost rec, int n, Viewport vp) EagleWorld()
    {
        var (w, rec) = World(1, "BOARD_08");
        int n = -1;
        for (int i = 0; i < w.Objects.Count; i++) if (w.Obj(i).Kind == ObjectKind.Eagle) n = i;
        Assert.True(n > 0);
        var vp = new Viewport(0, 2, VgaScreen.Width, 158) { OriginX = w.Obj(n).X - 100, OriginY = 0 };
        w.UpdateObjects(vp, touch: false); // sets the current viewport (eagle inert, Info1 == 0)
        return (w, rec, n, vp);
    }

    [Fact]
    public void EagleIsInertUntilInfo1IsTwo()
    {
        var (w, rec, n, _) = EagleWorld();
        foreach (int info in new[] { 0, 1 })
        {
            w.Obj(n).Info1 = info;
            Assert.Equal(0, w.Dispatch(n, Msg.Update, 0));
            Assert.Empty(rec.Dialog);
        }
        Assert.Equal(0, w.Obj(n).SubState);
    }

    [Fact]
    public void EagleSpeaksFourLinesThenFliesAndDies()
    {
        var (w, rec, n, _) = EagleWorld();
        var p = w.Obj(n);
        Assert.Equal((17, 4, 2), (p.State, p.Yd, p.Xd));
        p.Info1 = 2;
        int x0 = p.X;
        for (int i = 0; i < 3; i++) w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(x0, p.X); // still speaking
        w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(new[] { (17, false), (18, true), (19, false), (20, true) }, rec.Dialog.ToArray());
        Assert.Equal(x0 + p.Xd, p.X);
        Assert.Equal(4, p.SubState);
        int guard = 0;
        while ((int)w.Obj(n).Kind != ObjectKind.Killme && guard++ < 2000) w.Dispatch(n, Msg.Update, 0);
        Assert.Equal(ObjectKind.Killme, (int)w.Obj(n).Kind);
        Assert.Equal(4, rec.Dialog.Count); // never speaks again
    }

    [Fact]
    public void EagleUpdateReturnsStateCountParityWhileActive()
    {
        var (w, _, n, _) = EagleWorld();
        var p = w.Obj(n);
        p.Info1 = 2;
        var speaking = new HashSet<int>();   // parities of StateCount seen while SubState < Yd after the update
        var flying = new HashSet<int>();     // ... while SubState == Yd
        int guard = 0;
        while ((int)p.Kind != ObjectKind.Killme && guard++ < 2000)
        {
            int r = w.Dispatch(n, Msg.Update, 0);
            if ((int)p.Kind == ObjectKind.Killme) { Assert.Equal(1, r); break; }
            Assert.Equal((p.StateCount & 1) == 0 ? 1 : 0, r);
            (p.SubState == p.Yd ? flying : speaking).Add(p.StateCount & 1);
        }
        Assert.Equal(new[] { 0, 1 }, speaking.OrderBy(x => x).ToArray());
        Assert.Equal(new[] { 0, 1 }, flying.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void EagleTouchRules()
    {
        var (w, _, n, _) = EagleWorld();
        var p = w.Obj(n);
        p.Info1 = 0; Assert.Equal(0, w.Dispatch(n, Msg.Touch, 0)); Assert.Equal(0, p.Info1);
        p.Info1 = 2; Assert.Equal(0, w.Dispatch(n, Msg.Touch, 0)); Assert.Equal(2, p.Info1);
        p.Info1 = 1; Assert.Equal(0, w.Dispatch(n, Msg.Touch, 5)); Assert.Equal(1, p.Info1);
        Assert.Equal(1, w.Dispatch(n, Msg.Touch, 0));
        Assert.Equal(2, p.Info1);
    }

    [Fact]
    public void EagleTriggerFromPadSpawnsEffectsAndConsumesPad()
    {
        var (w, _, n, _) = EagleWorld();
        int pad = w.AddObj(ObjectKind.Pad, 10, 10, 0, 0);
        int hh = w.CountKind(ObjectKind.HitHero), fl = w.CountKind(ObjectKind.Flash);
        Assert.Equal(1, w.Dispatch(n, Msg.Trigger, pad));
        Assert.Equal(1, w.Obj(n).Info1);
        Assert.Equal(ObjectKind.Killme, (int)w.Obj(pad).Kind);
        Assert.Equal(hh + 1, w.CountKind(ObjectKind.HitHero));
        Assert.Equal(fl + 3, w.CountKind(ObjectKind.Flash));
    }

    [Fact]
    public void EagleTriggerFromNonPadLeavesSender()
    {
        var (w, _, n, _) = EagleWorld();
        int sw = w.AddObj(ObjectKind.Switch, 10, 10, 0, 0);
        w.Dispatch(n, Msg.Trigger, sw);
        Assert.Equal(ObjectKind.Switch, (int)w.Obj(sw).Kind);
    }
}
