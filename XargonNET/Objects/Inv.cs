namespace XargonNET.Objects;

/// <summary>Inventory item ids (XARGON.H inv_*).</summary>
public static class Inv
{
    public const int Hero = 0, MapKey = 1, Epic = 2, Fire = 3, Laser = 4, Bird = 5, Fish = 6,
                     Rock = 7, Jump = 8, Invin = 9, Icon1 = 10, Icon2 = 11, Icon3 = 12,
                     Key0 = 13, Key1 = 14, Key2 = 15, Key3 = 16;

    /// <summary>Inventory id of key colour <paramref name="state"/> (inv_key0 + state).</summary>
    public static int Key(int state) => Key0 + state;
}
