using System;
using System.Collections.Generic;
using System.Linq;

namespace ToasterReskinLoader.emotes;

// Pure string work for autocomplete, no Unity types, so it can be unit tested.
public static class EmoteSearch
{
    public const int MIN_QUERY_LENGTH = 2;
    public const int MAX_NAME_LENGTH = 32;

    // Finds an unfinished ":query" ending at the caret. colonIndex is the index of the ':'.
    // The colon must start the text or follow whitespace, so "12:30" and "a:b" don't trigger.
    public static bool TryFindQuery(string text, int caret, out int colonIndex, out string query)
    {
        colonIndex = -1;
        query = null;
        if (string.IsNullOrEmpty(text) || caret <= 0 || caret > text.Length)
        {
            return false;
        }

        int i = caret - 1;
        while (i >= 0 && IsNameChar(text[i]))
        {
            i--;
        }
        int length = caret - 1 - i;
        if (i < 0 || text[i] != ':' || length < MIN_QUERY_LENGTH || length > MAX_NAME_LENGTH)
        {
            return false;
        }
        if (i > 0 && !char.IsWhiteSpace(text[i - 1]))
        {
            return false;
        }

        colonIndex = i;
        query = text.Substring(i + 1, length);
        return true;
    }

    // Prefix matches first, then substring matches; shorter names first within each group.
    public static List<string> Rank(IEnumerable<string> names, string query, int limit)
    {
        return names
            .Select(name => new { name, index = name.IndexOf(query, StringComparison.OrdinalIgnoreCase) })
            .Where(x => x.index >= 0)
            .OrderBy(x => x.index == 0 ? 0 : 1)
            .ThenBy(x => x.name.Length)
            .ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => x.name)
            .ToList();
    }

    // Replaces text[colonIndex, caret) with ":name: " and returns the caret after it.
    // Skips the trailing space if one already follows.
    public static string Complete(string text, int colonIndex, int caret, string name, out int newCaret)
    {
        string after = text.Substring(caret);
        string insert = ":" + name + ":" + (after.StartsWith(" ") ? "" : " ");
        newCaret = colonIndex + insert.Length + (after.StartsWith(" ") ? 1 : 0);
        return text.Substring(0, colonIndex) + insert + after;
    }

    // Inserts ":name: " at the caret for the picker, adding a leading space if needed.
    public static string Insert(string text, int caret, string name, out int newCaret)
    {
        caret = Math.Max(0, Math.Min(caret, text.Length));
        string before = text.Substring(0, caret);
        string after = text.Substring(caret);
        string lead = before.Length > 0 && !char.IsWhiteSpace(before[before.Length - 1]) ? " " : "";
        string insert = lead + ":" + name + ":" + (after.StartsWith(" ") ? "" : " ");
        newCaret = caret + insert.Length + (after.StartsWith(" ") ? 1 : 0);
        return before + insert + after;
    }

    static bool IsNameChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
    }
}
