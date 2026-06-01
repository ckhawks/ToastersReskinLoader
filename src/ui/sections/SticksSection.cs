using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.ui.sections;

public static class SticksSection
{
    public static void CreateSection(VisualElement contentScrollViewContent)
    {
        void showStick() { ChangingRoomHelper.ShowStick(); }
        contentScrollViewContent.schedule.Execute(showStick).ExecuteLater(2);
        

        Label monetizationDisclaimer = new Label(
            "<size=14>Please support the developer, GAFURIX, by subscribing to the Puck Patreon and purchasing the in-game cosmetics. Please do not use Toaster's Reskin Loader as a way to circumvent supporting the game's developer.</size><br>");
        monetizationDisclaimer.style.color = Color.white;
        monetizationDisclaimer.style.marginBottom = 16;
        monetizationDisclaimer.style.whiteSpace = WhiteSpace.Normal;
        contentScrollViewContent.Add(monetizationDisclaimer);
            
        var profile = ReskinProfileManager.currentProfile;

        // Attacker section
        var attackerStickReskins = ReskinRegistry.GetReskinChoices("stick_attacker", out var unchangedAttacker);
        Label attackerSticksTitle = new Label("Skater");
        attackerSticksTitle.style.fontSize = 24;
        attackerSticksTitle.style.color = Color.white;
        contentScrollViewContent.Add(attackerSticksTitle);

        UITools.AddReskinDropdownRow(contentScrollViewContent, "Blue personal", attackerStickReskins,
            profile.stickAttackerBluePersonal, unchangedAttacker, "stick_attacker", "blue_personal", PlayerTeam.Blue, PlayerRole.Attacker);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Red personal", attackerStickReskins,
            profile.stickAttackerRedPersonal, unchangedAttacker, "stick_attacker", "red_personal", PlayerTeam.Red, PlayerRole.Attacker);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Blue team", attackerStickReskins,
            profile.stickAttackerBlue, unchangedAttacker, "stick_attacker", "blue_team", PlayerTeam.Blue, PlayerRole.Attacker);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Red team", attackerStickReskins,
            profile.stickAttackerRed, unchangedAttacker, "stick_attacker", "red_team", PlayerTeam.Red, PlayerRole.Attacker);

        // Goalie section
        var goalieStickReskins = ReskinRegistry.GetReskinChoices("stick_goalie", out var unchangedGoalie);
        Label goalieSticksTitle = new Label("Goalie");
        goalieSticksTitle.style.fontSize = 24;
        goalieSticksTitle.style.color = Color.white;
        contentScrollViewContent.Add(goalieSticksTitle);

        UITools.AddReskinDropdownRow(contentScrollViewContent, "Blue personal", goalieStickReskins,
            profile.stickGoalieBluePersonal, unchangedGoalie, "stick_goalie", "blue_personal", PlayerTeam.Blue, PlayerRole.Goalie);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Red personal", goalieStickReskins,
            profile.stickGoalieRedPersonal, unchangedGoalie, "stick_goalie", "red_personal", PlayerTeam.Red, PlayerRole.Goalie);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Blue team", goalieStickReskins,
            profile.stickGoalieBlue, unchangedGoalie, "stick_goalie", "blue_team", PlayerTeam.Blue, PlayerRole.Goalie);
        UITools.AddReskinDropdownRow(contentScrollViewContent, "Red team", goalieStickReskins,
            profile.stickGoalieRed, unchangedGoalie, "stick_goalie", "red_team", PlayerTeam.Red, PlayerRole.Goalie);
    }
    
}