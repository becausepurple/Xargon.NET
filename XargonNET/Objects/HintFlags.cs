namespace XargonNET.Objects;

/// <summary>
/// The first-time tutorial hint flags (X_OBJ.C: <c>first_elev/heart/emerald/fruit/platform/box</c>).
/// Each is a process global in the C, initialised to 1 and never reset by death, new game or level
/// change. The port keeps one instance per episode mount (owned by <c>GameApp</c>, handed to every
/// <see cref="ObjectManager"/>) so the hints persist across levels, deaths and map&lt;-&gt;level
/// transitions and are reset only when a new episode is mounted.
/// </summary>
public sealed class HintFlags
{
    public bool Elev = true;
    public bool Heart = true;
    public bool Emerald = true;
    public bool Fruit = true;
    public bool Platform = true;
    public bool Box = true;
}
