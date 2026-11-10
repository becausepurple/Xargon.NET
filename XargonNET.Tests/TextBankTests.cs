using Xunit;
using XargonNET.Assets;
using XargonNET.Text;

namespace XargonNET.Tests;

public class TextBankTests
{
    private static TextBank Bank(int ep) => AssetSet.Mount(AssetSet.FindAssetsDir(), ep).Text;

    [Fact]
    public void Ep1HelpMessageHasTitleAndManyLines()
    {
        var m = Bank(1).Get(1);
        Assert.NotNull(m);
        Assert.True(m!.NumLines > 16);
        Assert.Equal((7, "XARGON HELP"), m.GetLine(0, false));
    }

    [Fact]
    public void Ep1PlatformTipsTitle()
    {
        var m = Bank(1).Get(2)!;
        // Shipped data stores the title in mixed case; the C only uppercases body lines on request.
        Assert.Equal((7, "Moving Platform Tips"), m.GetLine(0, false));
        Assert.Equal((7, "MOVING PLATFORM TIPS"), m.GetLine(0, true));
        Assert.Equal((7, ""), m.GetLine(1, false)); // blank separator line (CR LF CR LF)
        Assert.Equal((3, "There are two ways to get off of"), m.GetLine(2, false));
    }

    [Fact]
    public void Ep3Message37IsAbsent()
    {
        var b = Bank(3);
        Assert.False(b.Has(37));
        Assert.Null(b.Get(37));
        Assert.True(b.Has(36));
    }

    [Fact]
    public void OutOfRangeIndicesReturnNull()
    {
        var b = Bank(1);
        Assert.Null(b.Get(-1));
        Assert.Null(b.Get(40));
        Assert.False(b.Has(-1));
        Assert.False(b.Has(40));
    }

    [Fact]
    public void UppercaseFlagControlsCase()
    {
        var m = Bank(1).Get(4)!;
        Assert.Equal((6, "You may only SAVE a game"), m.GetLine(2, false));
        Assert.Equal((6, "YOU MAY ONLY SAVE A GAME"), m.GetLine(2, true));
    }

    [Fact]
    public void LineIndexPastEndReturnsDefault()
    {
        var m = Bank(1).Get(1)!;
        Assert.Equal((7, ""), m.GetLine(m.NumLines, false));
        Assert.Equal((7, ""), m.GetLine(m.NumLines + 50, true));
    }

    [Fact]
    public void LongLineIsCappedAtSeventySevenCharacters()
    {
        string body = new string('a', 100);
        var m = new TextMessage(System.Text.Encoding.ASCII.GetBytes("5" + body + "\r" + "7" + new string('b', 77) + "\r"));
        Assert.Equal((5, new string('a', 77)), m.GetLine(0, false));
        Assert.Equal((5, new string('A', 77)), m.GetLine(0, true));
        Assert.Equal((7, new string('b', 77)), m.GetLine(1, false)); // exactly 77 is kept whole
        var m78 = new TextMessage(System.Text.Encoding.ASCII.GetBytes("7" + new string('c', 78) + "\r"));
        Assert.Equal((7, new string('c', 77)), m78.GetLine(0, false));
    }

    [Fact]
    public void FirstBodyLineOfJournalEntryHasColourFive()
    {
        var m = Bank(1).Get(31)!;
        Assert.Equal((7, "JOURNAL ENTRY"), m.GetLine(0, false));
        Assert.Equal((5, "Use the arrow keys to scroll!"), m.GetLine(2, false));
    }

    [Fact]
    public void MissingAudioFileGivesEmptyBank()
    {
        var b = TextBank.Load(Path.Combine(Path.GetTempPath(), "no-such-audio.xr9"));
        Assert.False(b.Has(1));
        Assert.Null(b.Get(1));
    }

    [Fact]
    public void LevelTextMatchesC()
    {
        Assert.Equal("Malvineous enters\r\rthe forest of\r\renchanted trees\r", GameStrings.LevelText(1, 3));
        Assert.Equal("", GameStrings.LevelText(1, 11));
        Assert.Equal("", GameStrings.LevelText(1, 15));
        Assert.Null(GameStrings.LevelText(1, 16));
        Assert.Null(GameStrings.LevelText(1, -1));
        Assert.Equal("Malvineous creeps\r\rinto the\r\rSecret Chamber\r", GameStrings.LevelText(2, 15));
        Assert.Equal("Malvineous enters\r\rthe evil\r\rXargbot factory\r", GameStrings.LevelText(3, 15));
    }

    [Fact]
    public void FixedStrings()
    {
        Assert.Equal("VOLUME ONE --- Beyond Reality", GameStrings.VMsg(1));
        Assert.Equal("VOLUME TWO --- The Secret Chamber", GameStrings.VMsg(2));
        Assert.Equal("VOLUME THREE --- Xargon's Fury", GameStrings.VMsg(3));
        Assert.Equal("Come on Malvineous get moving", GameStrings.FidgetMsg(2, 3));
        Assert.Equal("You are mysteriously changed into a BUMBLEBEE!", GameStrings.InvGetMsg[5]);
        Assert.Equal(17, GameStrings.InvGetMsg.Count);
        Assert.Equal("", GameStrings.InvGetMsg[16]);
        Assert.Equal("CONGRATULATIONS, YOUR KEY FIT!", GameStrings.OpenMsg[3]);
        Assert.Equal("THE DOOR IS LOCKED. YOU NEED THE KEY!", GameStrings.DoorMsg[3]);
        Assert.Equal("You found a BLUE key!", GameStrings.InvKeyMsg[3]);
    }
}
