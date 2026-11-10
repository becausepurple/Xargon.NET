using System.Buffers.Binary;
using System.Text;

namespace XargonNET.Text;

/// <summary>
/// The 40 text messages stored in an episode's AUDIO.XRn (music.c text_get). Header layout:
/// textposn long[40] at offset 400, textlen int16[40] at offset 560 (little endian).
/// </summary>
public sealed class TextBank
{
    public const int MessageCount = 40;
    private const int PosOffset = 400, LenOffset = 560, HeaderSize = 640;

    private readonly TextMessage?[] _messages = new TextMessage?[MessageCount];

    public static TextBank Load(string audioPath)
    {
        var bank = new TextBank();
        if (!File.Exists(audioPath)) return bank;
        byte[] file = File.ReadAllBytes(audioPath);
        if (file.Length < HeaderSize) return bank;
        for (int n = 0; n < MessageCount; n++)
        {
            long pos = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(PosOffset + 4 * n, 4));
            int len = BinaryPrimitives.ReadInt16LittleEndian(file.AsSpan(LenOffset + 2 * n, 2));
            if (len <= 0 || pos < 0 || pos + len > file.Length) continue;
            bank._messages[n] = new TextMessage(file.AsSpan((int)pos, len).ToArray());
        }
        return bank;
    }

    public bool Has(int n) => Get(n) is not null;

    public TextMessage? Get(int n) => n < 0 || n >= MessageCount ? null : _messages[n];
}

/// <summary>One text message: raw bytes (no terminator); parsing mirrors xargon.c numlines/getline.</summary>
public sealed class TextMessage
{
    private const int MaxLine = 77;
    private readonly byte[] _data;

    internal TextMessage(byte[] data)
    {
        _data = data;
        int cr = 0;
        foreach (byte b in data) if (b == 13) cr++;
        NumLines = cr;
    }

    public int NumLines { get; }

    public (int Color, string Text) GetLine(int n, bool upper)
    {
        if (n < 0 || n >= NumLines) return (7, "");
        int c = 0, a = 0;
        while (a < n && c < _data.Length) a += _data[c++] == 13 ? 1 : 0;
        // C's char is signed: bytes >= 0x80 compare < 32 and are skipped too.
        while (c < _data.Length && (_data[c] < 32 || _data[c] >= 0x80) && _data[c] != 13) c++;
        int color = 7;
        if (c < _data.Length && _data[c] >= '0' && _data[c] <= '7') color = _data[c++] - '0';
        var sb = new StringBuilder();
        while (c < _data.Length && _data[c] != 13 && sb.Length < MaxLine)
        {
            char ch = (char)_data[c++];
            sb.Append(upper && ch >= 'a' && ch <= 'z' ? (char)(ch - 32) : ch);
        }
        return (color, sb.ToString());
    }
}
