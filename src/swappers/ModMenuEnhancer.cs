using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ToasterReskinLoader.qol;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers
{
    public static class ModMenuEnhancer
    {
        // Cached update state from the most recent check, keyed by workshop item id.
        // A missing key means "not yet checked"; a present entry tells us whether to
        // render the per-row "Update available" button or the neutral "Check" button.
        private static readonly Dictionary<string, WorkshopUpdateChecker.UpdateInfo> updateState = new();
        private static Button batchCheckBtn;
        private static Label batchStatusLabel;
        private static bool batchCheckInProgress;
        private static readonly FieldInfo _modsListField = typeof(UIMods)
            .GetField("modsList", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _modTemplateMapField = typeof(UIMods)
            .GetField("modTemplateContainerMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _pluginTemplateMapField = typeof(UIMods)
            .GetField("pluginTemplateContainerMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _modsField = typeof(UIMods)
            .GetField("mods", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool eventsRegistered;

        public static void RegisterEvents()
        {
            if (eventsRegistered) return;
            eventsRegistered = true;

            EventManager.AddEventListener("Event_OnModEnableFailed",
                new Action<Dictionary<string, object>>(OnModEnableFailed));
            EventManager.AddEventListener("Event_OnPluginEnableFailed",
                new Action<Dictionary<string, object>>(OnPluginEnableFailed));
        }

        private static void OnModEnableFailed(Dictionary<string, object> message)
        {
            var mod = (Mod)message["mod"];
            string name = mod.SteamWorkshopItem?.Details?.Title ?? mod.Id ?? "Unknown mod";
            MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                "Mod Error", $"{name} failed to load!", 5f);
        }

        private static void OnPluginEnableFailed(Dictionary<string, object> message)
        {
            var plugin = (global::Plugin)message["plugin"];
            string name = plugin.Id ?? "Unknown plugin";
            MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                "Plugin Error", $"{name} failed to load!", 5f);
        }

        private static TextField searchField;
        private static string activeFilter = "enabled"; // "enabled", "all", "plugins", "resourcepacks"
        // "alpha" | "downloads" | "ratio"
        private static string sortMode = "alpha";
        private static UIMods currentUIMods;
        private static bool controlsInjected;

        private static Button enabledTab, allTab, pluginsTab, resourceTab;
        private static Button sortButton;
        private static VisualElement restartBanner;
        private static VisualElement updatesBanner;
        private static Label updatesBannerLabel;
        private static bool modListChangedThisSession;
        // compactMode = true → hide descriptions + shrink preview icons.
        private static bool compactMode = false;

        // Master switch from the QoL config. When false, all Harmony postfixes
        // and the EnsureControlsInjected pipeline are skipped — vanilla menu
        // renders untouched. Toggling at runtime only affects future menu opens;
        // controls already injected in this session stay until game restart.
        private static bool IsEnabled() =>
            ToasterReskinLoader.qol.QoLRunner.Instance?.Config?.enableEnhancedModMenu ?? true;

        // Snapshot of all entry→element pairs from both maps.
        // Keys are either Mod or Plugin instances.
        private static readonly List<KeyValuePair<object, VisualElement>> allEntries = new();

        // ── Helpers to abstract over Mod vs Plugin ───────────────────

        private static bool IsLocalPlugin(object entry) => entry is global::Plugin;

        private static bool HasAssembly(object entry)
        {
            if (entry is Mod m) return !IsResourcePack(m.Path);
            if (entry is global::Plugin p) return p.HasAssembly;
            return false;
        }

        // Workshop mods that ship reskinpack.json at the root are resource packs;
        // everything else is a code mod. Vanilla's BasePlugin.HasAssembly check is
        // unreliable because the .dll is sometimes not yet discovered when we render.
        private static bool IsResourcePack(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try { return System.IO.File.Exists(System.IO.Path.Combine(path, "reskinpack.json")); }
            catch { return false; }
        }

        // Cached compatibility flag per .dll path. True = the .dll doesn't reference
        // IPuckPlugin (current b323 interface), so vanilla won't be able to load it.
        private static readonly Dictionary<string, bool> outdatedDllCache = new();

        private static bool IsOutdatedMod(object entry)
        {
            if (entry is not Mod m) return false;
            if (IsResourcePack(m.Path)) return false;
            var dll = FindTopLevelDll(m.Path);
            if (dll == null) return false;
            if (outdatedDllCache.TryGetValue(dll, out var hit)) return hit;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(dll);
                bool hasNew = ContainsAscii(bytes, "IPuckPlugin");
                return outdatedDllCache[dll] = !hasNew;
            }
            catch { return outdatedDllCache[dll] = false; }
        }

        private static string FindTopLevelDll(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                return System.IO.Directory
                    .EnumerateFiles(path, "*.dll", System.IO.SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        private static bool ContainsAscii(byte[] haystack, string needle)
        {
            if (haystack == null || haystack.Length < needle.Length) return false;
            byte[] n = System.Text.Encoding.ASCII.GetBytes(needle);
            int last = haystack.Length - n.Length;
            for (int i = 0; i <= last; i++)
            {
                int j = 0;
                while (j < n.Length && haystack[i + j] == n[j]) j++;
                if (j == n.Length) return true;
            }
            return false;
        }

        private static bool IsEnabled(object entry)
        {
            if (entry is Mod m) return m.IsEnabled;
            if (entry is global::Plugin p) return p.IsEnabled;
            return false;
        }

        private static string GetTitle(object entry)
        {
            if (entry is Mod m)
                return m.SteamWorkshopItem?.Details?.Title ?? m.Id ?? "";
            if (entry is global::Plugin p)
                return p.Id ?? "";
            return "";
        }

        private static string GetPath(object entry)
        {
            if (entry is Mod m) return m.Path;
            if (entry is global::Plugin p) return p.Path;
            return null;
        }

        private static string GetId(object entry)
        {
            if (entry is Mod m) return m.Id;
            if (entry is global::Plugin p) return p.Id;
            return null;
        }

        // A workshop mod is "missing files" when its content folder isn't actually
        // on disk — subscribed but not downloaded yet, or the files were deleted.
        // Local plugins always live on disk (that's how they're discovered), so
        // they're never flagged.
        private static bool IsMissingLocalFolder(object entry)
        {
            if (entry is not Mod m) return false;
            return ResolveModFolder(m) == null;
        }

        // Returns the existing on-disk folder for a workshop mod, or null if none
        // is found. Steam's reported install path is authoritative; fall back to
        // the conventional UGC location relative to the game dir (the game runs
        // with its working directory set to the install folder), which keeps us
        // correct even before Steam has populated Mod.Path.
        private static string ResolveModFolder(Mod m)
        {
            try
            {
                string p = m.Path;
                if (!string.IsNullOrEmpty(p) && System.IO.Directory.Exists(p)) return p;

                string id = m.Id;
                if (!string.IsNullOrEmpty(id))
                {
                    string conventional = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        System.IO.Path.GetFullPath("."), "..", "..",
                        "workshop", "content", PathManager.WorkshopAppId, id));
                    if (System.IO.Directory.Exists(conventional)) return conventional;
                }
            }
            catch { }
            return null;
        }

        // Workshop subscribers/downloads. -1 means "unknown" (local plugin or details
        // not yet loaded) so sorts can push it to the bottom regardless of direction.
        private static int GetDownloads(object entry)
        {
            if (entry is Mod m)
            {
                var d = m.SteamWorkshopItem?.Details;
                if (d != null) return d.Subscriptions;
            }
            return -1;
        }

        // On-disk size of the mod folder, cached per path. -1 = unknown.
        private static readonly Dictionary<string, long> sizeCache = new();

        private static long GetSizeBytes(object entry)
        {
            string path = GetPath(entry);
            if (string.IsNullOrEmpty(path)) return -1;
            if (sizeCache.TryGetValue(path, out var cached)) return cached;
            try
            {
                if (!System.IO.Directory.Exists(path)) return sizeCache[path] = -1;
                long total = 0;
                foreach (var f in System.IO.Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories))
                {
                    try { total += new System.IO.FileInfo(f).Length; } catch { /* skip */ }
                }
                return sizeCache[path] = total;
            }
            catch { return sizeCache[path] = -1; }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.#} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):0.##} GB";
        }

        private static int GetUpvotes(object entry)
        {
            if (entry is Mod m)
            {
                var d = m.SteamWorkshopItem?.Details;
                if (d != null) return d.Upvotes;
            }
            return 0;
        }

        // Returns NaN when there are zero votes (or no details), so callers can push
        // unrated entries to the bottom rather than ranking them as 0% or 100%.
        private static float GetRatio(object entry)
        {
            if (entry is Mod m)
            {
                var d = m.SteamWorkshopItem?.Details;
                if (d != null)
                {
                    int up = d.Upvotes;
                    int down = d.Downvotes;
                    int total = up + down;
                    if (total > 0) return (float)up / total;
                }
            }
            return float.NaN;
        }

        private static string SortLabel(string mode) => mode switch
        {
            "recent"    => "Sort: Recent",
            "downloads" => "Sort: Downloads",
            "ratio"     => "Sort: Rating",
            "size"      => "Sort: Size",
            _           => "Sort: A-Z",
        };

        private static void SortVisible(List<KeyValuePair<object, VisualElement>> list, string mode)
        {
            switch (mode)
            {
                case "downloads":
                    // Descending; unknowns (-1) fall to the bottom, then break ties by title.
                    list.Sort((a, b) =>
                    {
                        int da = GetDownloads(a.Key);
                        int db = GetDownloads(b.Key);
                        if (da < 0 && db >= 0) return 1;
                        if (db < 0 && da >= 0) return -1;
                        int cmp = db.CompareTo(da);
                        return cmp != 0 ? cmp : string.Compare(GetTitle(a.Key), GetTitle(b.Key), StringComparison.CurrentCulture);
                    });
                    break;

                case "ratio":
                    // Descending; NaN (unrated / local) falls to the bottom. Tiebreaker:
                    // higher upvote count wins, then title.
                    list.Sort((a, b) =>
                    {
                        float ra = GetRatio(a.Key);
                        float rb = GetRatio(b.Key);
                        bool aNan = float.IsNaN(ra);
                        bool bNan = float.IsNaN(rb);
                        if (aNan && !bNan) return 1;
                        if (bNan && !aNan) return -1;
                        if (aNan && bNan)
                            return string.Compare(GetTitle(a.Key), GetTitle(b.Key), StringComparison.CurrentCulture);
                        int cmp = rb.CompareTo(ra);
                        if (cmp != 0) return cmp;
                        int upCmp = GetUpvotes(b.Key).CompareTo(GetUpvotes(a.Key));
                        if (upCmp != 0) return upCmp;
                        return string.Compare(GetTitle(a.Key), GetTitle(b.Key), StringComparison.CurrentCulture);
                    });
                    break;

                case "recent":
                    // Leave natural insertion order from allEntries (most recently added first).
                    break;

                case "size":
                    // Descending; unknowns (-1) fall to the bottom.
                    list.Sort((a, b) =>
                    {
                        long sa = GetSizeBytes(a.Key);
                        long sb = GetSizeBytes(b.Key);
                        if (sa < 0 && sb >= 0) return 1;
                        if (sb < 0 && sa >= 0) return -1;
                        int cmp = sb.CompareTo(sa);
                        return cmp != 0 ? cmp : string.Compare(GetTitle(a.Key), GetTitle(b.Key), StringComparison.CurrentCulture);
                    });
                    break;

                default:
                    list.Sort((a, b) => string.Compare(GetTitle(a.Key), GetTitle(b.Key), StringComparison.CurrentCulture));
                    break;
            }
        }

        // ── Inject controls when the mods panel is shown ────────────

        private static void EnsureControlsInjected(UIMods instance)
        {
            currentUIMods = instance;
            if (controlsInjected) return;

            var mods = _modsField?.GetValue(instance) as VisualElement;
            if (mods == null) return;

            var header = mods.Q("Header");
            var content = mods.Q("Content");
            var scrollView = content?.Q<ScrollView>("ScrollView");
            if (header == null || content == null || scrollView == null) return;

            controlsInjected = true;
            activeFilter = "enabled";
            sortMode = "alpha";

            // ── Search field — in header, before close button ──
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;
            searchContainer.style.flexGrow = 1;
            searchContainer.style.justifyContent = Justify.FlexEnd;
            searchContainer.style.marginRight = 8;

            // "Compact Mode" toggle — left of the search field.
            var descToggle = new Toggle("Compact Mode");
            descToggle.value = compactMode;
            descToggle.style.marginRight = 16;
            descToggle.style.fontSize = 14;
            descToggle.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ToasterReskinLoader.qol.VanillaUIRetheme.RecolorTree(descToggle);
                // Force [checkbox] [label] order with breathing room between them.
                descToggle.style.flexDirection = FlexDirection.Row;
                var input = descToggle.Q(className: "unity-toggle__input");
                var lbl = descToggle.Q<Label>(className: "unity-toggle__text");
                if (input != null)
                {
                    input.style.marginRight = 8;
                    input.SendToBack();
                }
                if (lbl != null) lbl.BringToFront();
            });
            descToggle.RegisterValueChangedCallback(evt =>
            {
                compactMode = evt.newValue;
                RefreshDescriptionVisibility();
            });
            searchContainer.Add(descToggle);

            var searchLabel = new Label("Filter:");
            searchLabel.style.fontSize = 16;
            searchLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            searchLabel.style.marginRight = 6;
            searchContainer.Add(searchLabel);

            searchField = new TextField();
            searchField.value = "";
            searchField.style.width = 200;
            searchField.style.fontSize = 16;

            searchField.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                var input = searchField.Q(className: "unity-base-text-field__input");
                if (input != null)
                {
                    input.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
                    input.style.color = Color.white;
                    input.style.paddingLeft = 8;
                    input.style.paddingRight = 8;
                    input.style.paddingTop = 4;
                    input.style.paddingBottom = 4;
                }
            });

            searchField.RegisterCallback<ChangeEvent<string>>(evt => ApplyFilters());
            searchContainer.Add(searchField);

            var closeContainer = header.Q("CloseIconButtonContainer");
            if (closeContainer != null)
            {
                int closeIdx = header.IndexOf(closeContainer);
                header.Insert(closeIdx, searchContainer);
            }

            // ── Tab buttons + sort toggle — inside Content, above ScrollView ──
            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 8;
            tabRow.style.marginTop = 4;
            tabRow.style.paddingLeft = 8;
            tabRow.style.paddingRight = 8;

            enabledTab = CreateTabButton("Enabled", "enabled");
            allTab = CreateTabButton("All", "all");
            pluginsTab = CreateTabButton("Plugins", "plugins");
            resourceTab = CreateTabButton("Resource Packs", "resourcepacks");
            tabRow.Add(enabledTab);
            tabRow.Add(allTab);
            tabRow.Add(pluginsTab);
            tabRow.Add(resourceTab);

            sortButton = new Button { text = "Sort: A-Z" };
            sortButton.style.fontSize = 13;
            sortButton.style.width = 115;
            sortButton.style.paddingTop = 8;
            sortButton.style.paddingBottom = 8;
            sortButton.style.marginLeft = 8;
            sortButton.style.borderTopLeftRadius = 4;
            sortButton.style.borderTopRightRadius = 4;
            sortButton.style.borderBottomLeftRadius = 4;
            sortButton.style.borderBottomRightRadius = 4;
            sortButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            sortButton.style.color = Color.white;
            sortButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            sortButton.clicked += () =>
            {
                sortMode = sortMode switch
                {
                    "alpha" => "recent",
                    "recent" => "downloads",
                    "downloads" => "ratio",
                    "ratio" => "size",
                    _ => "alpha",
                };
                sortButton.text = SortLabel(sortMode);
                ApplyFilters();
            };
            tabRow.Add(sortButton);

            int scrollIdx = content.IndexOf(scrollView);
            if (scrollIdx >= 0)
                content.Insert(scrollIdx, tabRow);

            // ── Restart-recommended banner — hidden until the user toggles or updates a mod ──
            restartBanner = new VisualElement();
            restartBanner.name = "trl-restart-banner";
            restartBanner.style.flexDirection = FlexDirection.Row;
            restartBanner.style.alignItems = Align.Center;
            restartBanner.style.marginLeft = 8;
            restartBanner.style.marginRight = 8;
            restartBanner.style.marginBottom = 6;
            restartBanner.style.paddingTop = 10;
            restartBanner.style.paddingBottom = 10;
            restartBanner.style.paddingLeft = 14;
            restartBanner.style.paddingRight = 14;
            restartBanner.style.backgroundColor = new StyleColor(new Color(0.45f, 0.3f, 0.1f, 0.85f));
            restartBanner.style.borderTopLeftRadius = 4;
            restartBanner.style.borderTopRightRadius = 4;
            restartBanner.style.borderBottomLeftRadius = 4;
            restartBanner.style.borderBottomRightRadius = 4;
            restartBanner.style.justifyContent = Justify.Center;
            restartBanner.style.display = DisplayStyle.None;

            var bannerLabel = new Label("We strongly recommend <b>restarting your game</b> after changing your mod list.");
            bannerLabel.enableRichText = true;
            bannerLabel.style.color = Color.white;
            bannerLabel.style.fontSize = 15;
            bannerLabel.style.whiteSpace = WhiteSpace.Normal;
            bannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            restartBanner.Add(bannerLabel);

            int tabIdx = content.IndexOf(tabRow);
            if (tabIdx >= 0)
                content.Insert(tabIdx, restartBanner);
            else
                content.Insert(0, restartBanner);

            // ── Footer buttons — Open Logs and Open Config, on the left side ──
            // The footer is the 3rd child of Mods (after Header and Content)
            if (mods.childCount >= 3)
            {
                var footer = mods.ElementAt(2);

                var footerLeft = new VisualElement();
                footerLeft.style.flexDirection = FlexDirection.Row;
                footerLeft.style.flexGrow = 1;
                footerLeft.style.alignItems = Align.Center;

                string gameRoot = System.IO.Path.GetFullPath(".");

                var logsBtn = CreateFooterButton("Open Logs", () =>
                    Application.OpenURL($"file://{System.IO.Path.Combine(gameRoot, "Logs")}"));
                footerLeft.Add(logsBtn);

                var configBtn = CreateFooterButton("Open Config", () =>
                    Application.OpenURL($"file://{System.IO.Path.Combine(gameRoot, "config")}"));
                footerLeft.Add(configBtn);

                footer.Insert(0, footerLeft);
            }

            // ── "Check for Updates" button — placed in the header next to "MODS" ──
            batchCheckBtn = new Button { text = "CHECK FOR UPDATES" };
            // Skip the vanilla "button" class — it forces a min-width/padding that
            // makes the button huge and ignores our inline sizing.
            batchCheckBtn.style.fontSize = 11;
            batchCheckBtn.style.height = 24;
            batchCheckBtn.style.minHeight = 0;
            batchCheckBtn.style.maxHeight = 24;
            batchCheckBtn.style.width = 140;
            batchCheckBtn.style.minWidth = 0;
            batchCheckBtn.style.maxWidth = 140;
            batchCheckBtn.style.paddingLeft = 8;
            batchCheckBtn.style.paddingRight = 8;
            batchCheckBtn.style.paddingTop = 2;
            batchCheckBtn.style.paddingBottom = 2;
            batchCheckBtn.style.marginLeft = 12;
            batchCheckBtn.style.alignSelf = Align.Center;
            batchCheckBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            batchCheckBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            batchCheckBtn.style.color = Color.white;
            batchCheckBtn.style.borderTopLeftRadius = 4;
            batchCheckBtn.style.borderTopRightRadius = 4;
            batchCheckBtn.style.borderBottomLeftRadius = 4;
            batchCheckBtn.style.borderBottomRightRadius = 4;
            batchCheckBtn.clicked += RunBatchUpdateCheck;
            // Hidden — we keep this label around so existing batch logic that talks
            // to it still works, but the user-facing "X updates available" message
            // lives in the banner below.
            batchStatusLabel = new Label("");
            batchStatusLabel.style.display = DisplayStyle.None;

            var modsTitle = header.Q<Label>("Mods") ?? header.Q<Label>("Title") ?? header.Q<Label>();
            if (modsTitle != null && modsTitle.parent != null)
            {
                int titleIdx = modsTitle.parent.IndexOf(modsTitle);
                modsTitle.parent.Insert(titleIdx + 1, batchCheckBtn);
            }
            else
            {
                header.Add(batchCheckBtn);
            }
            header.Add(batchStatusLabel);

            // ── Updates-available banner (green) — toggled by the batch check ──
            updatesBanner = new VisualElement();
            updatesBanner.name = "trl-updates-banner";
            updatesBanner.style.flexDirection = FlexDirection.Row;
            updatesBanner.style.alignItems = Align.Center;
            updatesBanner.style.justifyContent = Justify.Center;
            updatesBanner.style.marginLeft = 8;
            updatesBanner.style.marginRight = 8;
            updatesBanner.style.marginBottom = 6;
            updatesBanner.style.paddingTop = 10;
            updatesBanner.style.paddingBottom = 10;
            updatesBanner.style.paddingLeft = 14;
            updatesBanner.style.paddingRight = 14;
            updatesBanner.style.backgroundColor = new StyleColor(new Color(0.15f, 0.4f, 0.2f, 0.85f));
            updatesBanner.style.borderTopLeftRadius = 4;
            updatesBanner.style.borderTopRightRadius = 4;
            updatesBanner.style.borderBottomLeftRadius = 4;
            updatesBanner.style.borderBottomRightRadius = 4;
            updatesBanner.style.display = DisplayStyle.None;

            updatesBannerLabel = new Label("");
            updatesBannerLabel.enableRichText = true;
            updatesBannerLabel.style.color = Color.white;
            updatesBannerLabel.style.fontSize = 15;
            updatesBannerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            updatesBannerLabel.style.whiteSpace = WhiteSpace.Normal;
            updatesBannerLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            updatesBanner.Add(updatesBannerLabel);

            // Place above the restart banner so update info is the first thing seen.
            int restartIdx = content.IndexOf(restartBanner);
            if (restartIdx >= 0)
                content.Insert(restartIdx, updatesBanner);
            else
                content.Insert(0, updatesBanner);

            ToasterReskinLoader.Plugin.Log("[ModMenuEnhancer] Controls injected");
        }

        private static Button CreateFooterButton(string text, Action onClick)
        {
            var btn = new Button { text = text };
            btn.AddToClassList("button");
            btn.style.fontSize = 16;
            btn.style.paddingLeft = 16;
            btn.style.paddingRight = 16;
            btn.style.paddingTop = 6;
            btn.style.paddingBottom = 6;
            btn.style.marginRight = 8;
            btn.style.maxWidth = 130;
            btn.style.borderTopLeftRadius = 0;
            btn.style.borderTopRightRadius = 0;
            btn.style.borderBottomLeftRadius = 0;
            btn.style.borderBottomRightRadius = 0;

            btn.clicked += onClick;
            return btn;
        }

        // Aggregate both Mod and Plugin maps into allEntries
        private static void SnapshotEntries(UIMods instance)
        {
            allEntries.Clear();

            var modMap = _modTemplateMapField?.GetValue(instance) as System.Collections.IDictionary;
            if (modMap != null)
            {
                foreach (System.Collections.DictionaryEntry kvp in modMap)
                {
                    if (kvp.Value is VisualElement ve)
                        allEntries.Add(new KeyValuePair<object, VisualElement>(kvp.Key, ve));
                }
            }

            var pluginMap = _pluginTemplateMapField?.GetValue(instance) as System.Collections.IDictionary;
            if (pluginMap != null)
            {
                foreach (System.Collections.DictionaryEntry kvp in pluginMap)
                {
                    if (kvp.Value is VisualElement ve)
                        allEntries.Add(new KeyValuePair<object, VisualElement>(kvp.Key, ve));
                }
            }
        }

        // ── Patch: Show — inject controls, snapshot entries, reset state ──

        [HarmonyPatch(typeof(UIMods), nameof(UIMods.Show))]
        public static class UIModsShowPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIMods __instance, bool __result)
            {
                ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] Show postfix fired (result={__result})");
                if (!__result) return;
                if (!IsEnabled()) return;
                EnsureControlsInjected(__instance);
                if (searchField != null) searchField.value = "";
                activeFilter = "enabled";
                sortMode = "alpha";
                if (sortButton != null) sortButton.text = SortLabel(sortMode);
                UpdateTabVisuals();

                SnapshotEntries(__instance);
                ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] Show: snapshot has {allEntries.Count} entries");
                foreach (var kvp in allEntries)
                {
                    try { ApplyEnhancements(kvp.Key, kvp.Value); }
                    catch (Exception ex)
                    {
                        ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] ApplyEnhancements threw for '{GetTitle(kvp.Key)}': {ex}");
                    }
                }

                UpdateCounts();
                ApplyFilters();
                RefreshRestartBanner();
            }
        }

        private static void MarkModListChanged()
        {
            modListChangedThisSession = true;
            RefreshRestartBanner();
        }

        private const float ExpandedMinHeight = 140f;
        private const float CompactMinHeight  = 84f;
        private const float ExpandedPreviewSize = 120f;
        private const float CompactPreviewSize  = 38f;

        private static void ApplyCompactness(VisualElement element, VisualElement preview)
        {
            float rowMin = !compactMode ? ExpandedMinHeight : CompactMinHeight;
            float iconSize = !compactMode ? ExpandedPreviewSize : CompactPreviewSize;

            element.style.minHeight = rowMin;
            if (element is TemplateContainer && element.childCount > 0)
                element.ElementAt(0).style.minHeight = rowMin;

            if (preview != null)
            {
                preview.style.width = iconSize;
                preview.style.height = iconSize;
                preview.style.minWidth = 0;
                preview.style.minHeight = 0;
                preview.style.maxWidth = iconSize;
                preview.style.maxHeight = iconSize;
            }
        }

        private static void RefreshDescriptionVisibility()
        {
            foreach (var kvp in allEntries)
            {
                var element = kvp.Value;
                if (element == null) continue;
                var d = element.Q<Label>("DescriptionLabel");
                if (d != null)
                    d.style.display = !compactMode ? DisplayStyle.Flex : DisplayStyle.None;
                var preview = element.Q<VisualElement>("Preview");
                ApplyCompactness(element, preview);
            }
        }

        private static void RefreshRestartBanner()
        {
            if (restartBanner == null) return;
            restartBanner.style.display = modListChangedThisSession
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static Button CreateTabButton(string label, string filter)
        {
            var btn = new Button { text = label };
            btn.style.fontSize = 16;
            btn.style.flexBasis = 0;
            btn.style.flexGrow = 1;
            btn.style.paddingTop = 8;
            btn.style.paddingBottom = 8;
            btn.style.paddingLeft = 12;
            btn.style.paddingRight = 12;
            btn.style.marginRight = 2;
            btn.style.marginLeft = 2;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;

            btn.clicked += () =>
            {
                ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] Changing filter to '{filter}' from '{activeFilter}'");
                activeFilter = filter;
                UpdateTabVisuals();
                ApplyFilters();
            };

            return btn;
        }

        private static void UpdateTabVisuals()
        {
            SetTabActive(enabledTab, activeFilter == "enabled");
            SetTabActive(allTab, activeFilter == "all");
            SetTabActive(pluginsTab, activeFilter == "plugins");
            SetTabActive(resourceTab, activeFilter == "resourcepacks");
        }

        private static void SetTabActive(Button btn, bool active)
        {
            if (btn == null) return;
            btn.style.backgroundColor = active
                ? new StyleColor(new Color(0.35f, 0.35f, 0.35f))
                : new StyleColor(new Color(0.18f, 0.18f, 0.18f));
            btn.style.color = active ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            // Clear the actual CSS border — it draws mitered/diagonal corners when
            // only one edge has width. We use a positioned child as the indicator
            // instead, which gives a clean rectangular bar.
            btn.style.borderBottomWidth = 0;

            const string indName = "trl-tab-indicator";
            var ind = btn.Q<VisualElement>(indName);
            if (ind == null)
            {
                ind = new VisualElement();
                ind.name = indName;
                ind.style.position = Position.Absolute;
                ind.style.left = 0;
                ind.style.right = 0;
                ind.style.bottom = 0;
                ind.style.height = 3;
                ind.style.backgroundColor = new StyleColor(Color.white);
                ind.pickingMode = PickingMode.Ignore;
                btn.Add(ind);
            }
            ind.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void UpdateCounts()
        {
            int enabledCount = 0, totalAssembly = 0, totalResource = 0;
            foreach (var kvp in allEntries)
            {
                if (HasAssembly(kvp.Key))
                {
                    totalAssembly++;
                    if (IsEnabled(kvp.Key)) enabledCount++;
                }
                else
                {
                    totalResource++;
                }
            }

            if (enabledTab != null)
                enabledTab.text = $"Enabled - {enabledCount}";
            if (allTab != null)
                allTab.text = $"All - {allEntries.Count}";
            if (pluginsTab != null)
                pluginsTab.text = $"Plugins - {totalAssembly}";
            if (resourceTab != null)
                resourceTab.text = $"Resource Packs - {totalResource}";
        }

        private static void ApplyFilters()
        {
            if (currentUIMods == null)
            {
                ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] ApplyFilters: currentUIMods is null, aborting");
                return;
            }

            var modsList = _modsListField?.GetValue(currentUIMods) as VisualElement;
            if (modsList == null)
            {
                ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] ApplyFilters: modsList reflection returned null");
                return;
            }

            string search = searchField?.value?.ToLowerInvariant() ?? "";

            var visible = new List<KeyValuePair<object, VisualElement>>();
            foreach (var kvp in allEntries)
            {
                var entry = kvp.Key;

                bool matchesTab = activeFilter == "all"
                    || (activeFilter == "enabled" && HasAssembly(entry) && IsEnabled(entry))
                    || (activeFilter == "plugins" && HasAssembly(entry))
                    || (activeFilter == "resourcepacks" && !HasAssembly(entry));

                string title = GetTitle(entry);
                bool matchesSearch = string.IsNullOrEmpty(search)
                    || title.ToLowerInvariant().Contains(search);

                if (matchesTab && matchesSearch)
                    visible.Add(kvp);
            }

            SortVisible(visible, sortMode);

            int removed = 0;
            foreach (var kvp in allEntries)
            {
                try { kvp.Value.RemoveFromHierarchy(); removed++; }
                catch (Exception ex)
                {
                    ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] ApplyFilters: Remove threw for '{GetTitle(kvp.Key)}': {ex}");
                }
            }

            int added = 0;
            foreach (var kvp in visible)
            {
                var ve = kvp.Value;
                if (ve == null)
                {
                    ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] ApplyFilters: kvp.Value is NULL for '{GetTitle(kvp.Key)}'");
                    continue;
                }
                try
                {
                    modsList.hierarchy.Add(ve);
                    added++;
                }
                catch (Exception ex)
                {
                    ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] ApplyFilters: Add threw for '{GetTitle(kvp.Key)}' (parentNull={ve.parent == null}, type={ve.GetType().Name}): {ex.GetType().Name} {ex.Message}\n{ex.StackTrace}");
                }
            }

        }

        // ── Patches: UpdateMod / UpdatePlugin ────────────────────────

        [HarmonyPatch(typeof(UIMods), "UpdateMod")]
        public static class UIModsUpdateModPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIMods __instance, Mod mod)
            {
                if (!IsEnabled()) return;
                var map = _modTemplateMapField?.GetValue(__instance) as System.Collections.IDictionary;
                if (map == null || !map.Contains(mod))
                {
                    ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer]   UpdateMod: map missing entry (mapNull={map == null})");
                    return;
                }
                if (map[mod] is VisualElement element)
                    ApplyEnhancements(mod, element);
                UpdateCounts();
            }
        }

        [HarmonyPatch(typeof(UIMods), "UpdatePlugin")]
        public static class UIModsUpdatePluginPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIMods __instance, global::Plugin plugin)
            {
                if (!IsEnabled()) return;
                var map = _pluginTemplateMapField?.GetValue(__instance) as System.Collections.IDictionary;
                if (map == null || !map.Contains(plugin))
                {
                    ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer]   UpdatePlugin: map missing entry (mapNull={map == null})");
                    return;
                }
                if (map[plugin] is VisualElement element)
                    ApplyEnhancements(plugin, element);
                UpdateCounts();
            }
        }

        public static void ApplyEnhancements(object entry, VisualElement element)
        {
            string entryName = GetTitle(entry);
            string entryType = IsLocalPlugin(entry) ? "Plugin" : "Mod";

            var desc = element.Q<Label>("DescriptionLabel");
            var preview = element.Q<VisualElement>("Preview");

            // In b323, inner UI.Mod children are populated in OnAttachToPanel.
            // If we got here before attach (e.g. Show postfix racing AddMod's Ready callback),
            // defer until the element is attached. UpdateMod/UpdatePlugin patches will
            // also catch it once vanilla calls them inside Ready.
            if (desc == null || preview == null)
            {
                if (element.panel == null)
                {
                    EventCallback<AttachToPanelEvent> handler = null;
                    handler = _ =>
                    {
                        element.UnregisterCallback(handler);
                        ApplyEnhancements(entry, element);
                    };
                    element.RegisterCallback(handler);
                }
                return;
            }

            // ── Let the row grow vertically to fit our extra content ──
            // Walk up the chain stripping height clamps that prevent expansion.
            element.style.maxHeight = StyleKeyword.None;
            element.style.height = StyleKeyword.Auto;
            if (element is TemplateContainer && element.childCount > 0)
            {
                var card = element.ElementAt(0);
                card.style.maxHeight = StyleKeyword.None;
                card.style.height = StyleKeyword.Auto;
            }
            for (var p = desc.parent; p != null && p != element; p = p.parent)
            {
                p.style.maxHeight = StyleKeyword.None;
                p.style.height = StyleKeyword.Auto;
            }
            ApplyCompactness(element, preview);

            // Clamp the description to ~2 lines so a verbose mod doesn't push the
            // title / stats / buttons off the visible card.
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.maxHeight = 72;
            desc.style.overflow = Overflow.Hidden;
            desc.style.textOverflow = TextOverflow.Ellipsis;
            desc.style.display = !compactMode ? DisplayStyle.Flex : DisplayStyle.None;
            // Preview opacity (already set above) keeps the disabled dimming for the
            // smaller icon; size itself flips with compactness.

            // Vertically center the preview image within its parent column.
            if (preview.parent != null)
                preview.parent.style.justifyContent = Justify.Center;
            preview.style.alignSelf = Align.Center;

            float opacity = (!HasAssembly(entry) || IsEnabled(entry)) ? 1f : 0.3f;
            // Apply to the whole right-side column (title row, stats row, description,
            // bottom button row) plus the preview image. The toggle chip lives on the
            // outer card host, so it stays fully visible.
            if (desc.parent != null)
                desc.parent.style.opacity = opacity;
            preview.style.opacity = opacity;

            // Track where to drop the statistics card; populated when we insert the plain title.
            VisualElement titleHost = null;
            VisualElement plainTitleRef = null;

            // ── Replace the vanilla title Link with a plain Label ──
            // The vanilla UI.Link re-writes its inner TextLabel to "<a><u>" + Text + "</u></a>"
            // every time its Text setter is invoked (which UIMods does after we run), so
            // overwriting the text in-place gets clobbered. Instead hide the Link and insert
            // a sibling Label that mirrors the title in plain text. We already render an
            // explicit "Open on Workshop" button below, so losing the link is fine.
            // Link lives under ModPreviewContainer, not under "Preview" (which is the image),
            // so search from `element` rather than `preview`.
            var linkEl = element.Q<VisualElement>("Link");
            if (linkEl != null)
            {
                linkEl.style.display = DisplayStyle.None;
                // Belt-and-suspenders: also kill the inner TextLabel so we don't depend on
                // display propagation if vanilla styles override it.
                var innerTextLabel = linkEl.Q<Label>("TextLabel");
                if (innerTextLabel != null)
                {
                    innerTextLabel.style.display = DisplayStyle.None;
                    innerTextLabel.text = "";
                }
            }

            // Insert a horizontal "title row" — [plain title | badges] — immediately
            // before the DescriptionLabel so it shares the same parent column and
            // stacks above the description.
            if (desc.parent != null)
            {
                const string titleRowName = "trl-title-row";
                const string plainTitleName = "trl-plain-title";
                const string titleBadgesName = "trl-title-badges";

                var titleRow = desc.parent.Q<VisualElement>(titleRowName);
                if (titleRow == null)
                {
                    titleRow = new VisualElement();
                    titleRow.name = titleRowName;
                    titleRow.style.flexDirection = FlexDirection.Row;
                    titleRow.style.alignItems = Align.Center;
                    titleRow.style.flexWrap = Wrap.Wrap;
                    titleRow.style.marginBottom = 2;
                    int descIdx = desc.parent.IndexOf(desc);
                    desc.parent.Insert(Math.Max(0, descIdx), titleRow);
                }

                var plainTitle = titleRow.Q<Label>(plainTitleName);
                if (plainTitle == null)
                {
                    plainTitle = new Label();
                    plainTitle.name = plainTitleName;
                    plainTitle.style.color = Color.white;
                    plainTitle.style.fontSize = 18;
                    plainTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                    plainTitle.style.whiteSpace = WhiteSpace.Normal;
                    plainTitle.style.flexShrink = 1;
                    titleRow.Add(plainTitle);
                }
                plainTitle.text = GetTitle(entry);

                var titleBadges = titleRow.Q<VisualElement>(titleBadgesName);
                if (titleBadges == null)
                {
                    titleBadges = new VisualElement();
                    titleBadges.name = titleBadgesName;
                    titleBadges.style.flexDirection = FlexDirection.Row;
                    titleBadges.style.flexWrap = Wrap.Wrap;
                    titleBadges.style.alignItems = Align.Center;
                    titleBadges.style.marginLeft = 10;
                    titleRow.Add(titleBadges);
                }
                PopulateBadges(titleBadges, entry);

                titleHost = desc.parent;
                // Anchor for sibling insertions (e.g. the stats row) — needs to be a
                // direct child of titleHost, not a grandchild like `plainTitle`.
                plainTitleRef = titleRow;
            }

            // ── Status chip in the top-right corner of the row card ──
            // Code mods: bundle the vanilla checkbox with a big colored ENABLED/DISABLED
            //   label inside a shared rounded background. UI.Mod caches its toggle in
            //   OnAttachToPanel and UIMods only talks to that reference, so reparenting
            //   the toggle is safe.
            // Resource packs: same chip shape, but show the reskin entry count instead
            //   of the toggle (they're always "on" — TRL just loads what's there).
            {
                const string chipName = "trl-toggle-chip";

                // The TemplateContainer's first child is the actual rounded card.
                VisualElement cardHost = (element is TemplateContainer && element.childCount > 0)
                    ? element.ElementAt(0)
                    : element;

                var chip = cardHost.Q<VisualElement>(chipName);
                if (chip == null)
                {
                    chip = new VisualElement();
                    chip.name = chipName;
                    chip.style.position = Position.Absolute;
                    chip.style.top = 8;
                    chip.style.right = 8;
                    chip.style.minHeight = 32;
                    chip.style.flexDirection = FlexDirection.Row;
                    chip.style.alignItems = Align.Center;
                    chip.style.paddingLeft = 10;
                    chip.style.paddingRight = 10;
                    chip.style.paddingTop = 6;
                    chip.style.paddingBottom = 6;
                    chip.style.borderTopLeftRadius = 6;
                    chip.style.borderTopRightRadius = 6;
                    chip.style.borderBottomLeftRadius = 6;
                    chip.style.borderBottomRightRadius = 6;
                    cardHost.Add(chip);

                    var stateLabel = new Label();
                    stateLabel.name = "trl-state-label";
                    stateLabel.style.fontSize = 14;
                    stateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    stateLabel.style.marginRight = 10;
                    stateLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    chip.Add(stateLabel);

                    if (HasAssembly(entry))
                    {
                        var toggle = element.Q<Toggle>();
                        if (toggle != null)
                        {
                            ReparentToggleIntoChip(toggle, chip);

                            VisualElement capturedInfo = desc.parent;
                            VisualElement capturedPreview = preview;
                            VisualElement capturedElement = element;
                            toggle.RegisterValueChangedCallback(evt =>
                            {
                                ToasterReskinLoader.Plugin.Log($"[ModMenuEnhancer] User clicked {entryType} '{entryName}' to {(evt.newValue ? "ENABLE; ENABLING..." : "DISABLE; DISABLING...")}");
                                UpdateToggleChip(chip, stateLabel, evt.newValue);
                                float op = evt.newValue ? 1f : 0.3f;
                                if (capturedInfo != null) capturedInfo.style.opacity = op;
                                if (capturedPreview != null) capturedPreview.style.opacity = op;
                                var bRow = capturedElement?.Q<VisualElement>("trl-bottom-row");
                                if (bRow != null) bRow.style.opacity = 1f;
                                MarkModListChanged();
                            });
                        }
                    }
                }

                var stateLbl = chip.Q<Label>("trl-state-label");
                if (IsMissingLocalFolder(entry))
                {
                    // Subscribed but the content folder isn't on disk (Steam hasn't
                    // finished downloading, or the files were removed). There's
                    // nothing for the loader to load, so pull the toggle into the
                    // chip, make it non-interactive so the mod can't be enabled,
                    // and flag the row.
                    var toggles = element.Query<Toggle>().ToList();
                    foreach (var t in toggles)
                    {
                        if (t.parent != chip)
                            ReparentToggleIntoChip(t, chip);
                    }
                    var toggle = chip.Q<Toggle>() ?? (toggles.Count > 0 ? toggles[0] : null);
                    if (toggle != null) toggle.SetEnabled(false);
                    if (stateLbl != null) UpdateMissingFilesChip(chip, stateLbl);
                }
                else if (HasAssembly(entry))
                {
                    // Walk EVERY Toggle in the row. The one already inside the chip
                    // stays. Any others (the vanilla template's toggle that drifted
                    // back, or a fresh one vanilla re-instantiated) get reparented
                    // in too — so we never end up with a stray checkbox sitting in
                    // the vanilla position.
                    var toggles = element.Query<Toggle>().ToList();
                    foreach (var t in toggles)
                    {
                        if (t.parent != chip)
                            ReparentToggleIntoChip(t, chip);
                    }
                    var toggle = chip.Q<Toggle>() ?? (toggles.Count > 0 ? toggles[0] : null);

                    if (IsOutdatedMod(entry))
                    {
                        if (toggle != null) toggle.SetEnabled(false);
                        if (stateLbl != null) UpdateOutdatedChip(chip, stateLbl);
                    }
                    else
                    {
                        if (toggle != null) toggle.SetEnabled(true);
                        if (stateLbl != null && toggle != null)
                            UpdateToggleChip(chip, stateLbl, toggle.value);
                    }
                }
                else
                {
                    if (stateLbl != null)
                        UpdatePackChip(chip, stateLbl, entry);
                }
            }

            // ── Tuck the vanilla Statistics block out of sight ──
            // CRITICAL: we do NOT reparent it (ModPreview.OnAttachToPanel re-queries
            // it on every attach and would NRE → ApplyFilters aborts → no rows).
            // We also can't use display:None — that suppresses style resolution, so
            // we'd never be able to read the icon backgroundImage out of USS. So we
            // pull it out of flow with position:Absolute + opacity:0 instead.
            var statistics = element.Q<VisualElement>("Statistics");
            if (statistics != null)
            {
                statistics.style.position = Position.Absolute;
                statistics.style.opacity = 0;
                statistics.style.top = 0;
                statistics.style.left = 0;
                statistics.pickingMode = PickingMode.Ignore;
            }

            // ── Our own stats card + badges row under the title ──
            // Built from mod data, so we don't touch vanilla elements.
            if (titleHost != null && plainTitleRef != null)
            {
                const string statsRowName = "trl-stats-row";
                var statsRow = titleHost.Q<VisualElement>(statsRowName);
                if (statsRow == null)
                {
                    statsRow = new VisualElement();
                    statsRow.name = statsRowName;
                    statsRow.style.flexDirection = FlexDirection.Row;
                    statsRow.style.alignItems = Align.Center;
                    statsRow.style.flexWrap = Wrap.Wrap;
                    statsRow.style.marginTop = 2;
                    statsRow.style.marginBottom = 4;
                    int titleIdx = titleHost.IndexOf(plainTitleRef);
                    titleHost.Insert(titleIdx + 1, statsRow);
                }

                const string statsCardName = "trl-stats-card";
                var statsCard = statsRow.Q<VisualElement>(statsCardName);
                if (statsCard == null)
                {
                    statsCard = new VisualElement();
                    statsCard.name = statsCardName;
                    statsCard.style.flexDirection = FlexDirection.Row;
                    statsCard.style.alignItems = Align.Center;
                    statsCard.style.minHeight = 26;
                    statsCard.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.35f));
                    statsCard.style.paddingLeft = 10;
                    statsCard.style.paddingRight = 10;
                    statsCard.style.paddingTop = 4;
                    statsCard.style.paddingBottom = 4;
                    statsCard.style.borderTopLeftRadius = 4;
                    statsCard.style.borderTopRightRadius = 4;
                    statsCard.style.borderBottomLeftRadius = 4;
                    statsCard.style.borderBottomRightRadius = 4;
                    statsRow.Add(statsCard);

                    var neutral = new Color(0.8f, 0.8f, 0.8f);
                    var green = new Color(0.4f, 1f, 0.5f);
                    var red = new Color(1f, 0.45f, 0.45f);
                    statsCard.Add(BuildStatItem("trl-dl", neutral));
                    statsCard.Add(BuildStatItem("trl-up", green));
                    statsCard.Add(BuildStatItem("trl-dn", red));

                    // Size has no icon to steal from vanilla, so just a label.
                    var szItem = new VisualElement();
                    szItem.name = "trl-sz";
                    szItem.style.flexDirection = FlexDirection.Row;
                    szItem.style.alignItems = Align.Center;
                    var szLabel = new Label();
                    szLabel.name = "trl-sz-label";
                    szLabel.style.fontSize = 11;
                    szLabel.style.color = neutral;
                    szItem.Add(szLabel);
                    statsCard.Add(szItem);

                    // Icons live in USS-applied backgroundImage on hidden vanilla children;
                    // those aren't resolved until after a layout pass. Defer the copy.
                    if (statistics != null)
                    {
                        VisualElement capturedStats = statistics;
                        VisualElement capturedCard = statsCard;
                        capturedCard.schedule.Execute(() => StealStatIconsInto(capturedStats, capturedCard))
                            .Until(() => StealStatIconsInto(capturedStats, capturedCard));
                    }
                }
                PopulateStatsCard(statsCard, entry);
            }

            // ── Bottom row: action button + badges ──
            const string bottomRowName = "trl-bottom-row";
            var bottomRow = element.Q<VisualElement>(bottomRowName);
            // Place the action buttons inline with the stats card (to its right).
            // Fall back to desc.parent if the stats row hasn't been created.
            var buttonHost = element.Q<VisualElement>("trl-stats-row") ?? desc.parent;
            if (bottomRow == null && buttonHost != null)
            {
                bottomRow = new VisualElement();
                bottomRow.name = bottomRowName;
                bottomRow.style.flexDirection = FlexDirection.Row;
                bottomRow.style.alignItems = Align.Center;
                bottomRow.style.flexWrap = Wrap.Wrap;
                bottomRow.style.marginLeft = 8;
                buttonHost.Add(bottomRow);
            }
            else if (bottomRow != null && buttonHost != null && bottomRow.parent != buttonHost)
            {
                // Existing row created under desc.parent in an earlier session — move it.
                bottomRow.RemoveFromHierarchy();
                bottomRow.style.marginTop = 0;
                bottomRow.style.marginLeft = 8;
                buttonHost.Add(bottomRow);
            }
            if (bottomRow == null) return;
            // Buttons always at full opacity so they're still readable / clickable
            // even when the mod is disabled.
            bottomRow.style.opacity = 1f;

            // Action button: "Open on Workshop" for workshop mods, "Open Folder" for local plugins
            const string actionBtnName = "trl-action-btn";
            if (bottomRow.Q<Button>(actionBtnName) == null)
            {
                bool localPlugin = IsLocalPlugin(entry);
                var actionBtn = new Button { text = localPlugin ? "Open Folder" : "Open on Workshop" };
                actionBtn.name = actionBtnName;
                actionBtn.style.fontSize = 12;
                actionBtn.style.width = 130;
                actionBtn.style.height = 26;
                actionBtn.style.paddingTop = 2;
                actionBtn.style.paddingBottom = 2;
                actionBtn.style.marginRight = 8;
                actionBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                actionBtn.style.color = Color.white;
                actionBtn.style.borderTopLeftRadius = 4;
                actionBtn.style.borderTopRightRadius = 4;
                actionBtn.style.borderBottomLeftRadius = 4;
                actionBtn.style.borderBottomRightRadius = 4;
                actionBtn.style.unityTextAlign = TextAnchor.MiddleCenter;

                actionBtn.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    actionBtn.style.backgroundColor = new StyleColor(Color.white);
                    actionBtn.style.color = Color.black;
                });
                actionBtn.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    actionBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                    actionBtn.style.color = Color.white;
                });

                if (localPlugin)
                {
                    string modPath = GetPath(entry);
                    actionBtn.clicked += () => Application.OpenURL($"file://{modPath}");
                }
                else
                {
                    string workshopId = GetId(entry);
                    actionBtn.clicked += () =>
                        Application.OpenURL($"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}");
                }

                bottomRow.Add(actionBtn);
            }

            // ── Per-mod update button (workshop mods only) ──
            if (!IsLocalPlugin(entry))
            {
                string itemId = GetId(entry);
                EnsureUpdateButton(bottomRow, itemId);
            }

        }

        // Returns true once every icon slot has a backgroundImage (so the scheduler
        // can use it as both the action and the Until predicate).
        private static bool StealStatIconsInto(VisualElement statistics, VisualElement card)
        {
            bool allFilled = true;
            allFilled &= CopyIconFromStatChild(statistics, "Subscriptions", card.Q<VisualElement>("trl-dl-icon"));
            allFilled &= CopyIconFromStatChild(statistics, "Upvotes",       card.Q<VisualElement>("trl-up-icon"));
            allFilled &= CopyIconFromStatChild(statistics, "Downvotes",     card.Q<VisualElement>("trl-dn-icon"));
            return allFilled;
        }

        private static bool CopyIconFromStatChild(VisualElement statistics, string childName, VisualElement target)
        {
            if (target == null) return true; // nothing to fill, treat as done
            // Already filled this slot — don't re-fill on later schedule ticks.
            var existing = target.resolvedStyle.backgroundImage;
            if (existing.texture != null || existing.sprite != null || existing.vectorImage != null)
                return true;

            if (statistics == null) return false;
            var container = statistics.Q<VisualElement>(childName);
            if (container == null) return false;

            foreach (var ve in container.Query<VisualElement>().ToList())
            {
                if (ve is Label) continue;
                var rs = ve.resolvedStyle.backgroundImage;
                if (rs.texture != null || rs.sprite != null || rs.vectorImage != null)
                {
                    target.style.backgroundImage = new StyleBackground(rs);
                    return true;
                }
            }
            return false;
        }

        private static VisualElement BuildStatItem(string name, Color tint, bool isLast = false)
        {
            var item = new VisualElement();
            item.name = name;
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            if (!isLast) item.style.marginRight = 12;

            const float IconSize = 14f;
            var iconEl = new VisualElement();
            iconEl.name = name + "-icon";
            iconEl.style.width = IconSize;
            iconEl.style.height = IconSize;
            iconEl.style.marginRight = 4;
            iconEl.style.unityBackgroundImageTintColor = new StyleColor(tint);
            item.Add(iconEl);

            var label = new Label();
            label.name = name + "-label";
            label.style.fontSize = 11;
            label.style.color = tint;
            item.Add(label);

            return item;
        }

        private static void PopulateStatsCard(VisualElement card, object entry)
        {
            int subs = 0, upv = 0, dnv = 0;
            bool hasData = false;
            if (entry is Mod m && m.SteamWorkshopItem?.Details is { } d)
            {
                subs = d.Subscriptions;
                upv = d.Upvotes;
                dnv = d.Downvotes;
                hasData = true;
            }

            long sizeBytes = GetSizeBytes(entry);
            string sizeStr = sizeBytes >= 0 ? FormatBytes(sizeBytes) : "";

            // Show the card if we have ANY data (workshop stats or filesize).
            if (!hasData && string.IsNullOrEmpty(sizeStr))
            {
                card.style.display = DisplayStyle.None;
                return;
            }

            card.style.display = DisplayStyle.Flex;
            var dlLabel = card.Q<Label>("trl-dl-label");
            var upLabel = card.Q<Label>("trl-up-label");
            var dnLabel = card.Q<Label>("trl-dn-label");
            var szItem  = card.Q<VisualElement>("trl-sz");
            var szLabel = card.Q<Label>("trl-sz-label");

            if (dlLabel != null) dlLabel.text = hasData ? subs.ToString("N0") : "";
            if (upLabel != null) upLabel.text = hasData ? upv.ToString("N0") : "";
            if (dnLabel != null) dnLabel.text = hasData ? dnv.ToString("N0") : "";

            // Hide workshop-only items for entries without workshop data.
            if (!hasData)
            {
                foreach (var n in new[] { "trl-dl", "trl-up", "trl-dn" })
                    card.Q<VisualElement>(n)?.SetEnabled(false);
                foreach (var n in new[] { "trl-dl", "trl-up", "trl-dn" })
                {
                    var e = card.Q<VisualElement>(n);
                    if (e != null) e.style.display = DisplayStyle.None;
                }
            }
            else
            {
                foreach (var n in new[] { "trl-dl", "trl-up", "trl-dn" })
                {
                    var e = card.Q<VisualElement>(n);
                    if (e != null) e.style.display = DisplayStyle.Flex;
                }
            }

            if (szItem != null && szLabel != null)
            {
                bool show = !string.IsNullOrEmpty(sizeStr);
                szItem.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                // Add a left margin only when there are stats to the left of it.
                szItem.style.marginLeft = hasData ? 12 : 0;
                szLabel.text = sizeStr;
            }
        }

        private static void PopulateBadges(VisualElement badgeContainer, object entry)
        {
            // Uniform dark-gray background for all badges; the text color carries the meaning.
            var badgeBg = new Color(0.32f, 0.32f, 0.32f, 0.9f);

            if (badgeContainer.Q<Label>("trl-source-badge") == null)
            {
                if (IsLocalPlugin(entry))
                    badgeContainer.Add(CreateBadge("trl-source-badge", "Local",
                        new Color(0.85f, 0.85f, 0.85f), badgeBg));
                else
                    badgeContainer.Add(CreateBadge("trl-source-badge", "Workshop",
                        new Color(0.55f, 1f, 0.6f), badgeBg));
            }

            // Kind: "Plugin" if a code mod (top-level .dll), "Resource Pack" otherwise.
            // Remove the opposite-kind badge so an entry that was misclassified on a
            // previous render (e.g. before m.Path was populated) gets cleaned up.
            if (HasAssembly(entry))
            {
                badgeContainer.Q<Label>("trl-rp-badge")?.RemoveFromHierarchy();
                if (badgeContainer.Q<Label>("trl-plugin-badge") == null)
                    badgeContainer.Add(CreateBadge("trl-plugin-badge", "Plugin",
                        new Color(1f, 0.8f, 0.4f), badgeBg));
            }
            else
            {
                badgeContainer.Q<Label>("trl-plugin-badge")?.RemoveFromHierarchy();
                if (badgeContainer.Q<Label>("trl-rp-badge") == null)
                    badgeContainer.Add(CreateBadge("trl-rp-badge", "Resource Pack",
                        new Color(0.55f, 0.8f, 1f), badgeBg));
            }
        }

        private static void UpdatePackChip(VisualElement chip, Label label, object entry)
        {
            int count = 0;
            if (entry is Mod m && ulong.TryParse(m.Id, out var wid))
            {
                var pack = ReskinRegistry.reskinPacks
                    .Find(p => p.WorkshopId == wid);
                if (pack?.Reskins != null) count = pack.Reskins.Count;
            }

            label.text = count > 0 ? $"{count} RESKIN{(count == 1 ? "" : "S")}" : "IN TRL";
            label.style.color = new Color(0.6f, 0.85f, 1f);
            label.style.marginRight = 0;
            chip.style.backgroundColor = new StyleColor(new Color(0.15f, 0.25f, 0.4f, 0.7f));
        }

        private static void ReparentToggleIntoChip(Toggle toggle, VisualElement chip)
        {
            toggle.RemoveFromHierarchy();
            // Reset USS-applied positioning / sizing so the toggle behaves like a
            // normal inline flex child of the chip.
            toggle.style.position = Position.Relative;
            toggle.style.top = StyleKeyword.Auto;
            toggle.style.left = StyleKeyword.Auto;
            toggle.style.right = StyleKeyword.Auto;
            toggle.style.bottom = StyleKeyword.Auto;
            toggle.style.marginTop = 0;
            toggle.style.marginBottom = 0;
            toggle.style.marginLeft = 0;
            toggle.style.marginRight = 0;
            toggle.style.paddingTop = 0;
            toggle.style.paddingBottom = 0;
            toggle.style.paddingLeft = 0;
            toggle.style.paddingRight = 0;
            toggle.style.flexShrink = 0;
            toggle.style.flexGrow = 0;
            toggle.style.minHeight = 0;
            toggle.style.minWidth = 0;
            toggle.style.width = 22;
            toggle.style.height = 22;
            toggle.style.alignSelf = Align.Center;
            toggle.style.flexDirection = FlexDirection.Row;
            chip.Add(toggle);

            var tInput = toggle.Q(className: "unity-toggle__input");
            if (tInput != null)
            {
                tInput.style.position = Position.Relative;
                tInput.style.top = StyleKeyword.Auto;
                tInput.style.left = StyleKeyword.Auto;
                tInput.style.width = 22;
                tInput.style.height = 22;
                tInput.style.minWidth = 0;
                tInput.style.minHeight = 0;
                tInput.style.maxWidth = 22;
                tInput.style.maxHeight = 22;
                tInput.style.marginLeft = 0;
                tInput.style.marginRight = 0;
                tInput.style.marginTop = 0;
                tInput.style.marginBottom = 0;
                tInput.style.paddingLeft = 0;
                tInput.style.paddingRight = 0;
                tInput.style.flexShrink = 0;
                tInput.style.flexGrow = 0;
            }
            var tText = toggle.Q<Label>(className: "unity-toggle__text");
            if (tText != null) tText.style.display = DisplayStyle.None;
        }

        private static void UpdateOutdatedChip(VisualElement chip, Label label)
        {
            label.text = "OUTDATED B202";
            label.style.color = new Color(0.85f, 0.85f, 0.85f);
            label.style.marginRight = 0;
            chip.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f, 0.85f));
        }

        private static void UpdateMissingFilesChip(VisualElement chip, Label label)
        {
            label.text = "MISSING FILES";
            label.style.color = new Color(1f, 0.55f, 0.2f);
            label.style.marginRight = 0;
            chip.style.backgroundColor = new StyleColor(new Color(0.4f, 0.15f, 0.05f, 0.9f));
        }

        private static void UpdateToggleChip(VisualElement chip, Label label, bool enabled)
        {
            // Re-assert the gap between label and toggle — older builds created the
            // label without this margin, and the chip persists across reloads.
            label.style.marginRight = 10;
            if (enabled)
            {
                label.text = "ENABLED";
                label.style.color = new Color(0.2f, 1f, 0.4f);
                chip.style.backgroundColor = new StyleColor(new Color(0.1f, 0.35f, 0.15f, 0.7f));
            }
            else
            {
                label.text = "DISABLED";
                label.style.color = new Color(1f, 0.5f, 0.5f);
                chip.style.backgroundColor = new StyleColor(new Color(0.4f, 0.15f, 0.15f, 0.7f));
            }
        }

        private static void ShrinkStatistics(VisualElement statistics)
        {
            const float IconSize = 14f;

            statistics.style.fontSize = 10;
            statistics.Query<Label>().ForEach(label =>
            {
                label.style.fontSize = 10;
                label.style.color = new Color(0.7f, 0.7f, 0.7f);
            });

            statistics.Query<Image>().ForEach(img =>
            {
                img.style.width = IconSize;
                img.style.height = IconSize;
                img.style.maxWidth = IconSize;
                img.style.maxHeight = IconSize;
                img.style.minWidth = 0;
                img.style.minHeight = 0;
            });

            // Many UI Toolkit "icons" are plain VisualElements with a background image.
            // Shrink any descendant whose backgroundImage is set and which has no children
            // (so we don't accidentally squash a container).
            statistics.Query<VisualElement>().ForEach(ve =>
            {
                if (ve == statistics || ve is Label || ve is Image) return;
                if (ve.childCount != 0) return;
                var bg = ve.resolvedStyle.backgroundImage;
                if (bg.texture == null && bg.sprite == null && bg.vectorImage == null) return;

                ve.style.width = IconSize;
                ve.style.height = IconSize;
                ve.style.maxWidth = IconSize;
                ve.style.maxHeight = IconSize;
                ve.style.minWidth = 0;
                ve.style.minHeight = 0;
            });
        }

        // ── Workshop update check ────────────────────────────────────

        private const string UpdateBtnName = "trl-update-btn";

        private static void EnsureUpdateButton(VisualElement bottomRow, string itemId)
        {
            var existing = bottomRow.Q<Button>(UpdateBtnName);
            if (existing == null)
            {
                existing = new Button();
                existing.name = UpdateBtnName;
                existing.style.fontSize = 12;
                existing.style.height = 26;
                existing.style.paddingTop = 2;
                existing.style.paddingBottom = 2;
                existing.style.marginRight = 8;
                existing.style.paddingLeft = 12;
                existing.style.paddingRight = 12;
                existing.style.borderTopLeftRadius = 4;
                existing.style.borderTopRightRadius = 4;
                existing.style.borderBottomLeftRadius = 4;
                existing.style.borderBottomRightRadius = 4;
                existing.style.unityTextAlign = TextAnchor.MiddleCenter;

                // Hover: invert to white/black; on leave restore the state-driven look.
                var btn = existing;
                btn.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    btn.style.backgroundColor = new StyleColor(Color.white);
                    btn.style.color = Color.black;
                });
                btn.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    RefreshUpdateButton(btn, itemId);
                });

                int actionIdx = bottomRow.IndexOf(bottomRow.Q<Button>("trl-action-btn"));
                bottomRow.Insert(actionIdx + 1, existing);
            }

            RefreshUpdateButton(existing, itemId);
        }

        private static void RefreshUpdateButton(Button btn, string itemId)
        {
            btn.SetEnabled(true);
            updateState.TryGetValue(itemId, out var info);

            if (info == null)
            {
                btn.text = "Check for Update";
                btn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                btn.style.color = Color.white;
                btn.style.width = 130;
                btn.clickable = new Clickable(() => CheckOneAndRefresh(itemId, btn));
                return;
            }

            if (info.QueryFailed)
            {
                btn.text = "Retry Check";
                btn.style.backgroundColor = new StyleColor(new Color(0.4f, 0.2f, 0.2f));
                btn.style.color = Color.white;
                btn.style.width = 110;
                btn.tooltip = info.Error ?? "Query failed";
                btn.clickable = new Clickable(() => CheckOneAndRefresh(itemId, btn));
                return;
            }

            if (info.UpdateAvailable)
            {
                btn.text = "Update";
                btn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.2f));
                btn.style.color = Color.white;
                btn.style.width = 90;
                btn.tooltip = $"Server: {FormatTimestamp(info.ServerTimestamp)}\nLocal:  {FormatTimestamp(info.LocalTimestamp)}";
                btn.clickable = new Clickable(() => DownloadAndRefresh(itemId, btn));
                return;
            }

            btn.text = "Up to date";
            btn.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
            btn.style.color = new Color(0.7f, 0.7f, 0.7f);
            btn.style.width = StyleKeyword.Auto;
            btn.tooltip = $"Server: {FormatTimestamp(info.ServerTimestamp)}\nLocal:  {FormatTimestamp(info.LocalTimestamp)}";
            btn.clickable = new Clickable(() => CheckOneAndRefresh(itemId, btn));
        }

        private static void CheckOneAndRefresh(string itemId, Button btn)
        {
            btn.text = "Checking...";
            btn.SetEnabled(false);
            WorkshopUpdateChecker.CheckOne(itemId, info =>
            {
                updateState[itemId] = info;
                RefreshUpdateButton(btn, itemId);
            });
        }

        private static void DownloadAndRefresh(string itemId, Button btn)
        {
            btn.text = "Downloading...";
            btn.SetEnabled(false);
            WorkshopUpdateChecker.TriggerDownload(itemId, (ok, err) =>
            {
                if (ok)
                {
                    MarkModListChanged();
                    string title = GetTitleForId(itemId) ?? $"mod {itemId}";
                    // Re-query so the local timestamp updates and the button flips
                    // to "Up to date".
                    WorkshopUpdateChecker.CheckOne(itemId, info =>
                    {
                        updateState[itemId] = info;
                        RefreshUpdateButton(btn, itemId);
                    });
                    MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                        "Mod Updated", $"Downloaded latest for {title}. Restart the game to apply.", 5f);
                }
                else
                {
                    updateState[itemId] = new WorkshopUpdateChecker.UpdateInfo
                    {
                        ItemId = itemId, QueryFailed = true, Error = err
                    };
                    RefreshUpdateButton(btn, itemId);
                    MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                        "Update Failed", err ?? "Unknown error", 5f);
                }
            });
        }

        private static void RunBatchUpdateCheck()
        {
            if (batchCheckInProgress) return;
            batchCheckInProgress = true;
            batchCheckBtn.SetEnabled(false);
            batchCheckBtn.text = "CHECKING...";
            if (batchStatusLabel != null) batchStatusLabel.text = "Querying Steam...";

            WorkshopUpdateChecker.CheckAll(results =>
            {
                batchCheckInProgress = false;
                batchCheckBtn.SetEnabled(true);
                batchCheckBtn.text = "CHECK FOR UPDATES";

                int updates = 0, failed = 0;
                foreach (var info in results)
                {
                    updateState[info.ItemId] = info;
                    if (info.QueryFailed) failed++;
                    else if (info.UpdateAvailable) updates++;
                }

                if (updatesBanner != null && updatesBannerLabel != null)
                {
                    if (updates > 0)
                    {
                        updatesBannerLabel.text = failed > 0
                            ? $"<b>{updates}</b> update{(updates == 1 ? "" : "s")} available  ·  {failed} check{(failed == 1 ? "" : "s")} failed"
                            : $"<b>{updates}</b> update{(updates == 1 ? "" : "s")} available";
                        updatesBanner.style.display = DisplayStyle.Flex;
                    }
                    else
                    {
                        updatesBanner.style.display = DisplayStyle.None;
                    }
                }

                // Refresh every visible row's button to reflect the new state.
                foreach (var kvp in allEntries)
                {
                    if (IsLocalPlugin(kvp.Key)) continue;
                    string id = GetId(kvp.Key);
                    var bottomRow = kvp.Value.Q<VisualElement>("trl-bottom-row");
                    var btn = bottomRow?.Q<Button>(UpdateBtnName);
                    if (btn != null) RefreshUpdateButton(btn, id);
                }

                MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                    "Update Check",
                    updates > 0
                        ? $"{updates} mod{(updates == 1 ? " has" : "s have")} updates available"
                        : (failed > 0 ? $"{failed} check{(failed == 1 ? "" : "s")} failed" : "All mods up to date"),
                    4f);
            });
        }

        private static string GetTitleForId(string itemId)
        {
            var mod = ModManager.GetModById(itemId);
            string title = mod?.SteamWorkshopItem?.Details?.Title;
            if (!string.IsNullOrEmpty(title)) return title;
            if (updateState.TryGetValue(itemId, out var info) && !string.IsNullOrEmpty(info.Title))
                return info.Title;
            return null;
        }

        private static string FormatTimestamp(uint unixSeconds)
        {
            if (unixSeconds == 0) return "(not installed)";
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                .LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }

        private static Label CreateBadge(string name, string text, Color textColor, Color bgColor)
        {
            var badge = new Label(text);
            badge.name = name;
            badge.style.fontSize = 11;
            badge.style.minHeight = 22;
            badge.style.color = textColor;
            badge.style.backgroundColor = new StyleColor(bgColor);
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 3;
            badge.style.paddingBottom = 3;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.marginRight = 4;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.flexShrink = 0;
            return badge;
        }
    }
}
