using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.swappers
{
    public static class ModMenuEnhancer
    {
        private static readonly FieldInfo _modsListField = typeof(UIMods)
            .GetField("modsList", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _modVisualElementMapField = typeof(UIMods)
            .GetField("modVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo _modsField = typeof(UIMods)
            .GetField("mods", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool eventsRegistered;

        public static void RegisterEvents()
        {
            if (eventsRegistered) return;
            eventsRegistered = true;

            EventManager.AddEventListener("Event_OnModEnableFailed",
                new Action<Dictionary<string, object>>(OnModEnableFailed));
        }

        private static void OnModEnableFailed(Dictionary<string, object> message)
        {
            var mod = (Mod)message["mod"];
            string name = mod.InstalledItem?.ItemDetails?.Title
                ?? mod.InstalledItem?.Id.ToString() ?? "Unknown mod";
            MonoBehaviourSingleton<UIManager>.Instance?.ToastManager?.ShowToast(
                "Mod Error", $"{name} failed to load!", 5f);
        }

        private static TextField searchField;
        private static string activeFilter = "enabled"; // "enabled", "all", "plugins", "resourcepacks"
        private static bool sortAlphabetical = true;
        private static UIMods currentUIMods;
        private static bool controlsInjected;

        private static Button enabledTab, allTab, pluginsTab, resourceTab;
        private static Button sortButton;

        // Snapshot of all mod→element pairs from the map.
        // We physically remove/re-add elements from the modsList to filter and sort,
        // since ChildClassifier/ScrollView ignore display:none on children.
        private static readonly List<KeyValuePair<Mod, VisualElement>> allEntries = new();

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
            sortAlphabetical = true;

            // ── Search field — in header, before close button ──
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;
            searchContainer.style.flexGrow = 1;
            searchContainer.style.justifyContent = Justify.FlexEnd;
            searchContainer.style.marginRight = 8;

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

            sortButton = new Button { text = "A-Z" };
            sortButton.style.fontSize = 13;
            sortButton.style.width = 80;
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
                sortAlphabetical = !sortAlphabetical;
                sortButton.text = sortAlphabetical ? "A-Z" : "Recent";
                ApplyFilters();
            };
            tabRow.Add(sortButton);

            int scrollIdx = content.IndexOf(scrollView);
            if (scrollIdx >= 0)
                content.Insert(scrollIdx, tabRow);

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

            Plugin.Log("[ModMenuEnhancer] Controls injected");
        }

        private static Button CreateFooterButton(string text, Action onClick)
        {
            var btn = new Button { text = text };
            btn.AddToClassList("button");
            btn.style.fontSize = 18;
            btn.style.paddingLeft = 20;
            btn.style.paddingRight = 20;
            btn.style.paddingTop = 12;
            btn.style.paddingBottom = 12;
            btn.style.marginRight = 8;
            btn.style.maxWidth = 130;
            btn.style.borderTopLeftRadius = 0;
            btn.style.borderTopRightRadius = 0;
            btn.style.borderBottomLeftRadius = 0;
            btn.style.borderBottomRightRadius = 0;

            btn.clicked += onClick;
            return btn;
        }

        // ── Patch: Show — inject controls, snapshot entries, reset state ──

        [HarmonyPatch(typeof(UIMods), nameof(UIMods.Show))]
        public static class UIModsShowPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIMods __instance, bool __result)
            {
                if (!__result) return;
                EnsureControlsInjected(__instance);
                if (searchField != null) searchField.value = "";
                activeFilter = "enabled";
                sortAlphabetical = true;
                if (sortButton != null) sortButton.text = "A-Z";
                UpdateTabVisuals();

                // Snapshot all entries from the map
                var map = _modVisualElementMapField?.GetValue(__instance) as Dictionary<Mod, VisualElement>;
                if (map != null)
                {
                    allEntries.Clear();
                    foreach (var kvp in map)
                    {
                        allEntries.Add(kvp);
                        UIModsUpdateModPatch.ApplyEnhancements(__instance, kvp.Key);
                    }
                }

                UpdateCounts();
                ApplyFilters();
            }
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
        }

        private static void UpdateCounts()
        {
            int enabledCount = 0, totalAssembly = 0, totalResource = 0;
            foreach (var kvp in allEntries)
            {
                if (kvp.Key.IsAssemblyMod)
                {
                    totalAssembly++;
                    if (kvp.Key.IsEnabled) enabledCount++;
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
            if (currentUIMods == null) return;

            var modsList = _modsListField?.GetValue(currentUIMods) as VisualElement;
            if (modsList == null) return;

            string search = searchField?.value?.ToLowerInvariant() ?? "";

            // Determine which entries pass the filter
            var visible = new List<KeyValuePair<Mod, VisualElement>>();
            foreach (var kvp in allEntries)
            {
                var mod = kvp.Key;

                bool matchesTab = activeFilter == "all"
                    || (activeFilter == "enabled" && mod.IsAssemblyMod && mod.IsEnabled)
                    || (activeFilter == "plugins" && mod.IsAssemblyMod)
                    || (activeFilter == "resourcepacks" && !mod.IsAssemblyMod);

                string title = mod.InstalledItem?.ItemDetails?.Title
                    ?? mod.InstalledItem?.Id.ToString() ?? "";
                bool matchesSearch = string.IsNullOrEmpty(search)
                    || title.ToLowerInvariant().Contains(search);

                if (matchesTab && matchesSearch)
                    visible.Add(kvp);
            }

            // Sort
            if (sortAlphabetical)
            {
                visible = visible.OrderBy(kvp =>
                    kvp.Key.InstalledItem?.ItemDetails?.Title
                    ?? kvp.Key.InstalledItem?.Id.ToString() ?? "").ToList();
            }

            // Rebuild the list: clear and re-add only visible elements in order
            // First, detach all
            foreach (var kvp in allEntries)
                kvp.Value.RemoveFromHierarchy();

            // Then add visible ones in the desired order
            foreach (var kvp in visible)
                modsList.Add(kvp.Value);
        }

        // ── Patch: UpdateMod — badges, graying, workshop button ─────

        [HarmonyPatch(typeof(UIMods), "UpdateMod")]
        public static class UIModsUpdateModPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIMods __instance, Mod mod)
            {
                ApplyEnhancements(__instance, mod);
                UpdateCounts();
            }

            public static void ApplyEnhancements(UIMods instance, Mod mod)
            {
                var map = _modVisualElementMapField?.GetValue(instance) as Dictionary<Mod, VisualElement>;
                if (map == null || !map.ContainsKey(mod)) return;

                var element = map[mod];
                var title = element.Q<Label>("TitleLabel");
                var desc = element.Q<Label>("DescriptionLabel");
                var preview = element.Q<VisualElement>("Preview");

                float opacity = (!mod.IsAssemblyMod || mod.IsEnabled) ? 1f : 0.3f;
                if (title != null) title.style.opacity = opacity;
                if (desc != null) desc.style.opacity = opacity;
                if (preview != null) preview.style.opacity = opacity;

                // ── Bottom row: Workshop button + badges side by side ──
                const string bottomRowName = "trl-bottom-row";
                var bottomRow = element.Q<VisualElement>(bottomRowName);
                if (bottomRow == null && desc?.parent != null)
                {
                    bottomRow = new VisualElement();
                    bottomRow.name = bottomRowName;
                    bottomRow.style.flexDirection = FlexDirection.Row;
                    bottomRow.style.alignItems = Align.Center;
                    bottomRow.style.marginTop = 6;

                    int descIdx = desc.parent.IndexOf(desc);
                    desc.parent.Insert(descIdx + 1, bottomRow);
                }
                if (bottomRow == null) return;

                // Action button: "Open on Workshop" for workshop mods, "Open Folder" for local mods
                const string actionBtnName = "trl-action-btn";
                if (bottomRow.Q<Button>(actionBtnName) == null)
                {
                    string btnText = mod.IsPlugin ? "Open Folder" : "Open on Workshop";
                    var actionBtn = new Button { text = btnText };
                    actionBtn.name = actionBtnName;
                    actionBtn.style.fontSize = 12;
                    actionBtn.style.width = 130;
                    actionBtn.style.height = 35;
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

                    if (mod.IsPlugin)
                    {
                        string modPath = mod.InstalledItem.Path;
                        actionBtn.clicked += () => Application.OpenURL($"file://{modPath}");
                    }
                    else
                    {
                        ulong workshopId = mod.InstalledItem.Id;
                        actionBtn.clicked += () =>
                            Application.OpenURL($"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}");
                    }

                    bottomRow.Insert(0, actionBtn);
                }

                // ── Badges (to the right of the button, with 16px gap) ──
                const string badgeContainerName = "trl-badge-container";
                var badgeContainer = bottomRow.Q<VisualElement>(badgeContainerName);
                if (badgeContainer == null)
                {
                    badgeContainer = new VisualElement();
                    badgeContainer.name = badgeContainerName;
                    badgeContainer.style.flexDirection = FlexDirection.Row;
                    badgeContainer.style.flexWrap = Wrap.Wrap;
                    badgeContainer.style.alignItems = Align.Center;
                    badgeContainer.style.marginLeft = 16;
                    bottomRow.Add(badgeContainer);
                }

                if (!mod.IsAssemblyMod && badgeContainer.Q<Label>("trl-rp-badge") == null)
                    badgeContainer.Add(CreateBadge("trl-rp-badge", "Resource Pack",
                        new Color(0.6f, 0.8f, 1f), new Color(0.2f, 0.3f, 0.4f, 0.6f)));

                if (badgeContainer.Q<Label>("trl-source-badge") == null)
                {
                    if (mod.IsPlugin)
                        badgeContainer.Add(CreateBadge("trl-source-badge", "Local",
                            new Color(0.7f, 0.7f, 0.7f), new Color(0.3f, 0.3f, 0.3f, 0.6f)));
                    else
                        badgeContainer.Add(CreateBadge("trl-source-badge", "Workshop",
                            new Color(0.6f, 1f, 0.6f), new Color(0.2f, 0.4f, 0.2f, 0.6f)));
                }
            }
        }

        private static Label CreateBadge(string name, string text, Color textColor, Color bgColor)
        {
            var badge = new Label(text);
            badge.name = name;
            badge.style.fontSize = 11;
            badge.style.color = textColor;
            badge.style.backgroundColor = new StyleColor(bgColor);
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.marginRight = 4;
            return badge;
        }
    }
}
