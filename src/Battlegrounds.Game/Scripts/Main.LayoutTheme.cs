using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private void ApplySemanticLayout()
    {
        if (_modTheme is null)
            return;

        ApplySceneLayout();
        ApplyRowLayout(_leaderButtons, ModThemeMetricKeys.Row.Leader);
        ApplyRowLayout(_offerButtons, ModThemeMetricKeys.Row.Offer);
        ApplyRowLayout(_fieldButtons, ModThemeMetricKeys.Row.Field);
        ApplyRowLayout(_reserveButtons, ModThemeMetricKeys.Row.Reserve);
    }

    private void ApplySceneLayout()
    {
        var outerMargin = ResolveThemeMetric(ModThemeMetricKeys.Layout.OuterMargin, 0.0f, 512.0f);
        if (GetNodeOrNull<MarginContainer>("Margin") is { } margin)
        {
            margin.OffsetLeft = outerMargin;
            margin.OffsetTop = outerMargin;
            margin.OffsetRight = -outerMargin;
            margin.OffsetBottom = -outerMargin;
        }

        ApplySeparation("Margin/Shell", ModThemeMetricKeys.Layout.ShellGap);
        ApplyMinimumSize("Margin/Shell/OpponentRail", ModThemeMetricKeys.Layout.OpponentRailWidth, null);
        ApplySeparation("Margin/Shell/OpponentRail/Content", ModThemeMetricKeys.Layout.OpponentRailContentGap);
        ApplySeparation("Margin/Shell/OpponentRail/Content/OpponentEntries", ModThemeMetricKeys.Layout.OpponentRailEntriesGap);

        ApplySeparation("Margin/Shell/CenterStage", ModThemeMetricKeys.Layout.CenterStageGap);
        ApplyMinimumSize("Margin/Shell/CenterStage/Status", null, ModThemeMetricKeys.Layout.StatusMinimumHeight);
        ApplyMinimumSize("Margin/Shell/CenterStage/LeaderPanel", null, ModThemeMetricKeys.Layout.LeaderPanelMinimumHeight);
        ApplySeparation("Margin/Shell/CenterStage/LeaderPanel", ModThemeMetricKeys.Layout.LeaderPanelGap);
        ApplyMinimumSize("Margin/Shell/CenterStage/LeaderPanel/LeaderButtons", null, ModThemeMetricKeys.Layout.LeaderPanelCardsMinimumHeight);

        const string preparation = "Margin/Shell/CenterStage/PreparationPanel";
        ApplySeparation(preparation, ModThemeMetricKeys.Layout.PreparationGap);
        ApplyMinimumSize(preparation + "/TavernControls", null, ModThemeMetricKeys.Layout.TavernControlsMinimumHeight);
        ApplySeparation(preparation + "/TavernControls/ControlsRow", ModThemeMetricKeys.Layout.TavernControlsGap);
        ApplyMinimumSize(preparation + "/TavernControls/ControlsRow/TierBadge", ModThemeMetricKeys.Layout.TavernTierBadgeWidth, ModThemeMetricKeys.Layout.TavernTierBadgeHeight);
        ApplyMinimumSize(preparation + "/TavernControls/ControlsRow/UpgradeButton", ModThemeMetricKeys.Layout.TavernUpgradeButtonWidth, ModThemeMetricKeys.Layout.TavernControlHeight);
        ApplyMinimumSize(preparation + "/TavernControls/ControlsRow/ControlSpacer", ModThemeMetricKeys.Layout.TavernControlSpacerWidth, null);
        ApplyMinimumSize(preparation + "/TavernControls/ControlsRow/RefreshButton", ModThemeMetricKeys.Layout.TavernRefreshButtonWidth, ModThemeMetricKeys.Layout.TavernControlHeight);
        ApplyMinimumSize(preparation + "/TavernControls/ControlsRow/FreezeButton", ModThemeMetricKeys.Layout.TavernFreezeButtonWidth, ModThemeMetricKeys.Layout.TavernControlHeight);

        ApplyMinimumSize(preparation + "/TavernShelf", null, ModThemeMetricKeys.Layout.TavernShelfMinimumHeight);
        ApplySeparation(preparation + "/TavernShelf/ShelfRow", ModThemeMetricKeys.Layout.TavernShelfGap);
        ApplyMinimumSize(preparation + "/TavernShelf/ShelfRow/ShopkeeperSlot", ModThemeMetricKeys.Layout.TavernShopkeeperWidth, null);
        ApplySeparation(preparation + "/TavernShelf/ShelfRow/ShopkeeperSlot/Content", ModThemeMetricKeys.Layout.TavernShopkeeperContentGap);
        ApplyMinimumSize(preparation + "/TavernShelf/ShelfRow/TavernBalanceSpacer", ModThemeMetricKeys.Layout.TavernBalanceSpacerWidth, null);

        ApplyMinimumSize(preparation + "/BoardStage", null, ModThemeMetricKeys.Layout.BoardMinimumHeight);
        ApplyMinimumSize(preparation + "/ReserveShelf", null, ModThemeMetricKeys.Layout.ReserveMinimumHeight);

        ApplyMinimumSize(preparation + "/HeroDock", null, ModThemeMetricKeys.Hud.HeroDockMinimumHeight);
        ApplySeparation(preparation + "/HeroDock/HeroDockRow", ModThemeMetricKeys.Layout.HeroDockGap);
        ApplyMinimumSize(preparation + "/HeroDock/HeroDockRow/HealthBadge", ModThemeMetricKeys.Layout.HeroDockHealthBadgeWidth, ModThemeMetricKeys.Layout.HeroDockHealthBadgeHeight);
        ApplyMinimumSize(preparation + "/HeroDock/HeroDockRow/HudArmorBadge", ModThemeMetricKeys.Layout.HeroDockArmorBadgeWidth, ModThemeMetricKeys.Layout.HeroDockArmorBadgeHeight);
        ApplyMinimumSize(preparation + "/HeroDock/HeroDockRow/HeroCore", ModThemeMetricKeys.Layout.HeroDockHeroCoreWidth, null);
        ApplySeparation(preparation + "/HeroDock/HeroDockRow/HeroCore", ModThemeMetricKeys.Layout.HeroDockHeroCoreGap);
        ApplyMinimumSize(preparation + "/HeroDock/HeroDockRow/HeroCore/PowerButton", ModThemeMetricKeys.Layout.HeroDockPowerButtonWidth, ModThemeMetricKeys.Layout.HeroDockPowerButtonHeight);
        ApplyMinimumSize(preparation + "/HeroDock/HeroDockRow/CombineButton", ModThemeMetricKeys.Layout.HeroDockCombineButtonWidth, ModThemeMetricKeys.Layout.HeroDockCombineButtonHeight);
        ApplyMinimumSize(preparation + "/HeroDock/HeroDockRow/ResourceBadge", ModThemeMetricKeys.Layout.HeroDockResourceBadgeWidth, ModThemeMetricKeys.Layout.HeroDockResourceBadgeHeight);

        ApplyMinimumSize("Margin/Shell/TurnRail", ModThemeMetricKeys.Layout.TurnRailWidth, null);
        ApplySeparation("Margin/Shell/TurnRail/Content", ModThemeMetricKeys.Layout.TurnRailGap);
        ApplyMinimumSize("Margin/Shell/TurnRail/Content/EndPreparationButton", null, ModThemeMetricKeys.Layout.TurnRailEndButtonHeight);

        ApplyInteractionLayout();
    }

    private void ApplyInteractionLayout()
    {
        var panel = GetNodeOrNull<VBoxContainer>("InteractionPanel");
        if (panel is not null)
        {
            panel.AnchorLeft = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorLeft, 0.0f, 1.0f);
            panel.AnchorTop = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorTop, 0.0f, 1.0f);
            panel.AnchorRight = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorRight, 0.0f, 1.0f);
            panel.AnchorBottom = ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.AnchorBottom, 0.0f, 1.0f);
        }

        if (GetNodeOrNull<MarginContainer>("InteractionPanel/InteractionSurface/InteractionMargin") is { } margin)
        {
            var horizontal = Mathf.RoundToInt(ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.MarginHorizontal, 0, 512));
            var vertical = Mathf.RoundToInt(ResolveThemeMetric(ModThemeMetricKeys.Layout.Interaction.MarginVertical, 0, 512));
            margin.AddThemeConstantOverride("margin_left", horizontal);
            margin.AddThemeConstantOverride("margin_right", horizontal);
            margin.AddThemeConstantOverride("margin_top", vertical);
            margin.AddThemeConstantOverride("margin_bottom", vertical);
        }

        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent", ModThemeMetricKeys.Layout.Interaction.ContentGap);
        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent/InteractionButtons", ModThemeMetricKeys.Layout.Interaction.ChoiceGap);
        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent/InteractionActions", ModThemeMetricKeys.Layout.Interaction.ActionGap);
    }

    private void ApplyMinimumSize(string path, string? widthKey, string? heightKey)
    {
        var control = GetNodeOrNull<Control>(path);
        if (control is null)
            return;

        var width = widthKey is null
            ? control.CustomMinimumSize.X
            : ResolveThemeMetric(widthKey, 0.0f, 4096.0f);
        var height = heightKey is null
            ? control.CustomMinimumSize.Y
            : ResolveThemeMetric(heightKey, 0.0f, 4096.0f);
        control.CustomMinimumSize = new Vector2(width, height);
    }

    private void ApplySeparation(string path, string metricKey)
    {
        var control = GetNodeOrNull<Control>(path);
        if (control is null)
            return;

        var separation = Mathf.RoundToInt(ResolveThemeMetric(metricKey, 0.0f, 512.0f));
        control.AddThemeConstantOverride("separation", separation);
    }

    private void ApplyRowLayout(HorizontalCardRow row, string prefix)
    {
        row.Gap = ResolveThemeMetric(ModThemeMetricKeys.Row.Gap(prefix), 0.0f, 512.0f);
        row.PreferredCardWidth = ResolveThemeMetric(ModThemeMetricKeys.Row.PreferredCardWidth(prefix), 1.0f, 2048.0f);
        row.MinimumCardWidth = ResolveThemeMetric(ModThemeMetricKeys.Row.MinimumCardWidth(prefix), 1.0f, row.PreferredCardWidth);
        row.PreferredCardHeight = ResolveThemeMetric(ModThemeMetricKeys.Row.PreferredCardHeight(prefix), 1.0f, 2048.0f);
        row.Padding = ResolveThemeMetric(ModThemeMetricKeys.Row.Padding(prefix), 0.0f, 512.0f);
        row.QueueSort();
    }

    private float ResolveThemeMetric(string key, float minimum, float maximum)
    {
        if (_modTheme?.Metrics.TryGetValue(key, out var value) != true)
            throw new InvalidOperationException($"Resolved presentation theme is missing required metric '{key}'.");

        return Mathf.Clamp((float)value, minimum, maximum);
    }

    internal float ResolvePresentationMetric(string key, float minimum, float maximum) =>
        ResolveThemeMetric(key, minimum, maximum);

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
