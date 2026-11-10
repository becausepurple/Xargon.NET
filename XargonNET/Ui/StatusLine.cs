namespace XargonNET.Ui;

/// <summary>
/// The bottom status line state (botmsg/botcol/bottime/text_flg) with <c>txt()</c> and <c>upd_botmsg()</c>
/// from XARGON.C:174-185. The C globals start zeroed (empty message, no timer, flag 0), so the first
/// <see cref="Update"/> installs the default message; a freshly built world also calls
/// <c>Txt(default, 4, 1)</c> the way play() does.
/// </summary>
public sealed class StatusLine
{
    /// <summary>botmsg is char[50]: at most 49 characters are kept.</summary>
    public const int MaxLength = 49;

    public string Message { get; private set; } = "";
    public int Color { get; private set; }
    public int Time { get; private set; }
    public int Flag { get; private set; }

    /// <summary>txt(msg,col,flg): flg 0 = transient for 100 ticks, non-zero = persistent.</summary>
    public void Txt(string msg, int col, int flg)
    {
        Message = msg.Length > MaxLength ? msg[..MaxLength] : msg;
        Color = col;
        if (flg == 0) Time = 100;
        Flag = flg;
    }

    /// <summary>upd_botmsg(): once per game tick, after the objects update.</summary>
    public void Update(string defaultMsg)
    {
        if (Time > 0) Time--;
        if (Time == 0 && Flag == 0) Txt(defaultMsg, 4, 1);
    }
}
