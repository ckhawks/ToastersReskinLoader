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
        private static bool sortAlphabetical = true;
        private static UIMods currentUIMods;
        private static bool controlsInjected;

        private static Button enabledTab, allTab, pluginsTab, resourceTab;
        private static Button sortButton;

        // Snapshot of all entry→element pairs from both maps.
        // Keys are either Mod or Plugin instances.
        private static readonly List<KeyValuePair<object, VisualElement>> allEntries = new();

        // ── Helpers to abstract over Mod vs Plugin ───────────────────

        private static bool IsLocalPlugin(object entry) => entry is global::Plugin;

        private static bool HasAssembly(object entry)
        {
            if (entry is Mod m) return m.HasAssembly;
            if (entry is global::Plugin p) return p.HasAssembly;
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

            ToasterReskinLoader.Plugin.Log("[ModMenuEnhancer] Controls injected");
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

        // [HarmonyPatch(typeof(UIMods), nameof(UIMods.Show))] // disabled for b323
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

                SnapshotEntries(__instance);
                foreach (var kvp in allEntries)
                    ApplyEnhancements(kvp.Key, kvp.Value);

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
            if (currentUIMods == null) return;

            var modsList = _modsListField?.GetValue(currentUIMods) as VisualElement;
            if (modsList == null) return;

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

            if (sortAlphabetical)
                visible = visible.OrderBy(kvp => GetTitle(kvp.Key)).ToList();

            foreach (var kvp in allEntries)
                kvp.Value.RemoveFromHierarchy();

            foreach (var kvp in visible)
                modsList.Add(kvp.Value);
        }

        // ── Patches: UpdateMod / UpdatePlugin ────────────────────────

        // [HarmonyPatch(typeof(UIMods), "UpdateMod")] // disabled for b323
        public static class UIModsUpdateModPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIMods __instance, Mod mod)
            {
                var map = _modTemplateMapField?.GetValue(__instance) as System.Collections.IDictionary;
                if (map == null || !map.Contains(mod)) return;
                if (map[mod] is VisualElement element)
                    ApplyEnhancements(mod, element);
                UpdateCounts();
            }
        }

        // [HarmonyPatch(typeof(UIMods), "UpdatePlugin")] // disabled for b323
        public static class UIModsUpdatePluginPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIMods __instance, global::Plugin plugin)
            {
                var map = _pluginTemplateMapField?.GetValue(__instance) as System.Collections.IDictionary;
                if (map == null || !map.Contains(plugin)) return;
                if (map[plugin] is VisualElement element)
                    ApplyEnhancements(plugin, element);
                UpdateCounts();
            }
        }

        public static void ApplyEnhancements(object entry, VisualElement element)
        {
            var desc = element.Q<Label>("DescriptionLabel");
            var preview = element.Q<VisualElement>("Preview");

            float opacity = (!HasAssembly(entry) || IsEnabled(entry)) ? 1f : 0.3f;
            if (desc != null) desc.style.opacity = opacity;
            if (preview != null) preview.style.opacity = opacity;

            // ── Bottom row: Workshop/Folder button + badges side by side ──
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

            // Action button: "Open on Workshop" for workshop mods, "Open Folder" for local plugins
            const string actionBtnName = "trl-action-btn";
            if (bottomRow.Q<Button>(actionBtnName) == null)
            {
                bool localPlugin = IsLocalPlugin(entry);
                string btnText = localPlugin ? "Open Folder" : "Open on Workshop";
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

            if (!HasAssembly(entry) && badgeContainer.Q<Label>("trl-rp-badge") == null)
                badgeContainer.Add(CreateBadge("trl-rp-badge", "Resource Pack",
                    new Color(0.6f, 0.8f, 1f), new Color(0.2f, 0.3f, 0.4f, 0.6f)));

            if (badgeContainer.Q<Label>("trl-source-badge") == null)
            {
                if (IsLocalPlugin(entry))
                    badgeContainer.Add(CreateBadge("trl-source-badge", "Local",
                        new Color(0.7f, 0.7f, 0.7f), new Color(0.3f, 0.3f, 0.3f, 0.6f)));
                else
                    badgeContainer.Add(CreateBadge("trl-source-badge", "Workshop",
                        new Color(0.6f, 1f, 0.6f), new Color(0.2f, 0.4f, 0.2f, 0.6f)));
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
