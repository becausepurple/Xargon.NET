namespace XargonNET.Text;

/// <summary>Hard-coded game strings, copied verbatim from the C sources (x_vol*.c, x_obj.c).</summary>
public static class GameStrings
{
    private static readonly string[] VMsgs =
    {
        "VOLUME ONE --- Beyond Reality",
        "VOLUME TWO --- The Secret Chamber",
        "VOLUME THREE --- Xargon's Fury"
    };

    private static readonly string[][] Fidget =
    {
        new[] { "Wow! These EPIC games are great", "The graphics in this game are incredible", "Have you tried KILOBLASTER yet?", "I can't wait to get volumes 2 and 3!" },
        new[] { "Apogee never made games this fun", "I can't imagine anyone being bored playing this", "Don't just stand there looking stupid", "Come on Malvineous get moving" },
        new[] { "What are you waiting for?", "Xargon is watching you!", "Don't give up Malvineous", "You'll never defeat Xargon!" },
    };

    private static readonly string[][] Levels =
    {
        new[]
        {
            "Malvineous returns\r\rto the safety of\r\rthe map level\r",
            "Malvineous begins\r\rhis journey in\r\rthis bizarre world\r",
            "Malvineous enters\r\ra strange\r\rnew dimension\r",
            "Malvineous enters\r\rthe forest of\r\renchanted trees\r",
            "Malvineous discovers\r\ra robot\r\rdevelopment plant\r",
            "Malvineous falls\r\rinto the\r\rcorridors of doom\r",
            "Malvineous ventures\r\rinto the\r\rcave of the ancients\r",
            "Malvineous gets lost\r\rin the mysterious\r\rcorkscrew cave\r",
            "Malvineous plunges\r\rinto the chilly waters\r\rof the coral grave\r",
            "Malvineous faces\r\rcertain death in the\r\rtunnels of terror\r",
            "Malvineous stumbles\r\rinto Xargon's\r\rsecret factory\r",
            "",
            "",
            "",
            "",
            ""
        },
        new[]
        {
            "Malvineous returns\r\rto the safety of\r\rthe map level\r",
            "Malvineous continues\r\rhis quest to find\r\rXargon's Castle\r",
            "Malvineous cautiously\r\renters the\r\rmassive volcano\r",
            "Malvineous explores\r\rmore of this\r\rstrange land\r",
            "Malvineous finds\r\rthe cave of the\r\rdeadly xbat\r",
            "Malvineous is surrounded\r\rby strange\r\ralien creatures\r",
            "Malvineous sloshes\r\rinto the\r\rswamps of despair\r",
            "Malvineous begins\r\rto sweat in\r\rthe lava pits\r",
            "Malvineous gets\r\rlost in the\r\rsurreal forest\r",
            "Malvineous journeys\r\rbeneath\r\rthe earth\r",
            "Malvineous approaches\r\rthe\r\rgrunt's hideout\r",
            "Malvineous dives\r\rinto the\r\rdangerous waters\r",
            "Malvineous discovers\r\rsome\r\rancient ruins\r",
            "Malvineous enters\r\ra\r\rmysterious maze\r",
            "Malvineous storms\r\rinto the\r\rhidden fortress\r",
            "Malvineous creeps\r\rinto the\r\rSecret Chamber\r"
        },
        new[]
        {
            "Malvineous returns\r\rto the safety of\r\rthe map level\r",
            "Malvineous finds\r\ra door to an\r\runderground tunnel\r",
            "Malvineous confronts\r\rmore of the\r\rgiant trolls\r",
            "Malvineous explores\r\ran unusual\r\rtribal village\r",
            "Malvineous takes on\r\rthe\r\rmaze of madness\r",
            "Malvineous journeys\r\rinto another\r\rerupting volcano\r",
            "Malvineous gets\r\rcaught in a\r\rviolent storm\r",
            "Malvineous can\r\rsee an\r\runderwater cove\r",
            "Malvineous searches\r\rfor\r\rprecious jewels\r",
            "Malvineous ventures\r\rinto the\r\rruins of Xarg\r",
            "Malvineous is\r\rtrapped in the\r\rLabyrinth level\r",
            "Malvineous slowly\r\renters the\r\rland of leeches\r",
            "Malvineous awakes\r\rin the\r\rdungeon of death\r",
            "Malvineous sneaks\r\rinto the\r\rdismal looking castle\r",
            "Malvineous forces\r\rhis way into\r\rXargon's eerie castle\r",
            "Malvineous enters\r\rthe evil\r\rXargbot factory\r"
        },
    };

    public static readonly IReadOnlyList<string> InvGetMsg = new[]
    {
        "You are magically transformed into a HUMAN!",
        "USE KEY TO OPEN GATE ON MAP",
        "",
        "",
        "Wow! An extra LASER bullet",
        "You are mysteriously changed into a BUMBLEBEE!",
        "You enter the Submersible Underwater Pod-S.U.P.",
        "YOU FOUND A ROCK TO THROW",
        "These XIBOK shoes help you jump higher",
        "You found a shield of INVINCIBILITY",
        "",
        "",
        "",
        "",
        "",
        "",
        ""
    };

    public static readonly IReadOnlyList<string> DoorMsg = new[]
    {
        "YOU NEED THE KEY. NOW WHERE IS THAT KEY?",
        "YOU MUST FIND THE KEY TO THIS DOOR FIRST.",
        "FIND THE KEY THAT MATCHES THE KEYHOLE.",
        "THE DOOR IS LOCKED. YOU NEED THE KEY!"
    };

    public static readonly IReadOnlyList<string> OpenMsg = new[]
    {
        "THE DOOR OPENS SLOWLY",
        "YOU GAIN ACCESS TO A NEW AREA",
        "YOU FOUND THE RIGHT KEY AND THE DOOR OPENS",
        "CONGRATULATIONS, YOUR KEY FIT!"
    };

    public static readonly IReadOnlyList<string> InvKeyMsg = new[]
    {
        "You found a YELLOW key!",
        "You found a GREEN key!",
        "You found a RED key!",
        "You found a BLUE key!"
    };

    private static int Idx(int episode) => Math.Clamp(episode, 1, 3) - 1;

    /// <summary>Default bottom-line message (v_msg).</summary>
    public static string VMsg(int episode) => VMsgs[Idx(episode)];

    /// <summary>fidgetmsg[i], i in 0..3.</summary>
    public static string FidgetMsg(int episode, int i) => Fidget[Idx(episode)][i];

    /// <summary>leveltxt[level] (CR-separated lines); null for level outside 0..15.</summary>
    public static string? LevelText(int episode, int level)
        => level < 0 || level >= 16 ? null : Levels[Idx(episode)][level];
}
