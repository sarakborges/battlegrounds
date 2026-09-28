using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private static class LayoutMetricKeys
    {
        public const string LeaderRow = "row.leader";
        public const string OfferRow = "row.offer";
        public const string FieldRow = "row.field";
        public const string ReserveRow = "row.reserve";
    }

    private void ApplySemanticLayout()
    {
        if (_modTheme is null)
            return;

        ApplySceneChromeLayout();
        ApplyRowLayout(_leaderButtons, LayoutMetricKeys.LeaderRow);
        ApplyRowLayout(_offerButtons, LayoutMetricKeys.OfferRow);
        ApplyRowLayout(_fieldButtons, LayoutMetricKeys.FieldRow);
        ApplyRowLayout(_reserveButtons, LayoutMetricKeys.ReserveRow);
    }

    private void ApplySceneChromeLayout()
    {
        var outerMargin = ResolveThemeMetric("layout.outerMargin", 8.0f, 0.0f, 512.0f);
        if (GetNodeOrNull<MarginContainer>("Margin") is { } margin)
        {
            margin.OffsetLeft = outerMargin;
            margin.OffsetTop = outerMargin;
            margin.OffsetRight = -outerMargin;
            margin.OffsetBottom = -outerMargin;
        }

        ApplySeparation("Margin/Shell", "layout.shell.gap", 6);
        ApplyMinimumSize("Margin/Shell/OpponentRail", "layout.opponentRail.width", 184, null, 0);
        ApplySeparation("Margin/Shell/OpponentRail/Content", "layout.opponentRail.contentGap", 4);
        ApplySeparation("Margin/Shell/OpponentRail/Content/OpponentEntries", "layout.opponentRail.entriesGap", 4);

        ApplySeparation("Margin/Shell/CenterStage", "layout.centerStage.gap", 3);
        ApplyMinimumSize("Margin/Shell/CenterStage/Status", null, 0, "layout.status.minimumHeight", 22);
        ApplyMinimumSize("Margin/Shell/CenterStage/LeaderPanel", null, 0, "layout.leaderPanel.minimumHeight", 520);
        ApplySeparation("Margin/Shell/CenterStage/LeaderPanel", "layout.leaderPanel.gap", 12);
        ApplyMinimumSize("Margin/Shell/CenterStage/LeaderPanel/LeaderButtons", null, 0, "layout.leaderPanel.cardsMinimumHeight", 320);

        const string preparation = "Margin/Shell/CenterStage/PreparationPanel";
        ApplySeparation(preparation, "layout.preparation.gap", 3);
        ApplyMinimumSize(preparation + "/TavernControls", null, 0, "layout.tavern.controls.minimumHeight", 38);
        ApplySeparation(preparation + "/TavernControls", "layout.tavern.controls.gap", 6);
        ApplyMinimumSize(preparation + "/TavernControls/TierBadge", "layout.tavern.tierBadge.width", 42, "layout.tavern.tierBadge.height", 34);
        ApplyMinimumSize(preparation + "/TavernControls/UpgradeButton", "layout.tavern.upgradeButton.width", 108, "layout.tavern.controlHeight", 34);
        ApplyMinimumSize(preparation + "/TavernControls/ControlSpacer", "layout.tavern.controlSpacerWidth", 30, null, 0);
        ApplyMinimumSize(preparation + "/TavernControls/RefreshButton", "layout.tavern.refreshButton.width", 96, "layout.tavern.controlHeight", 34);
        ApplyMinimumSize(preparation + "/TavernControls/FreezeButton", "layout.tavern.freezeButton.width", 96, "layout.tavern.controlHeight", 34);

        ApplyMinimumSize(preparation + "/TavernShelf", null, 0, "layout.tavern.shelf.minimumHeight", 142);
        ApplySeparation(preparation + "/TavernShelf/ShelfRow", "layout.tavern.shelf.gap", 8);
        ApplyMinimumSize(preparation + "/TavernShelf/ShelfRow/ShopkeeperSlot", "layout.tavern.shopkeeperWidth", 92, null, 0);
        ApplySeparation(preparation + "/TavernShelf/ShelfRow/ShopkeeperSlot/Content", "layout.tavern.shopkeeperContentGap", 2);
        ApplyMinimumSize(preparation + "/TavernShelf/ShelfRow/TavernBalanceSpacer", "layout.tavern.balanceSpacerWidth", 92, null, 0);

        ApplyMinimumSize(preparation + "/BoardStage", null, 0, "layout.board.minimumHeight", 250);
        ApplyMinimumSize(preparation + "/ReserveShelf", null, 0, "layout.reserve.minimumHeight", 72);

        ApplySeparation(preparation + "/HeroDock", "layout.heroDock.gap", 6);
        ApplyMinimumSize(preparation + "/HeroDock/HealthBadge", "layout.heroDock.healthBadge.width", 52, "layout.heroDock.healthBadge.height", 52);
        ApplyMinimumSize(preparation + "/HeroDock/HudArmorBadge", "layout.heroDock.armorBadge.width", 46, "layout.heroDock.armorBadge.height", 46);
        ApplyMinimumSize(preparation + "/HeroDock/HeroCore", "layout.heroDock.heroCoreWidth", 224, null, 0);
        ApplySeparation(preparation + "/HeroDock/HeroCore", "layout.heroDock.heroCoreGap", 2);
        ApplyMinimumSize(preparation + "/HeroDock/HeroCore/PowerButton", "layout.heroDock.powerButton.width", 136, "layout.heroDock.powerButton.height", 38);
        ApplyMinimumSize(preparation + "/HeroDock/CombineButton", "layout.heroDock.combineButton.width", 94, "layout.heroDock.combineButton.height", 40);
        ApplyMinimumSize(preparation + "/HeroDock/ResourceBadge", "layout.heroDock.resourceBadge.width", 58, "layout.heroDock.resourceBadge.height", 52);

        ApplyMinimumSize("Margin/Shell/TurnRail", "layout.turnRail.width", 92, null, 0);
        ApplySeparation("Margin/Shell/TurnRail/Content", "layout.turnRail.gap", 6);
        ApplyMinimumSize("Margin/Shell/TurnRail/Content/EndPreparationButton", null, 0, "layout.turnRail.endButtonHeight", 64);

        ApplyInteractionLayout();
    }

    private void ApplyInteractionLayout()
    {
        var panel = GetNodeOrNull<VBoxContainer>("InteractionPanel");
        if (panel is not null)
        {
            panel.AnchorLeft = ResolveThemeMetric("layout.interaction.anchorLeft", 0.24f, 0.0f, 1.0f);
            panel.AnchorTop = ResolveThemeMetric("layout.interaction.anchorTop", 0.20f, 0.0f, 1.0f);
            panel.AnchorRight = ResolveThemeMetric("layout.interaction.anchorRight", 0.76f, 0.0f, 1.0f);
            panel.AnchorBottom = ResolveThemeMetric("layout.interaction.anchorBottom", 0.80f, 0.0f, 1.0f);
        }

        if (GetNodeOrNull<MarginContainer>("InteractionPanel/InteractionSurface/InteractionMargin") is { } margin)
        {
            var horizontal = Mathf.RoundToInt(ResolveThemeMetric("layout.interaction.marginHorizontal", 12, 0, 512));
            var vertical = Mathf.RoundToInt(ResolveThemeMetric("layout.interaction.marginVertical", 10, 0, 512));
            margin.AddThemeConstantOverride("margin_left", horizontal);
            margin.AddThemeConstantOverride("margin_right", horizontal);
            margin.AddThemeConstantOverride("margin_top", vertical);
            margin.AddThemeConstantOverride("margin_bottom", vertical);
        }

        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent", "layout.interaction.contentGap", 6);
        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent/InteractionButtons", "layout.interaction.choiceGap", 4);
        ApplySeparation("InteractionPanel/InteractionSurface/InteractionMargin/InteractionContent/InteractionActions", "layout.interaction.actionGap", 8);
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
        row.Gap = ResolveThemeMetric(prefix + ".gap", row.Gap, 0.0f, 512.0f);
        row.PreferredCardWidth = ResolveThemeMetric(prefix + ".preferredCardWidth", row.PreferredCardWidth, 1.0f, 2048.0f);
        row.MinimumCardWidth = ResolveThemeMetric(prefix + ".minimumCardWidth", row.MinimumCardWidth, 1.0f, row.PreferredCardWidth);
        row.PreferredCardHeight = ResolveThemeMetric(prefix + ".preferredCardHeight", row.PreferredCardHeight, 1.0f, 2048.0f);
        row.Padding = ResolveThemeMetric(prefix + ".padding", row.Padding, 0.0f, 512.0f);
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
