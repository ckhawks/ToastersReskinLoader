using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class AboutSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        Label description = new Label();
        description.text = $"Version: {Plugin.MOD_VERSION}<br><br>This mod was made by <b>Toaster (Stellaric)</b>, with contributions from Banix.\n\nIf you need support or have questions about the mod, you can join the Toaster's Rink Discord.";
        description.style.fontSize = 14;
        
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
                // width = referenceButton.style.width,
                // minWidth = referenceButton.style.minWidth,
                // maxWidth = referenceButton.style.maxWidth,
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
                // width = referenceButton.style.width,
                // minWidth = referenceButton.style.minWidth,
                // maxWidth = referenceButton.style.maxWidth,
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
    }
    
}