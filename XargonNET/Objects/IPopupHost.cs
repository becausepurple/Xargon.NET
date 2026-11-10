namespace XargonNET.Objects;

/// <summary>Popup window style: plain (0x44 frame, back 129) or dialog (0x47 frame, back 121).
/// Mirrors the <c>flg</c> argument of the C <c>dotextmsg(n, flg)</c> (0 = plain, dialog = 1).</summary>
public enum PopupStyle { Plain, Dialog }

/// <summary>
/// The blocking popup seam used by object handlers (dotextmsg/dialogmsg). The handler calls one of
/// these and the host runs a nested modal loop that composes a window over the last displayed frame,
/// exactly as the DOS original did inside the object message handler — nothing else updates or draws
/// until the popup is dismissed. The default implementation (<see cref="NullPopupHost"/>) does nothing,
/// which keeps headless/unit code free of SDL; <c>GameApp</c> installs the real
/// <c>PopupWindows</c> implementation.
/// </summary>
public interface IPopupHost
{
    /// <summary>dotextmsg(n, flg): a scrolling/short text popup in the plain or dialog style.</summary>
    void TextMessage(int n, PopupStyle style);

    /// <summary>dialogmsg(n, flg): a portrait dialog popup (hero or eagle face). Implemented in Task 7.</summary>
    void DialogMessage(int n, bool eagle);
}

/// <summary>A no-op popup host: object handlers that raise popups do nothing (tests, headless runs).</summary>
public sealed class NullPopupHost : IPopupHost
{
    public static readonly NullPopupHost Instance = new();
    public void TextMessage(int n, PopupStyle style) { }
    public void DialogMessage(int n, bool eagle) { }
}
