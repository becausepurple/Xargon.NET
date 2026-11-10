namespace XargonNET.Objects;

/// <summary>Object/block message codes (XARGON.H). Dispatched via <see cref="ObjMsg"/>.</summary>
public static class Msg
{
    public const int Draw = 0;     // msg_draw
    public const int Touch = 1;    // msg_touch
    public const int Update = 2;   // msg_update
    public const int Trigger = 3;  // msg_trigger
    public const int TrigOff = 4;  // msg_trigoff
    public const int TrigOn = 5;   // msg_trigon
}

/// <summary>
/// A per-kind behaviour handler (kindmsg in X_OBJ.C): given object index n, a message, and an
/// argument z, mutate state and/or draw, returning an int result. Behaviours are ported in a
/// later phase; the framework and dispatch table live here now.
/// </summary>
public delegate int ObjMsg(int n, int msg, int z);
