// Prefix the speaker's jersey number to their name in chat messages.
//
// Three chat delivery / rendering flavors exist in the Puck ecosystem:
//
// 1) Standard `ChatMessage` with Username + Content + IsSystem=false.
//    Vanilla `GetChatMessagePrefix` wraps `chatMessage.Username` in
//    team color and prepends it to Content. Most chat-decorating mods
//    (Toaster's Rink Companion star/EIS/BT suffix, etc.) read Username
//    directly. We mutate Username -> "#NN Amikiir" so every downstream
//    consumer naturally decorates around the number.
//
// 2) Toaster's Rink "rich text" system message — IsSystem=true with
//    the entire formatted line baked into Content
//    ("<noparse>...</noparse>: message"). PoncePlayerInput's
//    ChatRichTextPatch detects this format the same way; we follow
//    suit, find a known player's username inside the noparse block,
//    and splice "#NN " in right before it inside Content.
//
// 3) Ponce tag rewrite (PoncePlayerInput Chat.LiveGradient.cs). When
//    Content contains a `[[N|tagText|...]]` marker, LiveGradient's
//    Postfix REPLACES the chat label with a flex-row container of
//    [tagLabel, restLabel] and DISCARDS whatever was before the
//    marker — including the `<color>#NN Amikiir</color>:` prefix our
//    Username mutation produced. Vanilla rendering and our mutation
//    aren't enough to survive this.
//
//    Fix: stash the resolved Player in our ChatManager Prefix and run
//    a very-late `UIChat.AddChatMessage` Postfix (Priority.Last - 100,
//    after LiveGradient's Priority.Last - 1) that walks the newly-
//    inserted chat row. If LiveGradient transformed it into a flex
//    container, we prepend "#NN " to the rest label's text. If it's
//    still a plain Label, the standard mutation already handled it
//    and we do nothing.
//
// Idempotency: a regex check on Username and a startswith check on
// restLabel.text skip re-injection.

using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol;

internal static class NumberedNames
{
    // Matches "#-12 " or "#62 " etc. at the start of a string — the
    // shape we ourselves produce.
    private static readonly Regex _alreadyPrefixed =
        new Regex(@"^#-?\d+ ", RegexOptions.Compiled);

    // Captures the text inside the first `<noparse>...</noparse>` block
    // — PHL/CompTweaks-style servers wrap the decorated username here.
    private static readonly Regex _noparseUsername =
        new Regex(@"<noparse>(?<u>.*?)</noparse>", RegexOptions.Compiled);

    // FixedString32Bytes holds 29 UTF-8 bytes; FixedString512Bytes 510.
    private const int FixedString32MaxChars = 29;
    private const int FixedString512MaxChars = 510;

    // Set by the ChatManager.AddChatMessage Prefix and consumed by the
    // late UIChat.AddChatMessage Postfix. Lives only across the brief
    // window between those two methods on the main thread, so a static
    // slot is safe.
    [ThreadStatic]
    private static Player _pendingPlayer;

    // -- Upstream prefix: mutates Username / Content ------------------

    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.AddChatMessage))]
    [HarmonyPriority(Priority.First)]
    internal static class ChatManager_AddChatMessage_Prefix
    {
        private static void Prefix(ChatMessage chatMessage)
        {
            _pendingPlayer = null;
            if (!(QoLRunner.Instance?.Config?.enableNumberedNames ?? false)) return;
            try
            {
                if (chatMessage == null) return;

                if (chatMessage.IsSystem)
                    TryHandleSystemMessage(chatMessage);
                else
                    TryHandlePlayerMessage(chatMessage);
            }
            catch (Exception e) { Debug.LogWarning("[QoL] numbered chat names failed: " + e.Message); }
        }
    }

    // -- Late postfix: fixes up the rendered row when LiveGradient
    //    swapped our prefix out --------------------------------------

    [HarmonyPatch(typeof(UIChat), "AddChatMessage")]
    [HarmonyPriority(Priority.Last - 100)]
    internal static class UIChat_AddChatMessage_Postfix
    {
        private static void Postfix(UIChat __instance)
        {
            var player = _pendingPlayer;
            _pendingPlayer = null;
            if (player == null) return;
            if (__instance == null) return;

            try
            {
                var messagesRoot = AccessTools.Field(typeof(UIChat), "messages")?.GetValue(__instance) as VisualElement;
                if (messagesRoot == null)
                {
                    var sv = AccessTools.Field(typeof(UIChat), "scrollView")?.GetValue(__instance) as ScrollView;
                    messagesRoot = sv?.contentContainer;
                }
                if (messagesRoot == null || messagesRoot.childCount == 0) return;

                var lastRow = messagesRoot[messagesRoot.childCount - 1];
                if (lastRow == null) return;

                string injection = $"#{player.Number.Value} ";
                string canonicalName = player.Username.Value.ToString();

                // Case A: LiveGradient replaced the original Label with
                // a flex-row container [tagLabel, restLabel] — our
                // mutated chat prefix was discarded. Inject at the
                // start of the rest label.
                var flexContainer = FindNeonFlexContainer(lastRow);
                if (flexContainer != null && flexContainer.childCount >= 2
                    && flexContainer[flexContainer.childCount - 1] is Label restLabel)
                {
                    string current = restLabel.text ?? string.Empty;
                    if (!current.StartsWith(injection, StringComparison.Ordinal)
                        && !_alreadyPrefixed.IsMatch(current.TrimStart()))
                    {
                        // Preserve any single leading space LiveGradient
                        // adds so the tag and the rest aren't flush.
                        int i = 0;
                        while (i < current.Length && current[i] == ' ') i++;
                        restLabel.text = current.Substring(0, i) + injection + current.Substring(i);
                    }
                    return;
                }

                // Case B: Plain Label, but some other Postfix (e.g.
                // Companion's ChatFormatting roster lookup) rewrote
                // label.text using the player's canonical username
                // instead of the one we mutated in ChatManager — our
                // "#NN " never made it into the final string. Locate
                // the canonical username inside the label text and
                // splice "#NN " in front of it.
                Label plainLabel = lastRow as Label ?? lastRow.Q<Label>();
                if (plainLabel == null || string.IsNullOrEmpty(canonicalName)) return;

                string txt = plainLabel.text ?? string.Empty;
                // Already has our prefix somewhere — leave it.
                if (txt.IndexOf(injection + canonicalName, StringComparison.Ordinal) >= 0) return;
                int nameIdx = txt.IndexOf(canonicalName, StringComparison.Ordinal);
                if (nameIdx < 0) return;

                plainLabel.text = txt.Substring(0, nameIdx) + injection + txt.Substring(nameIdx);
            }
            catch (Exception e) { Debug.LogWarning("[QoL] numbered chat names post failed: " + e.Message); }
        }
    }

    // -- Internals -----------------------------------------------------

    // Standard path: mutate Username so downstream decorators see #NN.
    private static void TryHandlePlayerMessage(ChatMessage chatMessage)
    {
        if (!chatMessage.Username.HasValue) return;

        string current = chatMessage.Username.Value.ToString();
        if (string.IsNullOrEmpty(current)) return;
        if (_alreadyPrefixed.IsMatch(current)) return;

        Player player = ResolvePlayer(chatMessage, current);
        if (player == null) return;

        _pendingPlayer = player;

        string injected = $"#{player.Number.Value} {current}";
        if (injected.Length > FixedString32MaxChars)
            injected = injected.Substring(0, FixedString32MaxChars);

        chatMessage.Username = new FixedString32Bytes(injected);
    }

    // System-message path: PHL / CompTweaks / TagMod servers bake the
    // whole chat line into Content with IsSystem=true. Two flavors:
    //   (a) `<noparse>username</noparse>: msg` (older PHL format)
    //   (b) `<rich>[[G|tag|...|...]]<rich2><color>username</color>: msg`
    //       (TagMod gradient/neon, no noparse) — the LiveGradient
    //       Postfix keeps the suffix around `[[G|...]]` markers, so
    //       splicing `#NN ` before the username inside Content survives
    //       the render. (Neon `[[N|...]]` markers discard the prefix
    //       but keep the suffix too; the username sits in the suffix
    //       portion, so the same splice works.)
    private static void TryHandleSystemMessage(ChatMessage chatMessage)
    {
        string content = chatMessage.Content.ToString();
        if (string.IsNullOrEmpty(content)) return;

        // Must look like a chat line. Eliminates join/leave/MOTD/etc.
        int colonIdx = content.IndexOf(": ", StringComparison.Ordinal);
        if (colonIdx < 0) return;

        // Branch (a): noparse block — splice inside it (preserves rank
        // tags that some servers also bake into the noparse text).
        var noparseMatch = _noparseUsername.Match(content);
        if (noparseMatch.Success)
        {
            string decoratedName = noparseMatch.Groups["u"].Value;
            if (string.IsNullOrEmpty(decoratedName)) return;
            if (_alreadyPrefixed.IsMatch(decoratedName)) return;

            var pmA = MonoBehaviourSingleton<PlayerManager>.Instance;
            if (pmA == null) return;
            Player playerA = FindPlayerByUsernameMatch(pmA, decoratedName);
            if (playerA == null) return;
            string pnameA = playerA.Username.Value.ToString();
            if (string.IsNullOrEmpty(pnameA)) return;
            int idxA = decoratedName.IndexOf(pnameA, StringComparison.Ordinal);
            if (idxA < 0) return;

            string insertionA = $"#{playerA.Number.Value} ";
            string newDecorated = decoratedName.Insert(idxA, insertionA);
            string newContentA = content.Substring(0, noparseMatch.Index)
                + "<noparse>" + newDecorated + "</noparse>"
                + content.Substring(noparseMatch.Index + noparseMatch.Length);
            if (newContentA.Length > FixedString512MaxChars)
                newContentA = newContentA.Substring(0, FixedString512MaxChars);
            chatMessage.Content = new FixedString512Bytes(newContentA);
            _pendingPlayer = playerA;
            return;
        }

        // Branch (b): generic system-message chat (e.g. TagMod
        // `[[G|...]]` / `[[N|...]]` markers). Locate any known
        // player's canonical username in the content *before* the
        // first ': ' and splice "#NN " in front of it.
        string prefixPart = content.Substring(0, colonIdx);
        var pm = MonoBehaviourSingleton<PlayerManager>.Instance;
        if (pm == null) return;
        Player player = FindPlayerByUsernameMatch(pm, prefixPart);
        if (player == null) return;
        string pname = player.Username.Value.ToString();
        if (string.IsNullOrEmpty(pname)) return;

        int nameIdx = prefixPart.IndexOf(pname, StringComparison.Ordinal);
        if (nameIdx < 0) return;
        // Already has our prefix immediately before the name? Skip.
        if (nameIdx >= 4)
        {
            // crude check: look back ~12 chars for a "#NN " pattern
            int look = Math.Min(12, nameIdx);
            string look0 = prefixPart.Substring(nameIdx - look, look);
            if (_alreadyPrefixed.IsMatch(look0 + pname)) return;
        }

        string insertion = $"#{player.Number.Value} ";
        string newContent = content.Substring(0, nameIdx) + insertion + content.Substring(nameIdx);
        if (newContent.Length > FixedString512MaxChars)
            newContent = newContent.Substring(0, FixedString512MaxChars);
        chatMessage.Content = new FixedString512Bytes(newContent);
        _pendingPlayer = player;
    }

    private static Player ResolvePlayer(ChatMessage chatMessage, string usernameHint)
    {
        var pm = MonoBehaviourSingleton<PlayerManager>.Instance;
        if (pm == null) return null;

        if (chatMessage.SteamID.HasValue)
        {
            var byId = pm.GetPlayerBySteamId(chatMessage.SteamID.Value);
            if (byId != null) return byId;
        }
        return FindPlayerByUsernameMatch(pm, usernameHint);
    }

    private static Player FindPlayerByUsernameMatch(PlayerManager pm, string haystack)
    {
        var all = pm.GetPlayers();
        if (all == null) return null;
        Player best = null;
        int bestLen = 0;
        foreach (var p in all)
        {
            if (p == null) continue;
            string pname = p.Username.Value.ToString();
            if (string.IsNullOrEmpty(pname)) continue;
            // Prefer the longest matching username so "Amikiir" wins
            // over a short username that happens to be a substring.
            if (haystack.IndexOf(pname, StringComparison.Ordinal) >= 0 && pname.Length > bestLen)
            {
                best = p;
                bestLen = pname.Length;
            }
            else if (pname.IndexOf(haystack, StringComparison.Ordinal) >= 0 && pname.Length > bestLen)
            {
                best = p;
                bestLen = pname.Length;
            }
        }
        return best;
    }

    // Walks down the chat row and returns the first flex-row container
    // that has ≥2 children (the [tagLabel, restLabel] shape produced by
    // LiveGradient's Neon handler). Returns null if the row still
    // contains a plain Label — meaning vanilla rendering kept our
    // Username-mutation #NN and no post-fix is needed.
    private static VisualElement FindNeonFlexContainer(VisualElement element)
    {
        if (element == null) return null;
        if (!(element is Label) && element.childCount >= 2
            && element.style.flexDirection.value == FlexDirection.Row)
            return element;
        for (int i = 0; i < element.childCount; i++)
        {
            var found = FindNeonFlexContainer(element[i]);
            if (found != null) return found;
        }
        return null;
    }
}
