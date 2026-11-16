using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ToasterReskinLoader.emotes;

public enum HiddenEmoteMode
{
    // Leave ":name:" as typed.
    ShowName,
    // Strip known shortcodes; a message left empty is hidden.
    Remove,
    // Hide messages made only of emotes; mixed messages keep ":name:".
    HideEmoteOnly,
}

public class EmoteFormatOptions
{
    public bool Render = true;
    public HiddenEmoteMode WhenHidden = HiddenEmoteMode.ShowName;
    public bool Jumbo = true;
}

// Pure string work, no Unity types, so it can be unit tested.
public static class ChatEmoteFormatter
{
    // Discord stops enlarging past 27 emotes on a line.
    public const int JUMBO_MAX_EMOTES = 27;
    public const string JUMBO_SIZE = "200%";

    static readonly Regex shortcode = new Regex(":([A-Za-z0-9_]{2,32}):", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex spriteTag = new Regex("<sprite=\"[^\"]*\" name=\"([A-Za-z0-9_]{2,32})\">", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Turns our sprite tags back into ":name:", so copying a chat line keeps its emotes.
    public static string SpriteTagsToShortcodes(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf("<sprite", StringComparison.Ordinal) < 0)
        {
            return text;
        }
        return spriteTag.Replace(text, ":$1:");
    }

    // Rewrites known :name: shortcodes per the options. Input must already be rich-text filtered
    // by the game so the only tags in the output are ours. Unknown shortcodes always stay as text.
    // An empty result makes the game hide the message.
    public static string Format(string content, string atlasName, Func<string, bool> isKnown, EmoteFormatOptions options)
    {
        if (string.IsNullOrEmpty(content) || content.IndexOf(':') < 0)
        {
            return content;
        }
        if (!options.Render && options.WhenHidden == HiddenEmoteMode.ShowName)
        {
            return content;
        }

        int emoteCount = 0;
        bool hasOtherText = false;
        int last = 0;
        var sb = new StringBuilder(content.Length + 32);

        // A rejected match lets its closing colon start the next shortcode, as in ":nope:pog:".
        int pos = 0;
        while (pos < content.Length)
        {
            Match m = shortcode.Match(content, pos);
            if (!m.Success)
            {
                break;
            }
            string name = m.Groups[1].Value;
            if (!isKnown(name))
            {
                pos = m.Index + m.Length - 1;
                continue;
            }

            hasOtherText |= !IsWhiteSpace(content, last, m.Index);
            sb.Append(content, last, m.Index - last);
            if (options.Render)
            {
                sb.Append("<sprite=\"").Append(atlasName).Append("\" name=\"").Append(name).Append("\">");
            }
            emoteCount++;
            last = m.Index + m.Length;
            pos = last;
        }

        if (emoteCount == 0)
        {
            return content;
        }

        hasOtherText |= !IsWhiteSpace(content, last, content.Length);
        sb.Append(content, last, content.Length - last);

        if (!options.Render)
        {
            if (options.WhenHidden == HiddenEmoteMode.HideEmoteOnly)
            {
                return hasOtherText ? content : string.Empty;
            }
            return CollapseSpaces(sb.ToString());
        }

        if (options.Jumbo && !hasOtherText && emoteCount <= JUMBO_MAX_EMOTES)
        {
            return "<size=" + JUMBO_SIZE + ">" + sb + "</size>";
        }
        return sb.ToString();
    }

    static bool IsWhiteSpace(string s, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (!char.IsWhiteSpace(s[i]))
            {
                return false;
            }
        }
        return true;
    }

    // Removing "a :pog: b" leaves a double space; tidy that and trim.
    static string CollapseSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool lastWasSpace = false;
        foreach (char c in s)
        {
            bool isSpace = char.IsWhiteSpace(c);
            if (isSpace && lastWasSpace)
            {
                continue;
            }
            sb.Append(c);
            lastWasSpace = isSpace;
        }
        return sb.ToString().Trim();
    }
}
