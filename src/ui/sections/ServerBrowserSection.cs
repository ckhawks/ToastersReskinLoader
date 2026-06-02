// Server Browser tweaks + the four browser-side "stores" (saved passwords,
// favorites, blocked, trusted mod lists). Part of the "Tweaks" sidebar group.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ToasterReskinLoader.core;
using ToasterReskinLoader.serverbrowser;
using ToasterReskinLoader.ui; // MissingModsPopupSuppression

namespace ToasterReskinLoader.ui.sections;

public static class ServerBrowserSection
{
    public static void CreateSection(VisualElement root)
    {
        var cfg = SettingsUI.RequireConfig(root,
            "Server browser sorting, filtering, and saved server data.");
        if (cfg == null) return;
        var runner = SettingsRunner.Instance;

        SettingsUI.ToggleRow(root, "Show filters inline", cfg.enableInlineServerBrowserFilters,
            v =>
            {
                cfg.enableInlineServerBrowserFilters = v;
                runner.SaveAndRefresh();
                if (v) InlineServerBrowserFilters.ReapplyInlineFiltersForCurrent();
                else   InlineServerBrowserFilters.UndoInlineFiltersForCurrent();
            });
        SettingsUI.ToggleRow(root, "Remember filters between sessions", cfg.enableBrowserFilterPersistence,
            v => { cfg.enableBrowserFilterPersistence = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Auto-queue when joining a full server", cfg.enableServerSlotQueue,
            v => { cfg.enableServerSlotQueue = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Title-screen Quick Join button", cfg.enableMainMenuQuickJoin,
            v =>
            {
                cfg.enableMainMenuQuickJoin = v;
                runner.SaveAndRefresh();
                MainMenuButtons.RefreshForCurrentMenu();
            });
        SettingsUI.ToggleRow(root, "Title-screen Server Browser button", cfg.enableMainMenuServerBrowser,
            v =>
            {
                cfg.enableMainMenuServerBrowser = v;
                runner.SaveAndRefresh();
                MainMenuButtons.RefreshForCurrentMenu();
            });
        SettingsUI.ToggleRow(root, "Cache server browser between opens", cfg.enableServerPreviewCache,
            v => { cfg.enableServerPreviewCache = v; runner.SaveAndRefresh(); });
        SettingsUI.ToggleRow(root, "Fast server browser scanning (parallel pings)", cfg.enableFastServerBrowserScanning,
            v => { cfg.enableFastServerBrowserScanning = v; runner.SaveAndRefresh(); });

        // ── Server browser stores (compact rows) ──────────────────────────
        // Each store has its own enable toggle + expandable entry list.
        var passwordsList = new VisualElement(); passwordsList.style.marginTop = 4;
        var favoritesList = new VisualElement(); favoritesList.style.marginTop = 4;
        var blockedList   = new VisualElement(); blockedList.style.marginTop = 4;
        var trustedList   = new VisualElement(); trustedList.style.marginTop = 4;

        SettingsUI.Separator(root);

        CompactStoreRow(root,
            "Saved Passwords",
            () => cfg.enableSavedServerPasswords,
            () => SavedServerPasswords.SnapshotKeys().Count,
            v =>
            {
                cfg.enableSavedServerPasswords = v;
                runner.SaveAndRefresh();
                // Re-style open browser rows so the 🔓 auto-fill badge
                // appears/disappears live (the badge rides this toggle,
                // independent of favorites/blocks).
                ServerBrowserSort.RefreshForCurrentBrowser();
                RebuildSavedPasswordsList(passwordsList);
            },
            passwordsList,
            () => RebuildSavedPasswordsList(passwordsList));

        SettingsUI.Separator(root);

        CompactStoreRow(root,
            "Favorites",
            () => cfg.enableServerFavorites,
            () => ServerBrowserSort.SnapshotFavoriteKeys().Count,
            v =>
            {
                cfg.enableServerFavorites = v;
                runner.SaveAndRefresh();
                ServerBrowserSort.RefreshForCurrentBrowser();
                RebuildFavoritesList(favoritesList);
            },
            favoritesList,
            () => RebuildFavoritesList(favoritesList));

        SettingsUI.Separator(root);

        CompactStoreRow(root,
            "Blocked",
            () => cfg.enableServerBlocks,
            () => ServerBrowserSort.SnapshotBlockedKeys().Count,
            v =>
            {
                cfg.enableServerBlocks = v;
                runner.SaveAndRefresh();
                ServerBrowserSort.RefreshForCurrentBrowser();
                RebuildBlockedList(blockedList);
            },
            blockedList,
            () => RebuildBlockedList(blockedList));

        SettingsUI.Separator(root);

        CompactStoreRow(root,
            "Trusted Mod Lists",
            () => cfg.enableTrustedModLists,
            () => MissingModsPopupSuppression.SnapshotKeys().Count,
            v =>
            {
                cfg.enableTrustedModLists = v;
                runner.SaveAndRefresh();
                RebuildTrustedServersList(trustedList);
            },
            trustedList,
            () => RebuildTrustedServersList(trustedList));
    }

    // Single-line entry-store row: title + entry count + enable toggle +
    // expand chevron. Clicking the chevron / title toggles the inline
    // list visibility. The toggle is independent of the expand state.
    private static void CompactStoreRow(
        VisualElement parent,
        string title,
        Func<bool> getEnabled,
        Func<int> getCount,
        Action<bool> onChange,
        VisualElement list,
        Action rebuildList)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 10;
        row.style.marginBottom = 2;

        // Expand chevron on the LEFT, before the title — consistent with
        // the accordion arrows used elsewhere in the menu. Geometric Shapes
        // block (U+25B6/BC) — same family as vanilla's sort-direction arrows
        // in UIServerBrowser, so we know the game's font has them.
        var chevron = new Label("▶");
        chevron.style.fontSize = 12;
        chevron.style.color = new Color(0.7f, 0.7f, 0.7f);
        chevron.style.width = 14;
        chevron.style.marginRight = 6;
        chevron.style.unityTextAlign = TextAnchor.MiddleCenter;
        chevron.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(chevron);

        // flexGrow=1 on the title pushes the count + toggle to the far right edge.
        var titleLbl = new Label($"<b>{title}</b>");
        titleLbl.style.fontSize = 15;
        titleLbl.style.color = Color.white;
        titleLbl.style.flexGrow = 1;
        row.Add(titleLbl);

        int count = getCount();
        var countLbl = new Label($"({count})");
        countLbl.style.fontSize = 12;
        countLbl.style.color = new Color(0.65f, 0.65f, 0.65f);
        countLbl.style.marginRight = 10;
        row.Add(countLbl);

        var t = UITools.CreateConfigurationCheckbox(getEnabled());
        t.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            onChange(evt.newValue);
            countLbl.text = $"({getCount()})";
        });
        row.Add(t);

        parent.Add(row);

        // Initially collapsed.
        list.style.display = DisplayStyle.None;
        list.style.marginLeft = 18;
        list.style.marginBottom = 8;
        parent.Add(list);

        void ToggleExpand()
        {
            bool nowVisible = list.style.display == DisplayStyle.None;
            list.style.display = nowVisible ? DisplayStyle.Flex : DisplayStyle.None;
            chevron.text = nowVisible ? "▼" : "▶"; // ▼ expanded / ▶ collapsed
            if (nowVisible) rebuildList();
            countLbl.text = $"({getCount()})";
        }

        chevron.RegisterCallback<ClickEvent>(_ => ToggleExpand());
        titleLbl.RegisterCallback<ClickEvent>(_ => ToggleExpand());
    }

    // Shared shape for the four server-browser-side "stores" rendered as
    // an inline managed list. They differ only in key source, label text,
    // button labels, and the remove/clear actions.
    private sealed class StoreListSpec
    {
        public Func<bool> Gate;                                   // null = always shown
        public Func<List<string>> GetKeys;
        public string EmptyMessage;
        public Func<string, (string primary, string subtitle)> Labels;
        public string RemoveText;
        public Action<string> Remove;
        public string ClearAllText;
        public Action ClearAll;
        public Action AfterMutate;                                // null = nothing extra
    }

    private static void RebuildStoreList(VisualElement container, StoreListSpec spec)
    {
        if (container == null) return;
        container.Clear();
        if (spec.Gate != null && !spec.Gate()) return;

        void Rebuild() => RebuildStoreList(container, spec);

        var keys = spec.GetKeys();
        if (keys.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel(spec.EmptyMessage);
            empty.style.color = new Color(0.65f, 0.65f, 0.65f);
            empty.style.marginTop = 4;
            empty.style.marginBottom = 4;
            container.Add(empty);
            return;
        }

        foreach (var key in keys)
        {
            var row = UITools.CreateConfigurationRow();
            row.style.alignItems = Align.Center;

            var labelStack = new VisualElement();
            labelStack.style.flexGrow = 1;
            labelStack.style.flexDirection = FlexDirection.Column;

            var (primaryText, subtitleText) = spec.Labels(key);
            labelStack.Add(UITools.CreateConfigurationLabel(primaryText));

            if (!string.IsNullOrEmpty(subtitleText))
            {
                var subtitle = UITools.CreateConfigurationLabel(subtitleText);
                subtitle.style.fontSize = 11;
                subtitle.style.color = new Color(0.65f, 0.65f, 0.65f);
                subtitle.style.marginTop = 0;
                labelStack.Add(subtitle);
            }

            row.Add(labelStack);

            var removeBtn = new Button(() =>
            {
                spec.Remove(key);
                Rebuild();
                spec.AfterMutate?.Invoke();
            })
            { text = spec.RemoveText };
            UITools.StyleConfigButton(removeBtn);
            removeBtn.style.marginLeft = 8;
            row.Add(removeBtn);

            container.Add(row);
        }

        var clearAllRow = UITools.CreateConfigurationRow();
        clearAllRow.style.justifyContent = Justify.FlexEnd;
        clearAllRow.style.marginTop = 8;
        var clearAllBtn = new Button(() =>
        {
            spec.ClearAll();
            Rebuild();
            spec.AfterMutate?.Invoke();
        })
        { text = spec.ClearAllText };
        UITools.StyleConfigButton(clearAllBtn);
        clearAllRow.Add(clearAllBtn);
        container.Add(clearAllRow);
    }

    private static void RebuildSavedPasswordsList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            Gate         = () => SettingsRunner.Instance?.Config?.enableSavedServerPasswords ?? false,
            GetKeys      = SavedServerPasswords.SnapshotKeys,
            EmptyMessage = "No saved passwords yet.",
            Labels       = key =>
            {
                string name = SavedServerPasswords.GetCachedServerName(key);
                return (string.IsNullOrEmpty(name) ? key : name,
                        string.IsNullOrEmpty(name) ? null : key);
            },
            RemoveText   = "Forget",
            Remove       = SavedServerPasswords.Remove,
            ClearAllText = "Forget all saved passwords",
            ClearAll     = SavedServerPasswords.RemoveAll,
        });

    private static void RebuildFavoritesList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            GetKeys      = ServerBrowserSort.SnapshotFavoriteKeys,
            EmptyMessage = "No favorites yet — click the ★ on a server row to favorite it.",
            Labels       = key =>
            {
                string cached = ServerBrowserSort.GetFavoriteCachedName(key);
                return (string.IsNullOrEmpty(cached) ? key : cached,
                        string.IsNullOrEmpty(cached) ? null : key);
            },
            RemoveText   = "Remove",
            Remove       = ServerBrowserSort.RemoveFavorite,
            ClearAllText = "Remove all favorites",
            ClearAll     = ServerBrowserSort.RemoveAllFavorites,
            AfterMutate  = ServerBrowserSort.RefreshForCurrentBrowser,
        });

    private static void RebuildBlockedList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            GetKeys      = ServerBrowserSort.SnapshotBlockedKeys,
            EmptyMessage = "No blocked servers — right-click a row in the server browser to block.",
            Labels       = key =>
            {
                string cached = ServerBrowserSort.GetBlockedCachedName(key);
                return (string.IsNullOrEmpty(cached) ? key : cached,
                        string.IsNullOrEmpty(cached) ? null : key);
            },
            RemoveText   = "Unblock",
            Remove       = ServerBrowserSort.RemoveBlock,
            ClearAllText = "Unblock all servers",
            ClearAll     = ServerBrowserSort.RemoveAllBlocks,
            AfterMutate  = ServerBrowserSort.RefreshForCurrentBrowser,
        });

    private static void RebuildTrustedServersList(VisualElement container)
        => RebuildStoreList(container, new StoreListSpec
        {
            GetKeys      = MissingModsPopupSuppression.SnapshotKeys,
            EmptyMessage = "No trusted servers yet.",
            Labels       = key =>
            {
                string name = SavedServerPasswords.GetCachedServerName(key);
                int modCount = MissingModsPopupSuppression.CountModsFor(key);
                string plural = modCount == 1 ? "" : "s";
                string subtitle = string.IsNullOrEmpty(name)
                    ? $"{modCount} mod{plural} trusted"
                    : $"{key} — {modCount} mod{plural} trusted";
                return (string.IsNullOrEmpty(name) ? key : name, subtitle);
            },
            RemoveText   = "Untrust",
            Remove       = MissingModsPopupSuppression.Remove,
            ClearAllText = "Untrust all servers",
            ClearAll     = MissingModsPopupSuppression.RemoveAll,
        });
}
