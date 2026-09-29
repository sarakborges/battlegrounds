namespace Battlegrounds.Content;

public static class ModThemeMetricNames
{
    private static readonly HashSet<string> ExactNames = new(StringComparer.Ordinal)
    {
        ModThemeMetricKeys.Layout.OuterMargin,
        ModThemeMetricKeys.Layout.ShellGap,
        ModThemeMetricKeys.Layout.OpponentRailWidth,
        ModThemeMetricKeys.Layout.OpponentRailContentGap,
        ModThemeMetricKeys.Layout.OpponentRailEntriesGap,
        ModThemeMetricKeys.Layout.CenterStageGap,
        ModThemeMetricKeys.Layout.StatusMinimumHeight,
        ModThemeMetricKeys.Layout.LeaderPanelMinimumHeight,
        ModThemeMetricKeys.Layout.LeaderPanelGap,
        ModThemeMetricKeys.Layout.LeaderPanelCardsMinimumHeight,
        ModThemeMetricKeys.Layout.PreparationGap,
        ModThemeMetricKeys.Layout.TavernControlsMinimumHeight,
        ModThemeMetricKeys.Layout.TavernControlsGap,
        ModThemeMetricKeys.Layout.TavernTierBadgeWidth,
        ModThemeMetricKeys.Layout.TavernTierBadgeHeight,
        ModThemeMetricKeys.Layout.TavernUpgradeButtonWidth,
        ModThemeMetricKeys.Layout.TavernControlHeight,
        ModThemeMetricKeys.Layout.TavernControlSpacerWidth,
        ModThemeMetricKeys.Layout.TavernRefreshButtonWidth,
        ModThemeMetricKeys.Layout.TavernFreezeButtonWidth,
        ModThemeMetricKeys.Layout.TavernShelfMinimumHeight,
        ModThemeMetricKeys.Layout.TavernShelfGap,
        ModThemeMetricKeys.Layout.TavernShopkeeperWidth,
        ModThemeMetricKeys.Layout.TavernShopkeeperContentGap,
        ModThemeMetricKeys.Layout.TavernBalanceSpacerWidth,
        ModThemeMetricKeys.Layout.BoardMinimumHeight,
        ModThemeMetricKeys.Layout.ReserveMinimumHeight,
        ModThemeMetricKeys.Layout.HeroDockGap,
        ModThemeMetricKeys.Layout.HeroDockHealthBadgeWidth,
        ModThemeMetricKeys.Layout.HeroDockHealthBadgeHeight,
        ModThemeMetricKeys.Layout.HeroDockArmorBadgeWidth,
        ModThemeMetricKeys.Layout.HeroDockArmorBadgeHeight,
        ModThemeMetricKeys.Layout.HeroDockHeroCoreWidth,
        ModThemeMetricKeys.Layout.HeroDockHeroCoreGap,
        ModThemeMetricKeys.Layout.HeroDockPowerButtonWidth,
        ModThemeMetricKeys.Layout.HeroDockPowerButtonHeight,
        ModThemeMetricKeys.Layout.HeroDockCombineButtonWidth,
        ModThemeMetricKeys.Layout.HeroDockCombineButtonHeight,
        ModThemeMetricKeys.Layout.HeroDockResourceBadgeWidth,
        ModThemeMetricKeys.Layout.HeroDockResourceBadgeHeight,
        ModThemeMetricKeys.Layout.TurnRailWidth,
        ModThemeMetricKeys.Layout.TurnRailGap,
        ModThemeMetricKeys.Layout.TurnRailEndButtonHeight,
        ModThemeMetricKeys.Layout.PreparationScene.OpponentRailWidth,
        ModThemeMetricKeys.Layout.PreparationScene.TavernTop,
        ModThemeMetricKeys.Layout.PreparationScene.OfferTop,
        ModThemeMetricKeys.Layout.PreparationScene.FieldTop,
        ModThemeMetricKeys.Layout.PreparationScene.HeroBottom,
        ModThemeMetricKeys.Layout.PreparationScene.ReserveBottom,
        ModThemeMetricKeys.Layout.PreparationScene.ResourceRight,
        ModThemeMetricKeys.Layout.PreparationScene.ResourceBottom,
        ModThemeMetricKeys.Layout.PreparationScene.EndTurnRight,
        ModThemeMetricKeys.Layout.PreparationScene.EndTurnTop,
        ModThemeMetricKeys.Layout.Interaction.AnchorLeft,
        ModThemeMetricKeys.Layout.Interaction.AnchorTop,
        ModThemeMetricKeys.Layout.Interaction.AnchorRight,
        ModThemeMetricKeys.Layout.Interaction.AnchorBottom,
        ModThemeMetricKeys.Layout.Interaction.MarginHorizontal,
        ModThemeMetricKeys.Layout.Interaction.MarginVertical,
        ModThemeMetricKeys.Layout.Interaction.ContentGap,
        ModThemeMetricKeys.Layout.Interaction.ChoiceGap,
        ModThemeMetricKeys.Layout.Interaction.ActionGap,
        ModThemeMetricKeys.Layout.Combat.MarginHorizontal,
        ModThemeMetricKeys.Layout.Combat.MarginVertical,
        ModThemeMetricKeys.Layout.Combat.ContentGap,
        ModThemeMetricKeys.Layout.Combat.BoardsGap,
        ModThemeMetricKeys.Layout.Combat.UnitGap,
        ModThemeMetricKeys.Layout.Combat.ControlsGap,
        ModThemeMetricKeys.Card.ContentGap,
        ModThemeMetricKeys.Card.TokenBadgeSize,
        ModThemeMetricKeys.Card.TokenBadgeInset,
        ModThemeMetricKeys.Card.InspectWidth,
        ModThemeMetricKeys.Card.InspectMinimumHeight,
        ModThemeMetricKeys.Card.InspectArtHeight,
        ModThemeMetricKeys.Card.InspectOffset,
        ModThemeMetricKeys.Card.InspectBadgeSize,
        ModThemeMetricKeys.Card.InspectBadgeInset,
        ModThemeMetricKeys.Card.InspectContentGap,
        ModThemeMetricKeys.Hud.HeroDockMinimumHeight,
        ModThemeMetricKeys.Hud.HeroPortraitSize,
        ModThemeMetricKeys.Hud.OpponentEntryHeight,
        ModThemeMetricKeys.Hud.OpponentMarginHorizontal,
        ModThemeMetricKeys.Hud.OpponentMarginVertical,
        ModThemeMetricKeys.Hud.OpponentGap,
        ModThemeMetricKeys.Hud.OpponentRankWidth,
        ModThemeMetricKeys.Hud.OpponentPortraitSize,
        ModThemeMetricKeys.Hud.OpponentStatsWidth,
        ModThemeMetricKeys.Hud.OpponentIdentityGap,
        ModThemeMetricKeys.Hud.OpponentStatsGap,
        ModThemeMetricKeys.Drag.PreviewScale,
        ModThemeMetricKeys.Drag.PreviewRotationDegrees,
        ModThemeMetricKeys.Drag.DropTargetShadowScale,
        ModThemeMetricKeys.Motion.CombatPlaybackStepSeconds,
        ModThemeMetricKeys.Motion.UiSelectDurationSeconds,
        ModThemeMetricKeys.Motion.Cue.PulseScale,
        ModThemeMetricKeys.Motion.Cue.PulseDurationSeconds,
        ModThemeMetricKeys.Motion.Cue.ShakeRotationDegrees,
        ModThemeMetricKeys.Motion.Cue.ShakeDurationSeconds,
        ModThemeMetricKeys.Motion.Cue.LungeScale,
        ModThemeMetricKeys.Motion.Cue.LungeDurationSeconds,
        ModThemeMetricKeys.Motion.Cue.FadeScale,
        ModThemeMetricKeys.Motion.Cue.FadeOpacity,
        ModThemeMetricKeys.Motion.Cue.FadeDurationSeconds,
        ModThemeMetricKeys.Motion.Cue.PopScale,
        ModThemeMetricKeys.Motion.Cue.PopOpacity,
        ModThemeMetricKeys.Motion.Cue.PopDurationSeconds,
        ModThemeMetricKeys.Launcher.MarginHorizontal,
        ModThemeMetricKeys.Launcher.MarginVertical,
        ModThemeMetricKeys.Launcher.Gap,
        ModThemeMetricKeys.Launcher.ModGap,
        ModThemeMetricKeys.Launcher.DiagnosticsMinimumHeight,
    };

    private static readonly HashSet<string> RowRoles = new(StringComparer.Ordinal)
    {
        ModThemeMetricKeys.Row.LeaderRole,
        ModThemeMetricKeys.Row.OfferRole,
        ModThemeMetricKeys.Row.FieldRole,
        ModThemeMetricKeys.Row.ReserveRole,
    };

    private static readonly HashSet<string> RowProperties = new(StringComparer.Ordinal)
    {
        "gap", "preferredCardWidth", "minimumCardWidth", "preferredCardHeight", "padding",
    };

    private static readonly HashSet<string> CardRoles = new(StringComparer.Ordinal)
    {
        ModThemeMetricKeys.Card.DefaultRole,
        ModThemeMetricKeys.Card.LeaderRole,
        ModThemeMetricKeys.Card.ShopRole,
        ModThemeMetricKeys.Card.BoardRole,
        ModThemeMetricKeys.Card.ReserveRole,
        ModThemeMetricKeys.Card.ChoiceRole,
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
