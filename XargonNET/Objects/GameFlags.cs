namespace XargonNET.Objects;

/// <summary>
/// Info/object flag bits from XARGON.H. Used by both the info[] table (block properties) and
/// object kinds. A block is solid to the player unless <see cref="PlayerThru"/> is set.
/// </summary>
public static class GameFlags
{
    public const int PlayerThru = 1;      // f_playerthru — player passes through (not solid)
    public const int NotStair = 2;        // f_notstair
    public const int NotVine = 4;         // f_notvine
    public const int MsgTouch = 8;        // f_msgtouch
    public const int MsgDraw = 16;        // f_msgdraw — block drawn by handler (animated)
    public const int MsgUpdate = 32;      // f_msgupdate
    public const int Inside = 64;         // f_inside — carries text, no sprite
    public const int Front = 128;         // f_front — draw in front of player
    public const int Trigger = 256;       // f_trigger
    public const int Back = 512;          // f_back — background object
    public const int Always = 1024;       // f_always
    public const int Killable = 2048;     // f_killable
    public const int Fireball = 4096;     // f_fireball
    public const int Water = 8192;        // f_water
    public const int NotWater = 16384;    // f_notwater
    public const int Weapon = 16384;      // f_weapon (alias of NotWater)
}
