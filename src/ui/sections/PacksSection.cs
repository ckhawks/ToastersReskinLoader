using Steamworks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class PacksSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        ChangingRoomHelper.ShowBaseFocus();
        
        
        contentScrollViewContent.Clear(); // discard existing content
        VisualElement sectionTitleGroup = new VisualElement();
        VisualElement titleRow = new VisualElement();
        // titleRow.style.alignItems = Align.Center;
        titleRow.style.flexDirection = FlexDirection.Row;
        titleRow.style.justifyContent = Justify.SpaceBetween;
        sectionTitleGroup.style.flexDirection = FlexDirection.Column;
        Label contentSectionTitle = new Label("Packs");
        contentSectionTitle.style.fontSize = 30;
        contentSectionTitle.style.color = Color.white;
        sectionTitleGroup.Add(contentSectionTitle);
        Label packsNumberLabel =
            UITools.CreateConfigurationLabel($"{ReskinRegistry.reskinPacks.Count} pack{(ReskinRegistry.reskinPacks.Count == 1 ? "" : "s")} loaded");
        
        packsNumberLabel.style.marginBottom = 16;
        packsNumberLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        sectionTitleGroup.Add(packsNumberLabel);
        
        
        Button findPacksButton = new Button
        {
            text = "<size=20>Find Reskin Packs</size>",
            style =
            {
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                unityTextAlign = TextAnchor.MiddleCenter,
                // width = new StyleLength(new Length(100, LengthUnit.Percent)),
                // minWidth = new StyleLength(new Length(100, LengthUnit.Percent)),
                // maxWidth = new StyleLength(new Length(100, LengthUnit.Percent)),
                width = 300,
                // width = referenceButton.style.width,
                // minWidth = referenceButton.style.minWidth,
                // maxWidth = referenceButton.style.maxWidth,
                height = 40,
                minHeight = 40,
                maxHeight = 40,
                marginTop = 16,
                paddingTop = 8,
                paddingBottom = 8,
                paddingLeft = 15
            }
        };
        UITools.AddHoverEffectsForButton(findPacksButton);
        findPacksButton.RegisterCallback<ClickEvent>(FindPacksButtonClickHandler);
        
        void FindPacksButtonClickHandler(ClickEvent evt)
        {
            SteamFriends.ActivateGameOverlayToWebPage("https://steamcommunity.com/workshop/browse/?appid=2994020&requiredtags[]=Resource+Pack", EActivateGameOverlayToWebPageMode.k_EActivateGameOverlayToWebPageMode_Default);
        }
        titleRow.Add(sectionTitleGroup);
        titleRow.Add(findPacksButton);
        contentScrollViewContent.Add(titleRow);
        
        // https://steamcommunity.com/workshop/browse/?appid=2994020&requiredtags[]=Resource+Pack
        
            
        // For each loaded pack,
        foreach (var pack in ReskinRegistry.reskinPacks)
        {
            VisualElement row = UITools.CreateConfigurationRow();
                
            Label packLabel = UITools.CreateConfigurationLabel(pack.Name);
            row.Add(packLabel);
            VisualElement rightSide = new VisualElement();
            rightSide.style.flexDirection = FlexDirection.Row;
            rightSide.style.alignItems = Align.Center;
            // rightSide.style.justifyContent = Justify.SpaceBetween;
            if (pack.WorkshopId != 0)
            {
                // Label workshopLabel = UITools.CreateConfigurationLabel($"Workshop {pack.WorkshopId}");
                // row.Add(workshopLabel);
                Button workshopPackButton = new Button
                {
                    text = "View on Workshop",
                    style =
                    {
                        backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                        unityTextAlign = TextAnchor.MiddleCenter,
                        // width = new StyleLength(new Length(80, LengthUnit.Pixel)),
                        // minWidth = new StyleLength(new Length(80, LengthUnit.Pixel)),
                        // maxWidth = new StyleLength(new Length(80, LengthUnit.Pixel)),
                        fontSize = 10,
                        // height = 24,
                        // minHeight = 24,
                        // maxHeight = 24,
                        // marginTop = 2,
                        paddingTop = 2,
                        paddingBottom = 2,
                        paddingLeft = 8,
                        paddingRight = 8,
                        marginRight = 8,
                    }
                };
                workshopPackButton.RegisterCallback<MouseEnterEvent>(new EventCallback<MouseEnterEvent>((evt) =>
                {
                    workshopPackButton.style.backgroundColor = Color.white;
                    workshopPackButton.style.color = Color.black;
                }));
                workshopPackButton.RegisterCallback<MouseLeaveEvent>(new EventCallback<MouseLeaveEvent>((evt) =>
                {
                    workshopPackButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
                    workshopPackButton.style.color = Color.white;
                }));
                workshopPackButton.RegisterCallback<ClickEvent>(WorkshopPackButtonClickHandler);
        
                void WorkshopPackButtonClickHandler(ClickEvent evt)
                {
                    Application.OpenURL($"https://steamcommunity.com/sharedfiles/filedetails/?id={pack.WorkshopId}");
                }
                
                rightSide.Add(workshopPackButton);
            }
            else
            {
                Label workshopLabel = UITools.CreateConfigurationLabel($"Local pack");
                workshopLabel.style.fontSize = 12;
                workshopLabel.style.marginRight = 12;
                rightSide.Add(workshopLabel);
            }
            Label packDetailsLabel = UITools.CreateConfigurationLabel($"{pack.Reskins.Count} reskin{(pack.Reskins.Count == 1 ? "" : "s")}");
            packDetailsLabel.style.width = 80;
            packDetailsLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            rightSide.Add(packDetailsLabel);
            row.Add(rightSide);
            
            contentScrollViewContent.Add(row);
        }
    }
}