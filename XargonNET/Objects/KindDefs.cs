namespace XargonNET.Objects;

/// <summary>Static definition of an object kind, ported from X_OBJ.DEF (defobj macro).</summary>
public sealed class KindDef
{
    public string Name = "";
    public int ShapeTable;   // objtbl — shape table this kind draws from (0 = none/no sprite)
    public int Flags;        // objflgs
    public int Xl, Yl;       // collision/size
    public int Score;        // objscore
    /// <summary>Base shape offset within the table for the resting/first frame (from msg_draw).</summary>
    public int DrawOffset;

    public bool HasSprite => ShapeTable != 0 && (Flags & GameFlags.Inside) == 0;
    public bool IsFront => (Flags & GameFlags.Front) != 0;
    public bool IsBack => (Flags & GameFlags.Back) != 0;
}

/// <summary>
/// The object-kind table (kindtable/kindflags/... from X_OBJ.C, built from X_OBJ.DEF). Indexed by
/// object kind (0..88). <see cref="KindDef.DrawOffset"/> values were taken from each kind's
/// msg_draw base shape (e.g. emerald draws kindtable*256+28); kinds that draw dynamically or have
/// no sprite use offset 0.
/// </summary>
public static class KindDefs
{
    public const int Count = 89;
    public static readonly KindDef[] All = Build();

    public static KindDef Get(int kind) => (uint)kind < Count ? All[kind] : All[1]; // 1 = killme/none

    private static KindDef D(string name, int table, int flags, int xl, int yl, int score, int drawOffset = 0)
        => new() { Name = name, ShapeTable = table, Flags = flags, Xl = xl, Yl = yl, Score = score, DrawOffset = drawOffset };

    private static KindDef[] Build()
    {
        const int MsgTouch = GameFlags.MsgTouch, Inside = GameFlags.Inside, Trigger = GameFlags.Trigger;
        const int Front = GameFlags.Front, Back = GameFlags.Back, Always = GameFlags.Always;
        const int Killable = GameFlags.Killable, Fireball = GameFlags.Fireball, Weapon = GameFlags.NotWater;

        var a = new KindDef[Count];
        a[0] = D("PLAYER", 6, MsgTouch, 24, 40, 0);
        a[1] = D("KILLME", 0, 0, 0, 0, 0);
        a[2] = D("HEROSWIM", 40, MsgTouch, 30, 30, 0, 21);
        a[3] = D("HEROBEE", 32, MsgTouch, 24, 24, 0);
        a[4] = D("MINE", 40, 0, 20, 15, 0, 20);
        a[5] = D("TINY", 47, MsgTouch, 10, 16, 0);
        a[6] = D("TEXT6", 0, Inside, 6, 7, 0);
        a[7] = D("TEXT8", 0, Inside, 8, 8, 0);
        a[8] = D("BRIDGER", 0, Trigger, 0, 0, 0);
        a[9] = D("DOOR", 31, Trigger, 6, 8, 0, 32);
        a[10] = D("CENTEXPL", 52, 0, 8, 17, 20);
        a[11] = D("ADDSTEP", 0, Trigger, 16, 16, 0);
        a[12] = D("PAD", 51, 0, 16, 16, 0);
        a[13] = D("SPRING", 36, 0, 32, 16, 0);
        a[14] = D("ARROW", 36, 0, 12, 3, 0, 17);
        a[15] = D("MASHER", 36, 0, 24, 4, 0, 4);
        a[16] = D("PLATFORM", 36, Trigger | Always, 28, 4, 0, 13);
        a[17] = D("CHECKPT", 0, Inside, 16, 16, 0);
        a[18] = D("ELEV", 36, Trigger | Front, 32, 16, 0);
        a[19] = D("MAPDEMO", 27, Front, 64, 16, 0);
        a[20] = D("KEY", 31, 0, 12, 20, 0);
        a[21] = D("HEART", 37, 0, 16, 16, 100);
        a[22] = D("EMERALD", 30, 0, 16, 12, 100, 28);
        a[23] = D("EAGLE", 45, Trigger, 34, 44, 0);
        a[24] = D("EPIC", 34, 0, 32, 15, 0);
        a[25] = D("GRUNT", 35, Fireball, 38, 25, 400);
        a[26] = D("BOX", 37, Trigger, 16, 16, 0, 24);
        a[27] = D("SCORE", 0, Front, 8, 8, 0);
        a[28] = D("TOKEN", 30, Trigger, 16, 16, 0, 15);
        a[29] = D("SWITCH", 36, Back, 16, 16, 0, 23);
        a[30] = D("BUTTON", 51, 0, 16, 8, 0, 9);
        a[31] = D("POKER", 51, Trigger, 10, 16, 0, 13);
        a[32] = D("BULLET", 30, 0, 8, 3, 0);
        a[33] = D("BONUS", 37, 0, 16, 16, 0);
        a[34] = D("ROCK", 30, Weapon | MsgTouch, 16, 16, 0, 20);
        a[35] = D("FIREBALL", 30, Weapon | MsgTouch, 24, 22, 0, 34);
        a[36] = D("LASER", 30, Weapon | MsgTouch, 14, 4, 0, 38);
        a[37] = D("TORPEDO", 30, Weapon | MsgTouch, 10, 5, 0, 40);
        a[38] = D("FRAGEXPL", 30, Front, 6, 6, 0, 50);
        a[39] = D("FIREEXPL", 30, Front, 32, 32, 0, 45);
        a[40] = D("FLASH", 30, Front, 18, 23, 0);
        a[41] = D("HITWALL", 30, Front, 14, 14, 0, 55);
        a[42] = D("GEMS", 37, 0, 12, 14, 50, 29);
        a[43] = D("SPEAR", 36, 0, 6, 4, 0, 7);
        a[44] = D("STALAG", 0, Back | Trigger, 16, 16, 0);
        a[45] = D("BOULDER", 36, Fireball, 16, 16, 200, 19);
        a[46] = D("BALL", 51, 0, 0, 0, 0, 4);
        a[47] = D("FIRE", 48, Front, 16, 32, 0);
        a[48] = D("SEAROCK", 40, 0, 16, 16, 0, 19);
        a[49] = D("TORCH", 48, Back, 10, 24, 0, 12);
        a[50] = D("BITER", 60, 0, 10, 16, 0);
        a[51] = D("CLOUD", 36, Front, 80, 16, 0, 33);
        a[52] = D("CENTIPEDE", 52, Fireball, 76, 22, 500);
        a[53] = D("ALIEN", 58, Fireball, 40, 24, 250);
        a[54] = D("LEECH", 42, Fireball, 44, 23, 300);
        a[55] = D("TROLL", 61, Fireball, 32, 48, 400);
        a[56] = D("BLOB", 46, 0, 30, 13, 0);
        a[57] = D("HITHERO", 30, Front, 24, 35, 0, 63);
        a[58] = D("LIZARD", 56, Killable, 16, 16, 114);
        a[59] = D("SPIKES", 36, 0, 24, 16, 0);
        a[60] = D("XARGBOT", 59, 0, 16, 20, 65);
        a[61] = D("TRANSPORT", 0, Trigger, 16, 16, 0);
        a[62] = D("TRANSPAD", 0, 0, 16, 16, 0);
        a[63] = D("EFFECTS", 0, Always, 0, 0, 0);
        a[64] = D("KRUSTY", 39, 0, 36, 22, 160);
        a[65] = D("GHOUL", 54, 0, 30, 30, 2000);
        a[66] = D("BUBBLE", 40, 0, 8, 8, 0, 16);
        a[67] = D("EEL", 39, Killable, 34, 21, 60, 12);
        a[68] = D("BADFISH", 40, 0, 24, 16, 140, 6);
        a[69] = D("BAT", 56, Killable, 16, 13, 140, 8);
        a[70] = D("HOPPER", 63, Killable, 16, 14, 145);
        a[71] = D("TXTMSG", 0, 0, 16, 16, 0);
        a[72] = D("FRONT", 55, Front, 16, 16, 0);
        a[73] = D("TIMERPAD", 51, 0, 24, 10, 0);
        a[74] = D("TURRET", 59, Trigger, 22, 15, 0, 11);
        a[75] = D("CREEPER", 62, Fireball, 30, 14, 500);
        a[76] = D("(unused)", 0, 0, 0, 0, 0);
        a[77] = D("BEE", 32, Killable, 24, 24, 125);
        a[78] = D("CLIMBER", 50, 0, 16, 29, 0);
        a[79] = D("SPIDER", 43, Fireball, 40, 24, 150);
        a[80] = D("SKULL", 38, Killable, 18, 13, 10, 2);
        a[81] = D("SEAMONSTER", 39, 0, 26, 25, 0, 22);
        a[82] = D("ROBOT", 59, Fireball, 28, 28, 600, 14);
        a[83] = D("REDFISH", 40, Killable, 16, 8, 110, 22);
        a[84] = D("ICONS", 30, 0, 18, 18, 500, 31);
        a[85] = D("STORY1", 56, 0, 36, 36, 0);
        a[86] = D("STORY2", 57, 0, 36, 36, 0);
        a[87] = D("XARGON", 38, 0, 60, 68, 30000);
        a[88] = D("MAP", 47, 0, 48, 53, 0, 16);
        return a;
    }
}
