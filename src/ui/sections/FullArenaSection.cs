using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEngine;

namespace ToasterReskinLoader.ui.sections
{
    public static class FullArenaSection
    {
        private static Label statusLabel;
        private static TextField bundleField;
        private static TextField prefabField;
        private static TextField workshopField;
        
        public static void CreateSection(VisualElement container)
        {
            Plugin.LogDebug("Creation of a full arena replacement section");

            // Check if SceneryChanger.dll is loaded, show banner if missing
            if (!swappers.FullArenaSwapper.isInitialized)
            {
                CreateMissingDependencyBanner(container);
                return;
            }

            // container.Clear();

            // Title
            // Label header = new Label("COMPLETE REPLACEMENT OF THE ARENA");
            // header.style.fontSize = 26;
            // header.style.color = new Color(1f, 1f, 1f);
            // header.style.marginBottom = 20;
            // container.Add(header);

            // Tabs
            VisualElement tabsRow = new VisualElement();
            tabsRow.style.flexDirection = FlexDirection.Row;
            tabsRow.style.marginBottom = 20;
            
            Button tabAuto = CreateTabButton("AUTO-SEARCH", true);
            Button tabManual = CreateTabButton("MANUAL INPUT", false);
            
            tabsRow.Add(tabAuto);
            tabsRow.Add(tabManual);
            container.Add(tabsRow);
            
            // Containers
            VisualElement autoContainer = CreateAutoContainer();
            VisualElement manualContainer = CreateManualContainer();
            
            manualContainer.style.display = DisplayStyle.None;
            
            container.Add(autoContainer);
            container.Add(manualContainer);
            
            // Tab handlers
            tabAuto.clicked += () => 
            {
                autoContainer.style.display = DisplayStyle.Flex;
                manualContainer.style.display = DisplayStyle.None;
            };
            
            tabManual.clicked += () => 
            {
                autoContainer.style.display = DisplayStyle.None;
                manualContainer.style.display = DisplayStyle.Flex;
            };
            
            // Warning
            Label warning = new Label("Attention: It works the same way in practice mode, and on all servers!");
            warning.style.fontSize = 12;
            warning.style.color = Color.yellow;
            warning.style.marginTop = 20;
            container.Add(warning);
            
            // Status
            statusLabel = new Label("Ready to download");
            statusLabel.style.fontSize = 14;
            statusLabel.style.color = Color.green;
            statusLabel.style.marginTop = 15;
            container.Add(statusLabel);
            
            // Control buttons
            CreateControlButtons(container);
        }
        
        private static Button CreateTabButton(string text, bool active)
        {
            Button tab = new Button();
            tab.text = text;
            tab.style.flexGrow = 1;
            tab.style.height = 40;
            tab.style.marginRight = 5;
            tab.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            tab.style.backgroundColor = active ? 
                new StyleColor(new Color(0.3f, 0.5f, 0.8f)) : 
                new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            
            return tab;
        }
        
        private static VisualElement CreateAutoContainer()
        {
            VisualElement container = new VisualElement();
            
            Label title = new Label("Available Arenas:");
            title.style.fontSize = 16;
            title.style.color = Color.white;
            title.style.marginBottom = 10;
            container.Add(title);
            
            var arenas = swappers.FullArenaSwapper.GetAvailableArenas();
            
            if (arenas.Count == 0)
            {
                Label noArenas = new Label("No arenas were found.\nUse manual input.");
                noArenas.style.color = Color.yellow;
                noArenas.style.whiteSpace = WhiteSpace.Normal;
                container.Add(noArenas);
            }
            else
            {
                ScrollView scroll = new ScrollView();
                scroll.style.maxHeight = 250;
                
                foreach (var arena in arenas)
                {
                    Button arenaBtn = new Button(() => SelectArena(arena));
                    arenaBtn.text = $"{arena.Name}\nPrefab: {arena.PrefabName}" +
                                  (string.IsNullOrEmpty(arena.WorkshopId) ? "" : $"\nWorkshop: {arena.WorkshopId}");
                    arenaBtn.style.height = 70;
                    arenaBtn.style.marginBottom = 5;
                    arenaBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                    arenaBtn.style.paddingLeft = 10;
                    arenaBtn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                    arenaBtn.style.whiteSpace = WhiteSpace.Normal;
                    
                    scroll.Add(arenaBtn);
                }
                
                container.Add(scroll);
            }
            
            return container;
        }
        
        private static VisualElement CreateManualContainer()
        {
            VisualElement container = new VisualElement();
            
            Label title = new Label("Manual input of parameters:");
            title.style.fontSize = 16;
            title.style.color = Color.white;
            title.style.marginBottom = 15;
            container.Add(title);
            
            // Bundle Name
            VisualElement bundleRow = UITools.CreateConfigurationRow();
            bundleRow.Add(UITools.CreateConfigurationLabel("Name Bundle:"));
            bundleField = new TextField();
            bundleField.value = ReskinProfileManager.currentProfile.fullArenaBundle ?? "outdoorhockey";
            bundleRow.Add(bundleField);
            container.Add(bundleRow);
            
            // Prefab Name
            VisualElement prefabRow = UITools.CreateConfigurationRow();
            prefabRow.Add(UITools.CreateConfigurationLabel("Name Prefab:"));
            prefabField = new TextField();
            prefabField.value = ReskinProfileManager.currentProfile.fullArenaPrefab ?? "OutdoorHockey";
            prefabRow.Add(prefabField);
            container.Add(prefabRow);
            
            // Workshop ID
            VisualElement workshopRow = UITools.CreateConfigurationRow();
            workshopRow.Add(UITools.CreateConfigurationLabel("Workshop ID (optional):"));
            workshopField = new TextField();
            workshopField.value = ReskinProfileManager.currentProfile.fullArenaWorkshopId ?? "";
            workshopRow.Add(workshopField);
            container.Add(workshopRow);
            
            // Examples
            Label examples = new Label("Examples:\noutdoorhockey / OutdoorHockey / 3566470321");
            examples.style.fontSize = 11;
            examples.style.color = new Color(0.7f, 0.7f, 0.7f);
            examples.style.marginTop = 10;
            examples.style.whiteSpace = WhiteSpace.Normal;
            container.Add(examples);
            
            return container;
        }
        
        private static void SelectArena(swappers.FullArenaSwapper.ArenaInfo arena)
        {
            ReskinProfileManager.currentProfile.fullArenaBundle = arena.Name;
            ReskinProfileManager.currentProfile.fullArenaPrefab = arena.PrefabName;
            ReskinProfileManager.currentProfile.fullArenaWorkshopId = arena.WorkshopId ?? "";
            ReskinProfileManager.SaveProfile();
            
            UpdateStatus($"Selected: {arena.Name} ({arena.PrefabName})", new Color(0.2f, 0.8f, 0.2f));
            
            // Updating fields manually
            if (bundleField != null) bundleField.value = arena.Name;
            if (prefabField != null) prefabField.value = arena.PrefabName;
            if (workshopField != null && !string.IsNullOrEmpty(arena.WorkshopId)) 
                workshopField.value = arena.WorkshopId;
        }
        
        private static void CreateControlButtons(VisualElement container)
        {
            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.SpaceBetween;
            buttonRow.style.marginTop = 20;
            
            // Download button
            Button loadButton = new Button(() =>
            {
                string bundle = bundleField?.value?.Trim() ?? "";
                string prefab = prefabField?.value?.Trim() ?? "";
                string workshop = workshopField?.value?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(bundle))
                {
                    UpdateStatus("Enter the name of the Bundle!", Color.red);
                    return;
                }
                
                if (string.IsNullOrEmpty(prefab))
                {
                    prefab = "OutdoorHockey";
                }
                
                UpdateStatus("Loading the arena...", Color.yellow);
                
                if (string.IsNullOrEmpty(workshop))
                {
                    swappers.FullArenaSwapper.LoadArena(bundle, prefab);
                }
                else
                {
                    swappers.FullArenaSwapper.LoadArena(bundle, prefab, workshop);
                }
            });
            
            loadButton.text = "download the arena";
            loadButton.style.flexGrow = 1;
            loadButton.style.height = 45;
            loadButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.2f));
            loadButton.style.marginRight = 10;
            
            // Reset button
            Button resetButton = new Button(() =>
            {
                swappers.FullArenaSwapper.DisableFullArena();
                UpdateStatus("standard arena", Color.gray);
                
                // Resetting the fields
                if (bundleField != null) bundleField.value = "outdoorhockey";
                if (prefabField != null) prefabField.value = "OutdoorHockey";
                if (workshopField != null) workshopField.value = "";
            });
            
            resetButton.text = "reset";
            resetButton.style.width = 100;
            resetButton.style.height = 45;
            resetButton.style.backgroundColor = new StyleColor(new Color(0.5f, 0.2f, 0.2f));
            
            buttonRow.Add(loadButton);
            buttonRow.Add(resetButton);
            container.Add(buttonRow);
        }
        
        private static void UpdateStatus(string message, Color color)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
                statusLabel.style.color = color;
            }
        }

        /// <summary>
        /// Creates a banner informing the user that SceneryChanger.dll is required.
        /// Shows a clickable link to the Steam Workshop to install it.
        /// </summary>
        private static void CreateMissingDependencyBanner(VisualElement container)
        {
            // Outer banner container
            VisualElement bannerContainer = new VisualElement();
            bannerContainer.style.backgroundColor = new StyleColor(new Color(0.4f, 0.2f, 0.2f)); // Dark red
            bannerContainer.style.borderTopWidth = 2;
            bannerContainer.style.borderBottomWidth = 2;
            bannerContainer.style.borderLeftWidth = 2;
            bannerContainer.style.borderRightWidth = 2;
            bannerContainer.style.borderTopColor = new StyleColor(new Color(1f, 0.3f, 0.3f)); // Bright red
            bannerContainer.style.borderBottomColor = new StyleColor(new Color(1f, 0.3f, 0.3f));
            bannerContainer.style.borderLeftColor = new StyleColor(new Color(1f, 0.3f, 0.3f));
            bannerContainer.style.borderRightColor = new StyleColor(new Color(1f, 0.3f, 0.3f));
            bannerContainer.style.paddingLeft = 20;
            bannerContainer.style.paddingRight = 20;
            bannerContainer.style.paddingTop = 20;
            bannerContainer.style.paddingBottom = 20;
            bannerContainer.style.marginBottom = 30;
            bannerContainer.style.marginTop = 10;

            // Title
            Label title = new Label("Missing Required Dependency Mod");
            title.style.fontSize = 20;
            title.style.color = new Color(1f, 0.5f, 0.5f);
            title.style.marginBottom = 10;
            bannerContainer.Add(title);

            // Description
            Label description = new Label("You need to subscribe to <b>Dem's Scenery Loader</b> to use Full Arena Replacement!");
            description.style.fontSize = 14;
            description.style.color = Color.white;
            description.style.marginBottom = 15;
            description.style.whiteSpace = WhiteSpace.Normal;
            bannerContainer.Add(description);

            // Clickable link button
            Button workshopLink = new Button(() =>
            {
                const string workshopUrl = "https://steamcommunity.com/sharedfiles/filedetails/?id=3566470321";
                // Open in Steam overlay using steam:// protocol
                string steamOverlayUrl = $"steam://openurl/{workshopUrl}";
                UnityEngine.Application.OpenURL(steamOverlayUrl);
                Plugin.Log($"[FullArenaSection] Opened Steam Workshop link in overlay: {workshopUrl}");
            });
            workshopLink.text = "Click here to install from Steam Workshop";
            workshopLink.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.8f)); // Blue
            workshopLink.style.color = Color.white;
            workshopLink.style.fontSize = 13;
            workshopLink.style.paddingLeft = 15;
            workshopLink.style.paddingRight = 15;
            workshopLink.style.paddingTop = 10;
            workshopLink.style.paddingBottom = 10;
            workshopLink.style.height = 40;
            workshopLink.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Hover effect
            workshopLink.RegisterCallback<MouseEnterEvent>(evt =>
            {
                workshopLink.style.backgroundColor = new StyleColor(new Color(0.3f, 0.6f, 1f)); // Lighter blue
            });
            workshopLink.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                workshopLink.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.8f)); // Back to blue
            });

            bannerContainer.Add(workshopLink);
            // Description
            Label description2 = new Label("You will need to <b>restart your game</b> after subscribing.");
            description2.style.fontSize = 14;
            description2.style.color = Color.white;
            description2.style.marginBottom = 15;
            description2.style.whiteSpace = WhiteSpace.Normal;
            bannerContainer.Add(description2);

            container.Add(bannerContainer);
        }
    }
}