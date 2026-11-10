namespace XargonNET.Objects;

/// <summary>
/// Named board-cell (info index) constants from XARGON.H, used by animated-block logic. These are
/// values stored in the board grid (not shape ids); animated ranges are cycled in place each frame.
/// </summary>
public static class Tiles
{
    public const int Arrow1 = 149, Arrow2 = 150, Motion1 = 151;
    public const int Crum1 = 160, CrumLast = 171, Blink = 172;
    public const int Comp1 = 260, Comp3 = 262, EyeOp = 263, EyeCl = 264, NoEye = 265, BreakWall = 148;
    public const int Lava1 = 290, Lava8 = 297, Lavab = 298;
    public const int WaterFl1 = 364, WaterFl4 = 367, WaterFlt = 371;
    public const int Thrnt = 549, Thrnb = 550, Thornt = 563, Thornb = 564;
    public const int Acidb = 536;
    public const int WaterBubl = 375, WaterT1 = 376, WaterT8 = 383, SeaweedR = 384, SeaweedL = 385;
    public const int Acid1 = 528, Acid8 = 535;
    public const int WaterEnd1 = 574, WaterEnd8 = 581, WaterMst1 = 582, WaterMst8 = 589,
                     WaterWav1 = 590, WaterWav8 = 597;
    public const int Spike1 = 598, Spike6 = 603;
    public const int Flow1 = 616, Flow3 = 618;
    public const int Lever1 = 649, Lever7 = 655;
    public const int Elec1 = 676, Elec9 = 684;
    public const int MpGate = 720;
    public const int Qzap1 = 852, Qzap3 = 854;

    // Moving-platform / step-builder board cells (msg_elev, msg_bridger, msg_addstep).
    public const int S1l = 9, S1r = 10, S2 = 21, S2u = 25, S3 = 64, C2 = 65, C8 = 117;
    public const int Stlbeam = 138, Elevl = 139, Elevr = 140, Platl = 146, Platr = 147;
    public const int Bridge = 220, S6l = 734, S6r = 735;
}
