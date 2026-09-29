using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private void RefreshOpponentRailReferencePolish()
    {
        if (!_hudBound)
            return;

        var configuredPortraitSize = ResolvePresentationMetric(
            Battlegrounds.Content.ModThemeMetricKeys.Hud.OpponentPortraitSize,
            1.0f,
            512.0f);
        var portraitSize = Mathf.Min(configuredPortraitSize, 42.0f);
        var configuredRankWidth = ResolvePresentationMetric(
            Battlegrounds.Content.ModThemeMetricKeys.Hud.OpponentRankWidth,
            1.0f,
            256.0f);
        var rankWidth = Mathf.Min(configuredRankWidth, 18.0f);
        var configuredMarginHorizontal = ResolvePresentationMetric(
            Battlegrounds.Content.ModThemeMetricKeys.Hud.OpponentMarginHorizontal,
            0.0f,
            256.0f);
        var marginHorizontal = Mathf.Min(configuredMarginHorizontal, 4.0f);
        var identityWidth = Mathf.Clamp(portraitSize * 1.42f, 54.0f, 64.0f);

        var rail = GetNodeOrNull<PanelContainer>("Margin/Shell/OpponentRail") ??
                   GetNodeOrNull<PanelContainer>("ReferenceBoardOverlay/OpponentRail");
        if (rail is not null && rail.GetParent()?.Name.ToString() != "ReferenceBoardOverlay")
        {
            rail.CustomMinimumSize = new Vector2(
                rankWidth + portraitSize + identityWidth + marginHorizontal * 2.0f + 10.0f,
                0.0f);
            rail.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        }

        foreach (var entry in _opponentEntries.GetChildren().OfType<PanelContainer>())
        {
            entry.CustomMinimumSize = new Vector2(0, portraitSize + 12.0f);
            var row = Descendants<HBoxContainer>(entry).FirstOrDefault();
            if (row is null)
                continue;

            row.AddThemeConstantOverride("separation", 2);

            var rankLabel = row.GetChildren().OfType<Label>().FirstOrDefault();
            if (rankLabel is not null)
                rankLabel.CustomMinimumSize = new Vector2(rankWidth, 0);

            var portraitFrame = Descendants<PanelContainer>(entry)
                .FirstOrDefault(panel => string.Equals(
                    panel.ThemeTypeVariation.ToString(),
                    "OpponentPortraitFrame",
                    StringComparison.Ordinal));
            if (portraitFrame is null)
                continue;

            FramedCosmeticPortrait.ConfigureSquare(portraitFrame, portraitSize);
            EnsureOpponentPortraitBadges(entry, portraitFrame, portraitSize);

            var columns = row.GetChildren().OfType<VBoxContainer>().ToArray();
            if (columns.Length > 0)
            {
                var identity = columns[0];
                identity.CustomMinimumSize = new Vector2(identityWidth, 0.0f);
                identity.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                var labels = identity.GetChildren().OfType<Label>().ToArray();
                if (labels.Length > 0)
                {
                    labels[0].ThemeTypeVariation = "CaptionLabel";
                    labels[0].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                }
                if (labels.Length > 1)
                    labels[1].Visible = false;
            }

            if (columns.Length > 1)
                columns[^1].Visible = false;
        }
    }

    private static void EnsureOpponentPortraitBadges(
        PanelContainer entry,
        PanelContainer portraitFrame,
        float portraitSize)
    {
        var stats = Descendants<VBoxContainer>(entry).LastOrDefault();
        var statLabels = stats?.GetChildren().OfType<Label>().ToArray() ?? [];
        if (statLabels.Length < 2)
            return;

        var overlay = portraitFrame.GetNodeOrNull<Control>("OpponentBadgeOverlay");
        if (overlay is null)
        {
            overlay = new Control
            {
                Name = "OpponentBadgeOverlay",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 20,
            };
            portraitFrame.AddChild(overlay);
            overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        var badgeSize = Mathf.Clamp(portraitSize * 0.40f, 17.0f, 23.0f);
        var health = EnsureOpponentBadge(overlay, "Health", "HealthBadge", right: true, bottom: true, badgeSize);
        var tier = EnsureOpponentBadge(overlay, "Tier", "TierBadge", right: false, bottom: false, badgeSize);

        health.GetNode<Label>("Value").Text = statLabels[0].Text;
        tier.GetNode<Label>("Value").Text = statLabels[1].Text;
    }

    private static PanelContainer EnsureOpponentBadge(
        Control overlay,
        string name,
        string variation,
        bool right,
        bool bottom,
        float size)
    {
        var badge = overlay.GetNodeOrNull<PanelContainer>(name);
        if (badge is null)
        {
            badge = new PanelContainer
            {
                Name = name,
                ThemeTypeVariation = variation,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var value = new Label
            {
                Name = "Value",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            badge.AddChild(value);
            overlay.AddChild(badge);
        }

        badge.AnchorLeft = right ? 1.0f : 0.0f;
        badge.AnchorRight = right ? 1.0f : 0.0f;
        badge.AnchorTop = bottom ? 1.0f : 0.0f;
        badge.AnchorBottom = bottom ? 1.0f : 0.0f;
        badge.OffsetLeft = right ? -size : 0.0f;
        badge.OffsetRight = right ? 0.0f : size;
        badge.OffsetTop = bottom ? -size : 0.0f;
        badge.OffsetBottom = bottom ? 0.0f : size;
        badge.CustomMinimumSize = new Vector2(size, size);
        return badge;
    }
}
