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
        public const string PreparationControlsMinimumHeight = "layout.preparation.controls.minimumHeight";
        public const string PreparationControlsGap = "layout.preparation.controls.gap";
        public const string TierBadgeWidth = "layout.preparation.tierBadge.width";
        public const string TierBadgeHeight = "layout.preparation.tierBadge.height";
        public const string TierUpgradeButtonWidth = "layout.preparation.tierUpgradeButton.width";
        public const string PreparationControlHeight = "layout.preparation.controlHeight";
        public const string PreparationControlSpacerWidth = "layout.preparation.controlSpacerWidth";
        public const string OfferRefreshButtonWidth = "layout.preparation.offerRefreshButton.width";
        public const string OfferFreezeButtonWidth = "layout.preparation.offerFreezeButton.width";
        public const string OfferSurfaceMinimumHeight = "layout.preparation.offerSurface.minimumHeight";
        public const string OfferSurfaceGap = "layout.preparation.offerSurface.gap";
        public const string PreparationHostWidth = "layout.preparation.hostWidth";
        public const string PreparationHostContentGap = "layout.preparation.hostContentGap";
        public const string PreparationBalanceSpacerWidth = "layout.preparation.balanceSpacerWidth";
        public const string BoardMinimumHeight = "layout.board.minimumHeight";
        public const string ReserveMinimumHeight = "layout.reserve.minimumHeight";
        public const string LeaderDockGap = "layout.leaderDock.gap";
        public const string LeaderDockHealthBadgeWidth = "layout.leaderDock.healthBadge.width";
        public const string LeaderDockHealthBadgeHeight = "layout.leaderDock.healthBadge.height";
        public const string LeaderDockArmorBadgeWidth = "layout.leaderDock.armorBadge.width";
        public const string LeaderDockArmorBadgeHeight = "layout.leaderDock.armorBadge.height";
        public const string LeaderDockLeaderCoreWidth = "layout.leaderDock.leaderCoreWidth";
        public const string LeaderDockLeaderCoreGap = "layout.leaderDock.leaderCoreGap";
        public const string LeaderDockPowerButtonWidth = "layout.leaderDock.powerButton.width";
        public const string LeaderDockPowerButtonHeight = "layout.leaderDock.powerButton.height";
        public const string LeaderDockCombineButtonWidth = "layout.leaderDock.combineButton.width";
        public const string LeaderDockCombineButtonHeight = "layout.leaderDock.combineButton.height";
        public const string LeaderDockResourceBadgeWidth = "layout.leaderDock.resourceBadge.width";
        public const string LeaderDockResourceBadgeHeight = "layout.leaderDock.resourceBadge.height";
        public const string EndPreparationControlWidth = "layout.endPreparation.width";
        public const string EndPreparationControlGap = "layout.endPreparation.gap";
        public const string EndPreparationButtonHeight = "layout.endPreparation.buttonHeight";

        public static class PreparationScene
        {
            public const string OpponentRailWidth = "layout.preparationScene.opponentRailWidth";
            public const string ControlsTop = "layout.preparationScene.controlsTop";
            public const string OfferTop = "layout.preparationScene.offerTop";
            public const string FieldTop = "layout.preparationScene.fieldTop";
            public const string LeaderBottom = "layout.preparationScene.leaderBottom";
            public const string ReserveBottom = "layout.preparationScene.reserveBottom";
            public const string ResourceRight = "layout.preparationScene.resourceRight";
            public const string ResourceBottom = "layout.preparationScene.resourceBottom";
            public const string EndPreparationRight = "layout.preparationScene.endPreparationRight";
            public const string EndPreparationTop = "layout.preparationScene.endPreparationTop";
        }

        public static class CombatScene
        {
            public const string OpponentRailWidth = "layout.combatScene.opponentRailWidth";
            public const string OpponentLeaderTop = "layout.combatScene.opponentLeaderTop";
            public const string OpponentFieldTop = "layout.combatScene.opponentFieldTop";
            public const string PlayerFieldTop = "layout.combatScene.playerFieldTop";
            public const string PlayerLeaderBottom = "layout.combatScene.playerLeaderBottom";
            public const string EventTop = "layout.combatScene.eventTop";
            public const string RoundRight = "layout.combatScene.roundRight";
            public const string RoundTop = "layout.combatScene.roundTop";
        }

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

    public static class Hud
    {
        public const string LeaderDockMinimumHeight = "hud.leaderDock.minimumHeight";
        public const string LeaderPortraitSize = "hud.leaderPortrait.size";
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
        public const string DropTargetShadowScale = "drag.dropTarget.shadowScale";
    }

    public static class Motion
    {
        public const string CombatPlaybackStepSeconds = "motion.combatPlayback.stepSeconds";
        public const string UiSelectDurationSeconds = "motion.ui.select.durationSeconds";

        public static class Cue
        {
            public const string PulseScale = "motion.cue.pulse.scale";
            public const string PulseDurationSeconds = "motion.cue.pulse.durationSeconds";
            public const string ShakeRotationDegrees = "motion.cue.shake.rotationDegrees";
            public const string ShakeDurationSeconds = "motion.cue.shake.durationSeconds";
            public const string LungeScale = "motion.cue.lunge.scale";
            public const string LungeDurationSeconds = "motion.cue.lunge.durationSeconds";
            public const string FadeScale = "motion.cue.fade.scale";
            public const string FadeOpacity = "motion.cue.fade.opacity";
            public const string FadeDurationSeconds = "motion.cue.fade.durationSeconds";
            public const string PopScale = "motion.cue.pop.scale";
            public const string PopOpacity = "motion.cue.pop.opacity";
            public const string PopDurationSeconds = "motion.cue.pop.durationSeconds";
        }
    }
}
