using ToasterReskinLoader.emotes;
using Xunit;

public class EmoteSearchTests
{
    static readonly string[] names =
    {
        "catfire",
        "catsmile",
        "pog",
        "dogspin",
        "spin",
    };

    [Theory]
    [InlineData(":ca", 3, 0, "ca")]
    [InlineData("gg :cat", 7, 3, "cat")]
    [InlineData("gg :cat more", 7, 3, "cat")]
    public void FindsQueryAtCaret(string text, int caret, int colon, string query)
    {
        Assert.True(EmoteSearch.TryFindQuery(text, caret, out int c, out string q));
        Assert.Equal(colon, c);
        Assert.Equal(query, q);
    }

    [Theory]
    [InlineData(":c", 2)]
    [InlineData(":", 1)]
    [InlineData("at 12:30", 8)]
    [InlineData("a:cat", 5)]
    [InlineData(":pog:", 5)]
    [InlineData("gg :cat more", 12)]
    [InlineData("", 0)]
    public void IgnoresNonQueries(string text, int caret)
    {
        Assert.False(EmoteSearch.TryFindQuery(text, caret, out _, out _));
    }

    [Fact]
    public void RanksPrefixBeforeSubstringAndShortFirst()
    {
        Assert.Equal(new[] { "spin", "dogspin" }, EmoteSearch.Rank(names, "spin", 10));
        Assert.Equal(new[] { "catfire", "catsmile" }, EmoteSearch.Rank(names, "CAT", 10));
    }

    [Fact]
    public void RankRespectsLimit()
    {
        Assert.Single(EmoteSearch.Rank(names, "cat", 1));
    }

    [Fact]
    public void CompleteReplacesQueryAndAddsSpace()
    {
        string result = EmoteSearch.Complete("gg :ca", 3, 6, "catfire", out int caret);
        Assert.Equal("gg :catfire: ", result);
        Assert.Equal(result.Length, caret);
    }

    [Fact]
    public void CompleteMidTextReusesFollowingSpace()
    {
        string result = EmoteSearch.Complete("gg :ca now", 3, 6, "catfire", out int caret);
        Assert.Equal("gg :catfire: now", result);
        Assert.Equal("gg :catfire: ".Length, caret);
    }

    [Fact]
    public void InsertAddsLeadingSpaceAfterWord()
    {
        string result = EmoteSearch.Insert("gg", 2, "pog", out int caret);
        Assert.Equal("gg :pog: ", result);
        Assert.Equal(result.Length, caret);
    }

    [Fact]
    public void InsertIntoEmptyText()
    {
        Assert.Equal(":pog: ", EmoteSearch.Insert("", 0, "pog", out _));
    }
}
