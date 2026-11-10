using XargonNET.Video;

namespace XargonNET.Ui;

/// <summary>
/// Draws the episode-selection screen. The DOS game shipped one executable per episode; this
/// unified port presents the three episodes and lets the player choose which .XR data set to run.
/// </summary>
public static class EpisodeSelectScreen
{
    public static readonly string[] Names =
    {
        "", "BEYOND REALITY", "THE SECRET CHAMBER", "XARGON'S FURY",
    };

    public static void Draw(VgaScreen screen, TextRenderer text, int selected)
    {
        screen.Clear(0);
        Center(screen, text, 20, TextRenderer.Font8, 15, "X A R G O N");
        Center(screen, text, 40, TextRenderer.Font6, 7, "A .NET PORT  -  SELECT YOUR EPISODE");

        for (int ep = 1; ep <= 3; ep++)
        {
            int y = 80 + (ep - 1) * 20;
            bool on = ep == selected;
            int hi = on ? 15 : 7; // 8 is the fontcolor() two-tone cursor, so dim with 7
            string marker = on ? ">" : " ";
            Center(screen, text, y, TextRenderer.Font8, hi, $"{marker} {ep}.  {Names[ep]}");
        }

        Center(screen, text, 170, TextRenderer.Font6, 3,
            "ARROWS / 1-3 CHOOSE    ENTER START    ESC QUIT");
    }

    private static void Center(VgaScreen screen, TextRenderer text, int y, int font, int color, string s)
    {
        int x = (VgaScreen.Width - TextRenderer.Width(font, s)) / 2;
        text.Draw(screen, screen.Main, x, y, font, color, -1, s);
    }
}
