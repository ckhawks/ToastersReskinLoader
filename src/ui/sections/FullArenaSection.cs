using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityEngine;

namespace ToasterReskinLoader.ui.sections
{
    public static class FullArenaSection
    {
        private static Label statusLabel;

        public static void CreateSection(VisualElement container)
        {
            Plugin.LogDebug("Creation of a full arena replacement section");

            // Check if SceneryChanger.dll is loaded, show banner if missing
            if (!swappers.FullArenaSwapper.isInitialized)
            {
                CreateMissingDependencyBanner(container);
                return;
            }

            // Load arenas from reskinpack.json files
            var arenaEntries = ReskinRegistry.GetReskinEntriesByType("arena");

            if (arenaEntries.Count == 0)
            {
                Label noArenasLabel = new Label("No arenas found in loaded reskin packs.");
                noArenasLabel.style.fontSize = 14;
                noArenasLabel.style.color = Color.yellow;
                noArenasLabel.style.marginTop = 20;
                container.Add(noArenasLabel);
                return;
            }
            
            // Success banner showing Dem's Scenery Loader is loaded
            VisualElement successBanner = new VisualElement();
            successBanner.style.backgroundColor = new StyleColor(new Color(0.2f, 0.4f, 0.2f)); // Dark green
            successBanner.style.borderTopWidth = 1;
            successBanner.style.borderBottomWidth = 1;
            successBanner.style.borderLeftWidth = 1;
            successBanner.style.borderRightWidth = 1;
            successBanner.style.borderTopColor = new StyleColor(new Color(0.5f, 0.9f, 0.5f)); // Bright green
            successBanner.style.borderBottomColor = new StyleColor(new Color(0.5f, 0.9f, 0.5f));
            successBanner.style.borderLeftColor = new StyleColor(new Color(0.5f, 0.9f, 0.5f));
            successBanner.style.borderRightColor = new StyleColor(new Color(0.5f, 0.9f, 0.5f));
            successBanner.style.paddingLeft = 10;
            successBanner.style.paddingRight = 10;
            successBanner.style.paddingTop = 8;
            successBanner.style.paddingBottom = 8;
            successBanner.style.marginBottom = 15;
            
            Label successMsg = new Label("✓ Dem's Scenery Loader is loaded successfully");
            successMsg.style.fontSize = 12;
            successMsg.style.color = new Color(0.6f, 0.9f, 0.6f);
            successBanner.Add(successMsg);
            container.Add(successBanner);

            // Title
            Label title = new Label("SELECT ARENA");
            title.style.fontSize = 22;
            title.style.color = Color.white;
            title.style.marginBottom = 10;
            container.Add(title);
            
            // Description
            Label description = new Label("Choose a custom arena from your installed reskin packs.");
            description.style.fontSize = 12;
            description.style.color = new Color(0.8f, 0.8f, 0.8f);
            description.style.marginBottom = 20;
            container.Add(description);

            // Arena dropdown
            List<string> arenaNames = arenaEntries.Select(e => e.Name).ToList();
            arenaNames.Insert(0, "None (Default)");

            DropdownField arenaDropdown = new DropdownField();
            arenaDropdown.choices = arenaNames;
            arenaDropdown.value = ReskinProfileManager.currentProfile.fullArenaEnabled
                ? ReskinProfileManager.currentProfile.fullArenaBundle
                : "None (Default)";
            arenaDropdown.RegisterValueChangedCallback(evt => OnArenaSelected(arenaEntries, evt.newValue));
            container.Add(arenaDropdown);

            // Warning
            Label warning = new Label("Attention: Full arena replacement works in practice mode and on all servers!");
            warning.style.fontSize = 12;
            warning.style.color = Color.yellow;
            warning.style.marginTop = 20;
            container.Add(warning);

            // Status
            statusLabel = new Label("Ready");
            statusLabel.style.fontSize = 14;
            statusLabel.style.color = Color.green;
            statusLabel.style.marginTop = 15;
            container.Add(statusLabel);
        }
        
        private static void UpdateStatus(string message, Color color)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
                statusLabel.style.color = color;
            }
        }

        // Handles arena selection from dropdown
        private static void OnArenaSelected(List<ReskinRegistry.ReskinEntry> arenaEntries, string selectedName)
        {
            Plugin.LogDebug($"Arena selected: {selectedName}");

            if (selectedName == "None (Default)")
            {
                swappers.FullArenaSwapper.DisableFullArena();
                UpdateStatus("Standard arena", Color.gray);
                return;
            }

            var selectedArena = arenaEntries.FirstOrDefault(e => e.Name == selectedName);
            if (selectedArena == null)
            {
                Plugin.LogError($"Could not find arena: {selectedName}");
                UpdateStatus($"Arena not found: {selectedName}", Color.red);
                return;
            }

            if (string.IsNullOrEmpty(selectedArena.Path) || string.IsNullOrEmpty(selectedArena.PrefabName))
            {
                Plugin.LogError($"Arena {selectedName} missing path or prefabName");
                UpdateStatus($"Invalid arena configuration", Color.red);
                return;
            }

            // Extract bundle name from path (filename without extension)
            string bundleName = System.IO.Path.GetFileNameWithoutExtension(selectedArena.Path);

            UpdateStatus($"Loading arena: {selectedName}...", Color.yellow);
            swappers.FullArenaSwapper.LoadArena(bundleName, selectedArena.PrefabName);
            UpdateStatus($"Loaded: {selectedName}", Color.green);
        }

        // Creates a banner informing the user that SceneryChanger.dll is required
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