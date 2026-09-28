namespace Battlegrounds.Content;

public static class ModThemeMetricKeys
{
    public static class Launcher
    {
        public const string MarginHorizontal = "launcher.marginHorizontal";
        public const string MarginVertical = "launcher.marginVertical";
        public const string Gap = "launcher.gap";
        public const string ModGap = "launcher.modGap";
        public const string DiagnosticsMinimumHeight = "launcher.diagnosticsMinimumHeight";
    }

    public static class Layout
    {
        public const string OuterMargin = "layout.outerMargin";
        public const string ShellGap = "layout.shell.gap";
        public const string OpponentRailWidth = "layout.opponentRail.width";
        public const string OpponentRailContentGap = "layout.opponentRail.contentGap";
        public const string OpponentRailEntriesGap = "layout.opponentRail.entriesGap";
        public const string CenterStageGap = "layout.centerStage.gap";
        public const string StatusMinimumHeight = "layout.status.minimumHeight";
        public const string LeaderPanelMinimumHeight = "layout.leaderPanel.minimumHeight";
        public const string LeaderPanelGap = "layout.leaderPanel.gap";
        public const string LeaderPanelCardsMinimumHeight = "layout.leaderPanel.cardsMinimumHeight";
        public const string PreparationGap = "layout.preparation.gap";
        public const string TavernControlsMinimumHeight = "layout.tavern.controls.minimumHeight";
        public const string TavernControlsGap = "layout.tavern.controls.gap";
        public const string TavernTierBadgeWidth = "layout.tavern.tierBadge.width";
        public const string TavernTierBadgeHeight = "layout.tavern.tierBadge.height";
        public const string TavernUpgradeButtonWidth = "layout.tavern.upgradeButton.width";
        public const string TavernControlHeight = "layout.tavern.controlHeight";
        public const string TavernControlSpacerWidth = "layout.tavern.controlSpacerWidth";
        public const string TavernRefreshButtonWidth = "layout.tavern.refreshButton.width";
        public const string TavernFreezeButtonWidth = "layout.tavern.freezeButton.width";
        public const string TavernShelfMinimumHeight = "layout.tavern.shelf.minimumHeight";
        public const string TavernShelfGap = "layout.tavern.shelf.gap";
        public const string TavernShopkeeperWidth = "layout.tavern.shopkeeperWidth";
        public const string TavernShopkeeperContentGap = "layout.tavern.shopkeeperContentGap";
        public const string TavernBalanceSpacerWidth = "layout.tavern.balanceSpacerWidth";
        public const string BoardMinimumHeight = "layout.board.minimumHeight";
        public const string ReserveMinimumHeight = "layout.reserve.minimumHeight";
        public const string HeroDockGap = "layout.heroDock.gap";
        public const string HeroDockHealthBadgeWidth = "layout.heroDock.healthBadge.width";
        public const string HeroDockHealthBadgeHeight = "layout.heroDock.healthBadge.height";
        public const string HeroDockArmorBadgeWidth = "layout.heroDock.armorBadge.width";
        public const string HeroDockArmorBadgeHeight = "layout.heroDock.armorBadge.height";
        public const string HeroDockHeroCoreWidth = "layout.heroDock.heroCoreWidth";
        public const string HeroDockHeroCoreGap = "layout.heroDock.heroCoreGap";
        public const string HeroDockPowerButtonWidth = "layout.heroDock.powerButton.width";
        public const string HeroDockPowerButtonHeight = "layout.heroDock.powerButton.height";
        public const string HeroDockCombineButtonWidth = "layout.heroDock.combineButton.width";
        public const string HeroDockCombineButtonHeight = "layout.heroDock.combineButton.height";
        public const string HeroDockResourceBadgeWidth = "layout.heroDock.resourceBadge.width";
        public const string HeroDockResourceBadgeHeight = "layout.heroDock.resourceBadge.height";
        public const string TurnRailWidth = "layout.turnRail.width";
        public const string TurnRailGap = "layout.turnRail.gap";
        public const string TurnRailEndButtonHeight = "layout.turnRail.endButtonHeight";

        public static class Interaction
        {
            public const string AnchorLeft = "layout.interaction.anchorLeft";
            public const string AnchorTop = "layout.interaction.anchorTop";
            public const string AnchorRight = "layout.interaction.anchorRight";
            public const string AnchorBottom = "layout.interaction.anchorBottom";
            public const string MarginHorizontal = "layout.interaction.marginHorizontal";
            public const string MarginVertical = "layout.interaction.marginVertical";
            public const string ContentGap = "layout.interaction.contentGap";
            public const string ChoiceGap = "layout.interaction.choiceGap";
            public const string ActionGap = "layout.interaction.actionGap";
        }

        public static class Combat
        {
            public const string MarginHorizontal = "layout.combat.marginHorizontal";
            public const string MarginVertical = "layout.combat.marginVertical";
            public const string ContentGap = "layout.combat.contentGap";
            public const string BoardsGap = "layout.combat.boardsGap";
            public const string UnitGap = "layout.combat.unitGap";
            public const string ControlsGap = "layout.combat.controlsGap";
        }
    }

    public static class Card
    {
        public const string DefaultRole = "default";
        public const string LeaderRole = "leader";
        public const string ShopRole = "shop";
        public const string BoardRole = "board";
        public const string ReserveRole = "reserve";
        public const string ChoiceRole = "choice";
        public const string ContentGap = "card.contentGap";

        public static string MinimumWidth(string role) => $"card.{role}.minimumWidth";
        public static string ArtHeight(string role) => $"card.{role}.artHeight";
    }

    public static class Row
    {
        public const string LeaderRole = "leader";
        public const string OfferRole = "offer";
        public const string FieldRole = "field";
        public const string ReserveRole = "reserve";

        public const string Leader = "row.leader";
        public const string Offer = "row.offer";
        public const string Field = "row.field";
        public const string Reserve = "row.reserve";

        public static string Gap(string prefix) => prefix + ".gap";
        public static string PreferredCardWidth(string prefix) => prefix + ".preferredCardWidth";
        public static string MinimumCardWidth(string prefix) => prefix + ".minimumCardWidth";
        public static string PreferredCardHeight(string prefix) => prefix + ".preferredCardHeight";
        public static string Padding(string prefix) => prefix + ".padding";
    }

    public static class Hud
    {
        public const string HeroDockMinimumHeight = "hud.heroDock.minimumHeight";
        public const string HeroPortraitSize = "hud.heroPortrait.size";
        public const string OpponentEntryHeight = "hud.opponent.entryHeight";
        public const string OpponentMarginHorizontal = "hud.opponent.marginHorizontal";
        public const string OpponentMarginVertical = "hud.opponent.marginVertical";
        public const string OpponentGap = "hud.opponent.gap";
        public const string OpponentRankWidth = "hud.opponent.rankWidth";
        public const string OpponentPortraitSize = "hud.opponent.portraitSize";
        public const string OpponentStatsWidth = "hud.opponent.statsWidth";
        public const string OpponentIdentityGap = "hud.opponent.identityGap";
        public const string OpponentStatsGap = "hud.opponent.statsGap";
    }

    public static class Drag
    {
        public const string PreviewScale = "drag.preview.scale";
        public const string PreviewRotationDegrees = "drag.preview.rotationDegrees";
    }
}
