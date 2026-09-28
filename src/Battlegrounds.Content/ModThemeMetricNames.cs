namespace Battlegrounds.Content;

public static class ModThemeMetricNames
{
    private static readonly HashSet<string> ExactNames = new(StringComparer.Ordinal)
    {
        "layout.outerMargin",
        "layout.shell.gap",
        "layout.opponentRail.width",
        "layout.opponentRail.contentGap",
        "layout.opponentRail.entriesGap",
        "layout.centerStage.gap",
        "layout.status.minimumHeight",
        "layout.leaderPanel.minimumHeight",
        "layout.leaderPanel.gap",
        "layout.leaderPanel.cardsMinimumHeight",
        "layout.preparation.gap",
        "layout.tavern.controls.minimumHeight",
        "layout.tavern.controls.gap",
        "layout.tavern.tierBadge.width",
        "layout.tavern.tierBadge.height",
        "layout.tavern.upgradeButton.width",
        "layout.tavern.controlHeight",
        "layout.tavern.controlSpacerWidth",
        "layout.tavern.refreshButton.width",
        "layout.tavern.freezeButton.width",
        "layout.tavern.shelf.minimumHeight",
        "layout.tavern.shelf.gap",
        "layout.tavern.shopkeeperWidth",
        "layout.tavern.shopkeeperContentGap",
        "layout.tavern.balanceSpacerWidth",
        "layout.board.minimumHeight",
        "layout.reserve.minimumHeight",
        "layout.heroDock.gap",
        "layout.heroDock.healthBadge.width",
        "layout.heroDock.healthBadge.height",
        "layout.heroDock.armorBadge.width",
        "layout.heroDock.armorBadge.height",
        "layout.heroDock.heroCoreWidth",
        "layout.heroDock.heroCoreGap",
        "layout.heroDock.powerButton.width",
        "layout.heroDock.powerButton.height",
        "layout.heroDock.combineButton.width",
        "layout.heroDock.combineButton.height",
        "layout.heroDock.resourceBadge.width",
        "layout.heroDock.resourceBadge.height",
        "layout.turnRail.width",
        "layout.turnRail.gap",
        "layout.turnRail.endButtonHeight",
        "layout.interaction.anchorLeft",
        "layout.interaction.anchorTop",
        "layout.interaction.anchorRight",
        "layout.interaction.anchorBottom",
        "layout.interaction.marginHorizontal",
        "layout.interaction.marginVertical",
        "layout.interaction.contentGap",
        "layout.interaction.choiceGap",
        "layout.interaction.actionGap",
        "layout.combat.marginHorizontal",
        "layout.combat.marginVertical",
        "layout.combat.contentGap",
        "layout.combat.boardsGap",
        "layout.combat.unitGap",
        "layout.combat.controlsGap",
        "card.contentGap",
        "hud.heroDock.minimumHeight",
        "hud.heroPortrait.size",
        "hud.opponent.entryHeight",
        "hud.opponent.marginHorizontal",
        "hud.opponent.marginVertical",
        "hud.opponent.gap",
        "hud.opponent.rankWidth",
        "hud.opponent.portraitSize",
        "hud.opponent.statsWidth",
        "hud.opponent.identityGap",
        "hud.opponent.statsGap",
        "drag.preview.scale",
        "drag.preview.rotationDegrees",
        "launcher.marginHorizontal",
        "launcher.marginVertical",
        "launcher.gap",
        "launcher.modGap",
        "launcher.diagnosticsMinimumHeight",
    };

    private static readonly HashSet<string> RowRoles = new(StringComparer.Ordinal)
    {
        "leader", "offer", "field", "reserve",
    };

    private static readonly HashSet<string> RowProperties = new(StringComparer.Ordinal)
    {
        "gap", "preferredCardWidth", "minimumCardWidth", "preferredCardHeight", "padding",
    };

    private static readonly HashSet<string> CardRoles = new(StringComparer.Ordinal)
    {
        "default", "leader", "shop", "board", "reserve", "choice",
    };

    private static readonly HashSet<string> CardProperties = new(StringComparer.Ordinal)
    {
        "minimumWidth", "artHeight",
    };

    public static bool IsSupported(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (ExactNames.Contains(name)) return true;

        var parts = name.Split('.', StringSplitOptions.None);
        if (parts.Length != 3) return false;

        return parts[0] switch
        {
            "row" => RowRoles.Contains(parts[1]) && RowProperties.Contains(parts[2]),
            "card" => CardRoles.Contains(parts[1]) && CardProperties.Contains(parts[2]),
            _ => false,
        };
    }
}
