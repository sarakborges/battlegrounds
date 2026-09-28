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

        ApplyRowLayout(_leaderButtons, LayoutMetricKeys.LeaderRow);
        ApplyRowLayout(_offerButtons, LayoutMetricKeys.OfferRow);
        ApplyRowLayout(_fieldButtons, LayoutMetricKeys.FieldRow);
        ApplyRowLayout(_reserveButtons, LayoutMetricKeys.ReserveRow);
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
