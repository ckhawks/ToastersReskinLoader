using System.Collections.Generic;
using System.Linq;
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
            SteamFriends.ActivateGameOverlayToWebPage($"https://steamcommunity.com/workshop/browse/?appid={PathManager.WorkshopAppId}&requiredtags[]=Resource+Pack", EActivateGameOverlayToWebPageMode.k_EActivateGameOverlayToWebPageMode_Default);
        }
        titleRow.Add(sectionTitleGroup);
        titleRow.Add(findPacksButton);
        contentScrollViewContent.Add(titleRow);

        // Surface duplicate unique-ids loudly — two packs sharing one id will
        // silently clobber each other in ReskinProfileManager.
        var dupes = ReskinRegistry.FindDuplicateUniqueIds();
        if (dupes.Count > 0)
        {
            var banner = new VisualElement();
            banner.style.flexDirection = FlexDirection.Column;
            banner.style.marginTop = 8;
            banner.style.marginBottom = 12;
            banner.style.paddingTop = 10;
            banner.style.paddingBottom = 10;
            banner.style.paddingLeft = 14;
            banner.style.paddingRight = 14;
            banner.style.backgroundColor = new StyleColor(new Color(0.45f, 0.15f, 0.1f, 0.85f));
            banner.style.borderTopLeftRadius = 4;
            banner.style.borderTopRightRadius = 4;
            banner.style.borderBottomLeftRadius = 4;
            banner.style.borderBottomRightRadius = 4;

            var header = new Label("<b>Duplicate pack unique-ids detected</b>");
            header.enableRichText = true;
            header.style.color = Color.white;
            header.style.fontSize = 15;
            header.style.marginBottom = 4;
            banner.Add(header);

            var sub = new Label(
                "Packs that share a unique-id will overwrite each other's profile data. "
                + "Ask the pack author to set a distinct unique-id in their reskinpack.json.");
            sub.style.color = new Color(0.95f, 0.85f, 0.8f);
            sub.style.fontSize = 13;
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.marginBottom = 6;
            banner.Add(sub);

            foreach (var (id, packs) in dupes)
            {
                string names = string.Join(", ", packs.ConvertAll(p => $"\"{p.Name}\""));
                var line = new Label($"• <b>{id}</b> — {names}");
                line.enableRichText = true;
                line.style.color = Color.white;
                line.style.fontSize = 13;
                line.style.whiteSpace = WhiteSpace.Normal;
                banner.Add(line);
            }

            contentScrollViewContent.Add(banner);
        }

        // For each loaded pack,
        foreach (var pack in ReskinRegistry.reskinPacks)
        {
            // Wrap the row + its expandable list in a column container so they sit together.
            VisualElement packContainer = new VisualElement();
            packContainer.style.flexDirection = FlexDirection.Column;

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
                    evt.StopPropagation();
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

            // Chevron toggles the inline reskin list. Disabled (and dimmed) when
            // the pack has no reskins so it doesn't look interactive.
            Label chevron = new Label("▸"); // ▸
            chevron.style.color = Color.white;
            chevron.style.fontSize = 16;
            chevron.style.marginLeft = 8;
            chevron.style.width = 18;
            chevron.style.unityTextAlign = TextAnchor.MiddleCenter;
            bool hasReskins = pack.Reskins != null && pack.Reskins.Count > 0;
            if (!hasReskins) chevron.style.opacity = 0.3f;
            rightSide.Add(chevron);

            row.Add(rightSide);
            packContainer.Add(row);

            VisualElement expandedList = BuildReskinsList(pack);
            expandedList.style.display = DisplayStyle.None;
            packContainer.Add(expandedList);

            if (hasReskins)
            {
                // Whole row is clickable to toggle, with a hand cursor hint via background flash.
                row.RegisterCallback<ClickEvent>(evt =>
                {
                    bool nowOpen = expandedList.style.display == DisplayStyle.None;
                    expandedList.style.display = nowOpen ? DisplayStyle.Flex : DisplayStyle.None;
                    chevron.text = nowOpen ? "▾" : "▸"; // ▾ / ▸
                });
            }

            contentScrollViewContent.Add(packContainer);
        }
    }

    private static VisualElement BuildReskinsList(ReskinRegistry.ReskinPack pack)
    {
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.flexWrap = Wrap.Wrap;
        container.style.marginTop = 4;
        container.style.marginBottom = 10;
        container.style.paddingTop = 6;
        container.style.paddingBottom = 6;
        container.style.paddingLeft = 8;
        container.style.paddingRight = 8;
        container.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f, 0.6f));
        container.style.borderTopLeftRadius = 4;
        container.style.borderTopRightRadius = 4;
        container.style.borderBottomLeftRadius = 4;
        container.style.borderBottomRightRadius = 4;

        if (pack.Reskins == null || pack.Reskins.Count == 0)
        {
            var empty = UITools.CreateConfigurationLabel("(no reskins)");
            empty.style.fontSize = 12;
            empty.style.color = new Color(0.6f, 0.6f, 0.6f);
            container.Add(empty);
            return container;
        }

        var grouped = pack.Reskins
            .GroupBy(r => string.IsNullOrEmpty(r.Type) ? "other" : r.Type)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            column.style.minWidth = 180;
            column.style.maxWidth = 240;
            column.style.marginRight = 16;
            column.style.marginBottom = 8;

            var header = new Label($"[{group.Key}]");
            header.style.color = new Color(0.7f, 0.85f, 1f);
            header.style.fontSize = 12;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 2;
            column.Add(header);

            foreach (var entry in group.OrderBy(e => e.Name))
            {
                var item = new Label($"• {entry.Name}");
                item.style.color = Color.white;
                item.style.fontSize = 12;
                item.style.whiteSpace = WhiteSpace.Normal;
                column.Add(item);
            }

            container.Add(column);
        }

        return container;
    }
}