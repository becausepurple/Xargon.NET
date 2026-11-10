using XargonNET.Assets;
using XargonNET.Video;

namespace XargonNET.Objects;

/// <summary>
/// The "effects" object (kind 63, obj_effects / msg_effects in X_OBJ.C). One is placed in each
/// level; its <c>state</c> selects the sky/background colour preset (0..10) and its <c>substate</c>
/// selects a colour-cycling category. This is why the play-area "air" (a solid fill of palette
/// index 250) shows as sky-blue rather than black: the object sets colours 250/251 (and, for the
/// gradient presets, 176..183) at level start.
///
/// Here we apply the static part of the preset at load. The animated parts (lightning, lava and
/// waterfall cycling) belong to the per-frame object update and are handled by the object system.
/// </summary>
public static class Effects
{
    public const int EffectsKind = ObjectKind.Effects;

    /// <summary>Find the level's effects object and apply its sky-colour preset to the palette.</summary>
    public static void ApplyForBoard(VgaScreen screen, Board board)
    {
        foreach (var o in board.Objects)
            if (o.Kind == EffectsKind)
            {
                ApplySky(screen, o.State, o.Xd, o.Yd);
                return;
            }
    }

    /// <summary>
    /// Apply sky-colour preset <paramref name="state"/> (0..10), porting the switch in msg_effects.
    /// The air colours 250/251 are gated by the object's yd/xd (matching the original), and the
    /// gradient presets also rewrite the sky-band indices 176..183.
    /// </summary>
    public static void ApplySky(VgaScreen screen, int state, int xd, int yd)
    {
        void Air(int r, int g, int b)
        {
            if (yd == 0) screen.SetColor6(250, r, g, b);
            if (xd == 0) screen.SetColor6(251, r, g, b);
        }
        void Gradient(byte[,] g)
        {
            for (int i = 0; i < 8; i++)
                screen.SetColor6(176 + i, g[i, 0], g[i, 1], g[i, 2]);
        }

        switch (state)
        {
            case 0: // black sky
                screen.SetColor6(250, 0, 0, 0);
                screen.SetColor6(251, 0, 0, 0);
                break;
            case 1: // lightning — static base is black; flashes handled per-frame later
                screen.SetColor6(250, 0, 0, 0);
                screen.SetColor6(251, 0, 0, 0);
                break;
            case 2: // dk. blue sky
                screen.SetColor6(250, 0, 0, 32);
                if (xd == 0) screen.SetColor6(251, 0, 0, 32);
                break;
            case 3: // lt. blue sky
                Gradient(new byte[,] { {8,16,25},{8,20,29},{12,24,33},{16,28,41},{20,32,45},{24,40,49},{28,44,57},{36,48,60} });
                Air(36, 48, 60);
                break;
            case 4: // yellow sky
                Gradient(new byte[,] { {32,0,0},{40,0,0},{52,0,0},{60,0,0},{60,28,0},{60,40,0},{60,52,0},{60,60,0} });
                Air(60, 60, 0);
                break;
            case 5: // emerald sky
                Gradient(new byte[,] { {0,12,12},{0,18,17},{0,25,23},{0,32,27},{0,39,32},{0,46,35},{0,53,38},{0,60,40} });
                Air(0, 60, 40);
                break;
            case 6: // olive green
                screen.SetColor6(250, 32, 32, 24);
                if (xd == 0) screen.SetColor6(251, 32, 32, 24);
                break;
            case 7: // violet sky
                Gradient(new byte[,] { {13,5,22},{18,8,27},{23,13,33},{29,19,39},{35,25,45},{42,32,51},{49,40,57},{57,50,63} });
                Air(57, 50, 63);
                break;
            case 8: // factory grey
                screen.SetColor6(250, 23, 23, 23);
                if (xd == 0) screen.SetColor6(251, 23, 23, 23);
                break;
            case 9: // royal blue
                screen.SetColor6(250, 12, 23, 63);
                if (xd == 0) screen.SetColor6(251, 12, 23, 63);
                break;
            case 10: // factory grey v3
                screen.SetColor6(250, 20, 20, 23);
                if (xd == 0) screen.SetColor6(251, 20, 20, 23);
                break;
        }
    }

    /// <summary>
    /// upd_colors() / the counter switch of msg_effects: cycle animated palette ranges from the
    /// immutable source DAC. counter 0 = mines+pod, 1 = waterfalls+mines+pod, 2 = lava+mines+pod,
    /// 3 = upd_colors (lava, waterfalls, mines and the 236..239 pod range).
    /// </summary>
    public static void UpdColors(VgaScreen screen, int counter, int gamecount)
    {
        void Cyc(int c0, int c1, int mask, int div)
        {
            for (int c = c0; c < c1; c++)
                screen.SetColorFromDac(c, c0 + (((c + gamecount) & mask) / div));
        }
        switch (counter)
        {
            case 0: Cyc(234, 236, 3, 2); Cyc(236, 240, 7, 2); break;
            case 1: Cyc(240, 248, 7, 1); Cyc(234, 236, 3, 2); Cyc(236, 240, 7, 2); break;
            case 2: Cyc(192, 200, 7, 1); Cyc(234, 236, 3, 2); Cyc(236, 240, 7, 2); break;
            case 3: Cyc(192, 200, 7, 1); Cyc(240, 248, 7, 1); Cyc(234, 236, 3, 2); Cyc(236, 240, 7, 2); break;
        }
    }

    /// <summary>
    /// init_colors(): sky 250/251 to black, hero colours 207..233 and laser colours 162..167 back to
    /// the source DAC (run after returning to the map, the C's o_col=1).
    /// </summary>
    public static void InitColors(VgaScreen screen)
    {
        screen.SetColor6(250, 0, 0, 0);
        screen.SetColor6(251, 0, 0, 0);
        for (int c = 207; c < 234; c++) screen.SetColorFromDac(c, c);
        for (int c = 162; c < 168; c++) screen.SetColorFromDac(c, c);
    }

    /// <summary>dialogmsg's "reset hero colors": only entries 207..233 back to the source DAC (the level's
    /// laser colours 162..167 and sky 250/251 are left alone, unlike <see cref="InitColors"/>).</summary>
    public static void ResetHeroColors(VgaScreen screen)
    {
        for (int c = 207; c < 234; c++) screen.SetColorFromDac(c, c);
    }

    /// <summary>Hero colours 207..233: all red to max (low health / hurt flash).</summary>
    public static void HeroRed(VgaScreen screen)
    {
        for (int c = 207; c < 234; c++) screen.SetColor6(c, 63, screen.Dac[c * 3 + 1], screen.Dac[c * 3 + 2]);
    }

    /// <summary>Hero colours 207..233: all blue to max (invincibility / healed flash).</summary>
    public static void HeroBlue(VgaScreen screen)
    {
        for (int c = 207; c < 234; c++) screen.SetColor6(c, screen.Dac[c * 3], screen.Dac[c * 3 + 1], 63);
    }

    /// <summary>Restore hero colours 207..233 and the border colour 0 (drawstats, ouched == +-1).</summary>
    public static void HeroNormal(VgaScreen screen)
    {
        screen.SetColor6(0, 0, 0, 0);
        for (int c = 207; c < 234; c++) screen.SetColorFromDac(c, c);
    }

    /// <summary>
    /// The palette side of pl.ouched in refresh()/drawstats(): +-1 restores the hero colours; &gt;=2 flashes
    /// them red (and colour 0 red); &lt;=-2 flashes them blue. The counter itself is stepped by the caller.
    /// </summary>
    public static void OuchedColors(VgaScreen screen, int ouched)
    {
        if (ouched is 1 or -1) HeroNormal(screen);
        else if (ouched >= 2) { HeroRed(screen); screen.SetColor6(0, 63, 0, 0); }
        else if (ouched <= -2) HeroBlue(screen);
    }

    /// <summary>
    /// msg_effects msg_update: the 5-tick alternate-colour pulse (colour 249, invincibility blue,
    /// health==1 red, jump-boots 233), the colour-cycling category, then the sky preset / lightning.
    /// </summary>
    public static void Update(ObjectManager w, Obj o)
    {
        var screen = w.Screen;
        var pl = w.Pl;
        if (o.StateCount++ == 4)
        {
            o.StateCount = 0;
            if (o.SubState == 0)
            {
                o.SubState = 1;
                screen.SetColor6(249, 32, 0, 0);
                if (w.InvCount(Inv.Invin) > 0)
                {
                    HeroBlue(screen);
                    if (o.Info1++ >= 30)
                    {
                        o.Info1 = 0; w.TakeInv(Inv.Invin);
                        pl.Ouched = -1; // statmodflg|=mod_screen
                    }
                }
                else if (pl.Health == 1) HeroRed(screen);
                else if (w.InvCount(Inv.Jump) > 0) screen.SetColor6(233, 0, 15, 52);
            }
            else
            {
                o.SubState = 0;
                screen.SetColor6(249, 60, 0, 0);
                if (o.Info1 > 0 || pl.Health == 1) pl.Ouched = -1;
                else if (w.InvCount(Inv.Jump) > 0) screen.SetColor6(233, 8, 8, 8); // boots back to normal
            }
        }

        UpdColors(screen, o.Counter, w.Gamecount);
        if (o.State == -1) return;

        if (o.State == 1)
        {
            // Simulate lightning: a random 3-frame flash, then back to black.
            if (w.XrRandom(25) == 0)
            {
                o.ZapHold = 3;
                screen.SetColor6(250, 60, 60, 63);
                if (o.Xd == 0) screen.SetColor6(251, 60, 60, 63);
                // (snd_play thunder dropped: no sound in this port)
            }
            if (o.ZapHold == 0)
            {
                o.ZapHold = -1;
                screen.SetColor6(250, 0, 0, 0); screen.SetColor6(251, 0, 0, 0);
            }
            return;
        }
        ApplySky(screen, o.State, o.Xd, o.Yd);
        o.State = -1; // state 0 and every state > 1: the preset is applied once, then latched
    }
}
