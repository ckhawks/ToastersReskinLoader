using ToasterReskinLoader.swappers;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ToasterReskinLoader.ui.sections;

public static class AboutSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        Label description = new Label();
        description.text = $"Version: {Plugin.MOD_VERSION}<br><br>This mod was made by <b>Toaster (Stellaric)</b>, with contributions from Banix.\n\nIf you need support or have questions about the mod, you can join the Toaster's Rink Discord.";
        description.style.fontSize = 14;
        description.style.whiteSpace = WhiteSpace.Normal;
        
        Button discordButton = new Button
        {
            text = "<b>Toaster's Rink Discord</b>\n<size=14>(opens in browser)</size>",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                width = new StyleLength(new Length(100, LengthUnit.Percent)),
                minWidth = new StyleLength(new Length(100, LengthUnit.Percent)),
                maxWidth = new StyleLength(new Length(100, LengthUnit.Percent)),
                height = 80,
                minHeight = 80,
                maxHeight = 80,
                marginTop = 8,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15
            }
        };
        UITools.AddHoverEffectsForButton(discordButton);
        discordButton.RegisterCallback<ClickEvent>(DiscordButtonClickHandler);
        
        void DiscordButtonClickHandler(ClickEvent evt)
        {
            Application.OpenURL("https://discord.gg/mCmX5dwzsj");
        }

        contentScrollViewContent.Add(description);
        contentScrollViewContent.Add(discordButton);
        
        Label description2 = new Label();
        description2.text = "<br>This mod took a lot of time to make -- if you enjoy my work and you'd like to support the development of this and other mods, please consider donating to my Ko-fi.";
        description2.style.fontSize = 14;
        description2.style.whiteSpace = WhiteSpace.Normal;
        
        Button kofiButton = new Button
        {
            text = "<b>Toaster's Ko-fi</b>\n<size=14>(opens in browser)</size>",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleLeft,
                width = new StyleLength(new Length(100, LengthUnit.Percent)),
                minWidth = new StyleLength(new Length(100, LengthUnit.Percent)),
                maxWidth = new StyleLength(new Length(100, LengthUnit.Percent)),
                height = 80,
                minHeight = 80,
                maxHeight = 80,
                marginTop = 8,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15
            }
        };
        UITools.AddHoverEffectsForButton(kofiButton);
        kofiButton.RegisterCallback<ClickEvent>(KofiButtonClickHandler);

        contentScrollViewContent.Add(description2);
        contentScrollViewContent.Add(kofiButton);

        void KofiButtonClickHandler(ClickEvent evt)
        {
            Application.OpenURL("http://ko-fi.com/stellaric");
        }

        // Fun settings
        Label funLabel = new Label();
        funLabel.text = "<br><b>Fun Settings</b>";
        funLabel.style.fontSize = 16;
        funLabel.style.color = Color.white;
        funLabel.style.marginTop = 12;
        funLabel.style.marginBottom = 4;
        contentScrollViewContent.Add(funLabel);

        VisualElement bigHeadRow = UITools.CreateConfigurationRow();
        bigHeadRow.Add(UITools.CreateConfigurationLabel("Big Heads"));
        Toggle bigHeadToggle = UITools.CreateConfigurationCheckbox(Plugin.modSettings.BigHeadsEnabled);
        bigHeadToggle.RegisterCallback<ChangeEvent<bool>>(evt =>
        {
            Plugin.modSettings.BigHeadsEnabled = evt.newValue;
            Plugin.modSettings.Save();
            if (evt.newValue)
                HatSwapper.ResetHeadScales();
            else
                HatSwapper.ResetHeadScales();
        });
        bigHeadRow.Add(bigHeadToggle);
        contentScrollViewContent.Add(bigHeadRow);

        // Random skin tone button
        VisualElement skinToneRow = UITools.CreateConfigurationRow();
        skinToneRow.Add(UITools.CreateConfigurationLabel("Random Skin Tones"));
        Button skinToneButton = new Button
        {
            text = "Randomize",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                paddingLeft = 8,
                paddingRight = 8,
                paddingTop = 8,
                paddingBottom = 8
            }
        };
        UITools.AddHoverEffectsForButton(skinToneButton);
        skinToneButton.RegisterCallback<ClickEvent>(evt =>
        {
            RandomizeSkinTones();
        });
        skinToneRow.Add(skinToneButton);
        contentScrollViewContent.Add(skinToneRow);
    }

    private static void RandomizeSkinTones()
    {
        var random = new System.Random();
        var players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player?.PlayerBody?.PlayerMesh?.PlayerHead == null) continue;

            var renderers = player.PlayerBody.PlayerMesh.PlayerHead.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.name == "Head")
                {
                    Color randomColor = new Color(
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        (float)random.NextDouble(),
                        1f);
                    renderer.material.color = randomColor;
                    if (renderer.material.HasProperty("_BaseColor"))
                        renderer.material.SetColor("_BaseColor", randomColor);
                }
            }
        }

        // Also apply to locker room preview
        if (ChangingRoomHelper.IsInMainMenu())
        {
            var playerMesh = ChangingRoomHelper.GetPlayerMesh();
            if (playerMesh?.PlayerHead != null)
            {
                var renderers = playerMesh.PlayerHead.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    if (renderer.name == "Head")
                    {
                        Color randomColor = new Color(
                            (float)random.NextDouble(),
                            (float)random.NextDouble(),
                            (float)random.NextDouble(),
                            1f);
                        renderer.material.color = randomColor;
                        if (renderer.material.HasProperty("_BaseColor"))
                            renderer.material.SetColor("_BaseColor", randomColor);
                    }
                }
            }
        }
    }
    
}