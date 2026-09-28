using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private void ApplySemanticLayout()
    {
        if (_modTheme is null)
            return;

        ApplySceneChromeLayout();
        ApplyRowLayout(_leaderButtons, ModThemeMetricKeys.Row.Leader);
        ApplyRowLayout(_offerButtons, ModThemeMetricKeys.Row.Offer);
        ApplyRowLayout(_fieldButtons, ModThemeMetricKeys.Row.Field);
        ApplyRowLayout(_reserveButtons, ModThemeMetricKeys.Row.Reserve);
    }

    private void ApplySceneChromeLayout()
    {
        var outerMargin = ResolveThemeMetric(ModThemeMetricKeys.Layout.OuterMargin, 8.0f, 0.0f, 512.0f);
        if (GetNodeOrNull<MarginContainer>("Margin") is { } margin)
        {
            margin.OffsetLeft = outerMargin;
            margin.OffsetTop = outerMargin;
            margin.OffsetRight = -outerMargin;
            margin.OffsetBottom = -outerMargin;
        }

        ApplySeparation("Margin/Shell", ModThemeMetricKeys.Layout.ShellGap, 6);
        ApplyMinimumSize("Margin/Shell/OpponentRail", ModThemeMetricKeys.Layout.OpponentRailWidth, 184, null, 0);
        ApplySeparation("Margin/Shell/OpponentRail/Content", ModThemeMetricKeys.Layout.OpponentRailContentGap, 4);
        ApplySeparation("Margin/Shell/OpponentRail/Content/OpponentEntries", ModThemeMetricKeys.Layout.OpponentRailEntriesGap, 4);

        ApplySeparation("Margin/Shell/CenterStage", ModThemeMetricKeys.Layout.CenterStageGap, 3);
        ApplyMinimumSize("Margin/Shell/CenterStage/Status", null, 0, ModThemeMetricKeys.Layout.StatusMinimumHeight, 22);
        ApplyMinimumSize("Margin/Shell/CenterStage/LeaderPanel", null, 0, ModThemeMetricKeys.Layout.LeaderPanelMinimumHeight, 520);
        ApplySeparation("Margin/Shell/CenterStage/LeaderPanel", ModThemeMetricKeys.Layout.LeaderPanelGap, 12);
        ApplyMinimumSize("Margin/Shell/CenterStage/LeaderPanel/LeaderButtons", null, 0, ModThemeMetricKeys.Layout.LeaderPanelCardsMinimumHeight, 320);

        const string preparation = "Margin/Shell/CenterStage/PreparationPanel";
        ApplySeparation(preparation, ModThemeMetricKeys.Layout.PreparationGap, 3);
        ApplyMinimumSize(preparation + "/TavernControls", null, 0, ModThemeMetricKeys.Layout.TavernControlsMinimumHeight, 38);
        ApplySeparation(preparation + "/TavernControls", ModThemeMetricKeys.Layout.TavernControlsGap, 6);
        ApplyMinimumSize(preparation + "/TavernControls/TierBadge", ModThemeMetricKeys.Layout.TavernTierBadgeWidth, 42, ModThemeMetricKeys.Layout.TavernTierBadgeHeight, 34);
        ApplyMinimumSize(preparation + "/TavernControls/UpgradeButton", ModThemeMetricKeys.Layout.TavernUpgradeButtonWidth, 108, ModThemeMetricKeys.Layout.TavernControlHeight, 34);
        ApplyMinimumSize(preparation + "/TavernControls/ControlSpacer", ModThemeMetricKeys.Layout.TavernControlSpacerWidth, 30, null, 0);
        ApplyMinimumSize(preparation + "/TavernControls/RefreshButton", ModThemeMetricKeys.Layout.TavernRefreshButtonWidth, 96, ModThemeMetricKeys.Layout.TavernControlHeight, 34);
        ApplyMinimumSize(preparation + "/TavernControls/FreezeButton", ModThemeMetricKeys.Layout.TavernFreezeButtonWidth, 96, ModThemeMetricKeys.Layout.TavernControlHeight, 34);

        ApplyMinimumSize(preparation + "/TavernShelf", null, 0, ModThemeMetricKeys.Layout.TavernShelfMinimumHeight, 142);
        ApplySeparation(preparation + "/TavernShelf/ShelfRow", ModThemeMetricKeys.Layout.TavernShelfGap, 8);
        ApplyMinimumSize(preparation + "/TavernShelf/ShelfRow/ShopkeeperSlot", ModThemeMetricKeys.Layout.TavernShopkeeperWidth, 92, null, 0);
        ApplySeparation(preparation + "/TavernShelf/ShelfRow/ShopkeeperSlot/Content", ModThemeMetricKeys.Layout.TavernShopkeeperContentGap, 2);
        ApplyMinimumSize(preparation + "/TavernShelf/ShelfRow/TavernBalanceSpacer", ModThemeMetricKeys.Layout.TavernBalanceSpacerWidth, 92, null, 0);

        ApplyMinimumSize(preparation + "/BoardStage", null, 0, ModThemeMetricKeys.Layout.BoardMinimumHeight, 250);
        ApplyMinimumSize(preparation + "/ReserveShelf", null, 0, ModThemeMetricKeys.Layout.ReserveMinimumHeight, 72);

        ApplySeparation(preparation + "/HeroDock", ModThemeMetricKeys.Layout.HeroDockGap, 6);
        ApplyMinimumSize(preparation + "/HeroDock/HealthBadge", ModThemeMetricKeys.Layout.HeroDockHealthBadgeWidth, 52, ModThemeMetricKeys.Layout.HeroDockHealthBadgeHeight, 52);
        ApplyMinimumSize(preparation + "/HeroDock/HudArmorBadge", ModThemeMetricKeys.Layout.HeroDockArmorBadgeWidth, 46, ModThemeMetricKeys.Layout.HeroDockArmorBadgeHeight, 46);
        ApplyMinimumSize(preparation + "/HeroDock/HeroCore", ModThemeMetricKeys.Layout.HeroDockHeroCoreWidth, 224, null, 0);
        ApplySeparation(preparation + "/HeroDock/HeroCore", ModThemeMetricKeys.Layout.HeroDockHeroCoreGap, 2);
        ApplyMinimumSize(preparation + "/HeroDock/HeroCore/PowerButton", ModThemeMetricKeys.Layout.HeroDockPowerButtonWidth, 136, ModThemeMetricKeys.Layout.HeroDockPowerButtonHeight, 38);
        ApplyMinimumSize(preparation + "/HeroDock/CombineButton", ModThemeMetricKeys.Layout.HeroDockCombineButtonWidth, 94, ModThemeMetricKeys.Layout.HeroDockCombineButtonHeight, 40);
        ApplyMinimumSize(preparation + "/HeroDock/ResourceBadge", ModThemeMetricKeys.Layout.HeroDockResourceBadgeWidth, 58, ModThemeMetricKeys.Layout.HeroDockResourceBadgeHeight, 52);

        ApplyMinimumSize("Margin/Shell/TurnRail", ModThemeMetricKeys.Layout.TurnRailWidth, 92, null, 0);
        ApplySeparation("Margin/Shell/TurnRail/Content", ModThemeMetricKeys.Layout.TurnRailGap, 6);
        ApplyMinimumSize("Margin/Shell/TurnRail/Content/EndPreparationButton", null, 0, ModThemeMetricKeys.Layout.TurnRailEndButtonHeight, 64);

        ApplyInteractionLayout();
    }

    private void ApplyInteractionLayout()
    {
        var panel = GetNodeOrNull<VBoxContainer>("InteractionPanel");
        if (panel is not null)
        {
            panel.AnchorLeft = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorLeft, 0.24f, 0.0f, 1.0f);
            panel.AnchorTop = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorTop, 0.20f, 0.0f, 1.0f);
            panel.AnchorRight = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorRight, 0.76f, 0.0f, 1.0f);
            panel.AnchorBottom = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorBottom, 0.80f, 0.0f, 1.0f);
        }

        if (GetNodeOrNull<MarginContainer>("InteractionPanel/InteractionSurface/InteractionMargin") is { } margin)
        {
            var horizontal = Mathf.RoundToInt(ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.MarginHorizontal, 12, 0, 512));
            var vertical = Mathf.RoundToInt(ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.MarginVertical, 10, 0, 512));
            margin.AddThemeConstantOverride("margin_left", horizontal);
            margin.AddThemeConstantOverride("margin_right", horizontal);
            margin.AddThemeConstantOverride("margin_top", vertical);
            margin.AddThemeConstantOverride("margin_bottom", vertical);
        }

        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent", ModThemeMetricKeys.Layout.Interaction.ContentGap, 6);
        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent/InteractionButtons", ModThemeMetricKeys.Layout.Interaction.ChoiceGap, 4);
        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent/InteractionActions", ModThemeMetricKeys.Layout.Interaction.ActionGap, 8);
    }

    private void ApplyMinimumSize(
        string path,
        string? widthKey,
        float fallbackWidth,
        string? heightKey,
        float fallbackHeight)
    {
        var control = GetNodeOrNull<Control>(path);
        if (control is null)
            return;

        var width = widthKey is null
            ? control.CustomMinimumSize.X
            : ResolveThemeMetric(widthKey, fallbackWidth, 0.0f, 4096.0f);
        var height = heightKey is null
            ? control.CustomMinimumSize.Y
            : ResolveThemeMetric(heightKey, fallbackHeight, 0.0f, 4096.0f);
        control.CustomMinimumSize = new Vector2(width, height);
    }

    private void ApplySeparation(string path, string metricKey, int fallback)
    {
        var control = GetNodeOrNull<Control>(path);
        if (control is null)
            return;

        var separation = Mathf.RoundToInt(ResolveThemeMetric(metricKey, fallback, 0.0f, 512.0f));
        control.AddThemeConstantOverride("separation", separation);
    }

    private void ApplyRowLayout(HorizontalCardRow row, string prefix)
    {
        row.Gap = ResolveThemeMetric(ModThemeMetricKeys.Row.Gap(prefix), row.Gap, 0.0f, 512.0f);
        row.PreferredCardWidth = ResolveThemeMetric(ModThemeMetricKeys.Row.PreferredCardWidth(prefix), row.PreferredCardWidth, 1.0f, 2048.0f);
        row.MinimumCardWidth = ResolveThemeMetric(ModThemeMetricKeys.Row.MinimumCardWidth(prefix), row.MinimumCardWidth, 1.0f, row.PreferredCardWidth);
        row.PreferredCardHeight = ResolveThemeMetric(ModThemeMetricKeys.Row.PreferredCardHeight(prefix), row.PreferredCardHeight, 1.0f, 2048.0f);
        row.Padding = ResolveThemeMetric(ModThemeMetricKeys.Row.Padding(prefix), row.Padding, 0.0f, 512.0f);
        row.QueueSort();
    }

    private float ResolveThemeMetric(string key, float fallback, float minimum, float maximum)
    {
        if (_modTheme?.Metrics.TryGetValue(key, out var value) != true)
            return fallback;

        return Mathf.Clamp((float)value, minimum, maximum);
    }

    internal float ResolvePresentationMetric(string key, float fallback, float minimum, float maximum) =>
        ResolveThemeMetric(key, fallback, minimum, maximum);

    private void ApplyPresentationCardLayout(PresentationCardButton card)
    {
        if (_modTheme is null ||
            !_modTheme.Components.TryGetValue(ModThemeComponentRoles.Card, out var style) ||
            style.Padding is null)
            return;

        var margin = card.GetChildren().OfType<MarginContainer>().FirstOrDefault();
        if (margin is null)
            return;

        if (_modTheme.Spacing.TryGetValue(style.Padding.Horizontal, out var horizontal))
        {
            margin.AddThemeConstantOverride("margin_left", horizontal);
            margin.AddThemeConstantOverride("margin_right", horizontal);
        }

        if (_modTheme.Spacing.TryGetValue(style.Padding.Vertical, out var vertical))
        {
            margin.AddThemeConstantOverride("margin_top", vertical);
            margin.AddThemeConstantOverride("margin_bottom", vertical);
        }
    }
}
