using XargonNET.Ui;

namespace XargonNET.Engine;

/// <summary>What a game-menu key does (gamemenu(), xargon.c:1062-1115).</summary>
public enum GameMenuAction { None, Return, Quit, Save, Load, Instructions, Inventory, Buy, Granny, Noise }

/// <summary>The game and buy menu strings/geometry (xargon.c menu2/menu3), shared by the game, the
/// <c>--menuframe</c> debug dump and the tests.</summary>
public static class GameMenuDefs
{
    /// <summary>menu2. The ON/OFF states are drawn by the menuflag-2 overlay over the blank gaps.</summary>
    public const string GameText =
        "7    - GAME OPTIONS -    \r3RETURN TO GAME\r2QUIT GAME\r2SAVE GAME\r2LOAD GAME\r2INSTRUCTIONS\r2ADDITIONAL INVENTORY\r2BUY EXTRA ITEMS\r2GRANNY MODE is    \r2NOISE is    \r\r";

    /// <summary>The C keytab is "RQSLIABGN" plus a trailing Ctrl-E (editor), unreachable with num=9 and dropped.</summary>
    public const string GameKeys = "RQSLIABGN";

    /// <summary>menu3 (the buy menu; the buy feature itself is a later task).</summary>
    public const string BuyText =
        "7  What would you like to buy?\r3RETURN TO GAME\r2A. HEALTH UNIT 15 EMERALDS\r2B. EXTRA LASER 10 EMERALDS\r2C. RAPID FIRE  25 EMERALDS\r2D. 1 FIREBALL   5 EMERALDS\r2E. 5 FIREBALLS 20 EMERALDS\r2F. INVINCIBLE  30 EMERALDS\r\r";
    public const string BuyKeys = "RABCDEF";

    /// <summary>domenu(menu2, keys, y0=1, num=9, demo=0, textx0=24, winx8=9, winxl16=10, winy=24, menuflag=2).</summary>
    public static Menu CreateGameMenu() => new(GameText, GameKeys, 1, 9, 24, 9, 10, 24, 2);

    /// <summary>domenu(menu3, keys, 1, 7, 0, 24, 7, 12, 32, 0).</summary>
    public static Menu CreateBuyMenu() => new(BuyText, BuyKeys, 1, 7, 24, 7, 12, 32, 0);

    public static GameMenuAction Map(char key) => key switch
    {
        'R' => GameMenuAction.Return,
        'Q' => GameMenuAction.Quit,
        'S' => GameMenuAction.Save,
        'L' => GameMenuAction.Load,
        'I' => GameMenuAction.Instructions,
        'A' => GameMenuAction.Inventory,
        'B' => GameMenuAction.Buy,
        'G' => GameMenuAction.Granny,
        'N' => GameMenuAction.Noise,
        _ => GameMenuAction.None,
    };
}
