using System;
using System.Collections.Generic;
using ToasterReskinLoader.emotes;
using Xunit;

public class ChatEmoteFormatterTests
{
    static readonly HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "pog",
        "spin",
    };

    static string TagFor(string name) => known.Contains(name) ? Tag(name) : null;

    static string Format(string s) => ChatEmoteFormatter.Format(s, TagFor, new EmoteFormatOptions());

    static string Hidden(string s, HiddenEmoteMode mode) =>
        ChatEmoteFormatter.Format(s, TagFor, new EmoteFormatOptions { Render = false, WhenHidden = mode });

    static string Tag(string name) => $"<sprite=\"A\" name=\"{name}\">";

    [Fact]
    public void LeavesPlainTextAlone()
    {
        Assert.Equal("hello there", Format("hello there"));
    }

    [Fact]
    public void LeavesUnknownShortcodesAsText()
    {
        Assert.Equal("nice :nope: shot", Format("nice :nope: shot"));
    }

    [Fact]
    public void ReplacesKnownShortcodeInsideText()
    {
        Assert.Equal($"nice {Tag("pog")} shot", Format("nice :pog: shot"));
    }

    [Fact]
    public void EmoteOnlyLineIsJumbo()
    {
        Assert.Equal($"<size=200%>{Tag("pog")} {Tag("spin")}</size>", Format(":pog: :spin:"));
    }

    [Fact]
    public void AdjacentEmotesShareNoColon()
    {
        Assert.Equal($"<size=200%>{Tag("pog")}{Tag("spin")}</size>", Format(":pog::spin:"));
    }

    [Fact]
    public void RejectedMatchFreesItsClosingColon()
    {
        Assert.Equal($"x :nope{Tag("pog")}", Format("x :nope:pog:"));
    }

    [Fact]
    public void TooManyEmotesIsNotJumbo()
    {
        string line = string.Concat(System.Linq.Enumerable.Repeat(":pog:", ChatEmoteFormatter.JUMBO_MAX_EMOTES + 1));
        Assert.DoesNotContain("<size", Format(line));
    }

    [Fact]
    public void ClockTimesAreNotShortcodes()
    {
        Assert.Equal("at 12:30:45", Format("at 12:30:45"));
    }

    [Fact]
    public void MatchIsCaseInsensitiveButKeepsTypedName()
    {
        Assert.Equal($"<size=200%>{Tag("POG")}</size>", Format(":POG:"));
    }

    [Fact]
    public void JumboCanBeTurnedOff()
    {
        var options = new EmoteFormatOptions { Jumbo = false };
        Assert.Equal(Tag("pog"), ChatEmoteFormatter.Format(":pog:", TagFor, options));
    }

    [Fact]
    public void HiddenShowNameLeavesText()
    {
        Assert.Equal("nice :pog: shot", Hidden("nice :pog: shot", HiddenEmoteMode.ShowName));
    }

    [Fact]
    public void HiddenRemoveStripsKnownAndTidiesSpaces()
    {
        Assert.Equal("nice :nope: shot", Hidden("nice :pog: :nope: :spin: shot", HiddenEmoteMode.Remove));
    }

    [Fact]
    public void HiddenRemoveEmptiesEmoteOnlyMessage()
    {
        Assert.Equal("", Hidden(":pog: :spin:", HiddenEmoteMode.Remove));
    }

    [Fact]
    public void HiddenEmoteOnlyHidesEmoteOnlyMessage()
    {
        Assert.Equal("", Hidden(":pog: :spin:", HiddenEmoteMode.HideEmoteOnly));
    }

    [Fact]
    public void HiddenEmoteOnlyKeepsMixedMessageAsTyped()
    {
        Assert.Equal("gg :pog:", Hidden("gg :pog:", HiddenEmoteMode.HideEmoteOnly));
    }
}
