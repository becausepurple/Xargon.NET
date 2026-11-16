namespace XargonNET.Assets;

/// <summary>
/// What a board/file name may look like before it is allowed anywhere near a path. Shipped board names are
/// <c>MAP</c>, <c>INTRO</c>, <c>STORY</c>, <c>BOARD_nn</c> and checkpoint targets such as <c>board_01</c>/<c>demo1</c>;
/// shipped checkpoint "inside" texts are empty, <c>!</c> (back to the map) or a level-song marker
/// (<c>#song_0.xr1</c>, <c>*song_10.XR3</c>, <c>&amp;end_mac.xr1</c>).
/// </summary>
public static class BoardNames
{
    public const int MaxLength = 32;

    private static bool NameChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    /// <summary>A plain board name: 1..32 letters, digits or underscores. No separators, dots, drive colons or NULs.</summary>
    public static bool IsPlain(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxLength) return false;
        foreach (char c in name) if (!NameChar(c)) return false;
        return true;
    }

    /// <summary>A level-song marker: <c>#</c>, <c>*</c> or <c>&amp;</c> then a plain name with at most one
    /// <c>.ext</c> of 1..8 letters/digits.</summary>
    public static bool IsSongMarker(string? s)
    {
        if (s is null || s.Length < 2 || s.Length > MaxLength + 10 || s[0] is not ('#' or '*' or '&')) return false;
        string rest = s.Substring(1);
        int dot = rest.IndexOf('.');
        if (dot < 0) return IsPlain(rest);
        string ext = rest.Substring(dot + 1);
        if (ext.Length is < 1 or > 8) return false;
        foreach (char c in ext) if (!NameChar(c) || c == '_') return false;
        return IsPlain(rest.Substring(0, dot));
    }

    /// <summary>Whether a checkpoint's "inside" text is acceptable: empty/null, <c>!</c>, a song marker or a plain board name.</summary>
    public static bool IsCheckpointInside(string? inside) =>
        string.IsNullOrEmpty(inside) || inside == "!" || IsSongMarker(inside) || IsPlain(inside);
}
