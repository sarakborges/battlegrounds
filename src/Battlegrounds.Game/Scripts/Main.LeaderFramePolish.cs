using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private Texture2D? _leaderFrameTexture;

    private void RefreshLeaderFramePolish()
    {
        var frameTexture = ResolveSharedPortraitFrameTexture();
        if (frameTexture is null)
            return;

        ApplyLocalLeaderFrame(frameTexture);
        ApplyOpponentLeaderFrames(frameTexture);
        ApplyLeaderSelectionFrames(frameTexture);
    }

    private Texture2D? ResolveSharedPortraitFrameTexture()
    {
        if (_leaderFrameTexture is not null)
            return _leaderFrameTexture;

        if (_modTheme is null ||
            _themeBuilder is null ||
            !_modTheme.Components.TryGetValue(ModThemePanelRoles.Shopkeeper, out var style) ||
            style is null ||
            string.IsNullOrWhiteSpace(style.BackgroundAsset))
        {
            return null;
        }

        _leaderFrameTexture = _themeBuilder.LoadImage(style.BackgroundAsset);
        return _leaderFrameTexture;
    }

    private void ApplyLocalLeaderFrame(Texture2D texture)
    {
        const string framePath = "Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroDockRow/HeroPortraitCluster/HeroPortraitFrame";
        var frame = GetNodeOrNull<PanelContainer>(framePath);
        if (frame is null)
            return;

        EnsureFrameOverlay(frame, texture, "LeaderFrameOverlay");
    }

    private void ApplyOpponentLeaderFrames(Texture2D texture)
    {
        if (!_hudBound)
            return;

        foreach (var entry in _opponentEntries.GetChildren())
        {
            foreach (var panel in Descendants<PanelContainer>(entry))
            {
                if (!string.Equals(
                        panel.ThemeTypeVariation.ToString(),
                        "OpponentPortraitFrame",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                EnsureFrameOverlay(panel, texture, "LeaderFrameOverlay");
            }
        }
    }

    private void ApplyLeaderSelectionFrames(Texture2D texture)
    {
        if (!_leaderPanel.Visible)
            return;

        var portraitSize = ResolvePresentationMetric(
            ModThemeMetricKeys.Card.MinimumWidth(ModThemeMetricKeys.Card.LeaderRole),
            1.0f,
            2048.0f);

        foreach (var card in _leaderButtons.GetChildren().OfType<PresentationCardButton>())
        {
            var art = Descendants<TextureRect>(card).FirstOrDefault();
            if (art is null)
                continue;

            art.CustomMinimumSize = new Vector2(portraitSize, portraitSize);
            art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            art.ClipContents = true;
            EnsureFrameOverlay(art, texture, "LeaderFrameOverlay");
        }
    }

    private static void EnsureFrameOverlay(Control host, Texture2D texture, string name)
    {
        var overlay = host.GetNodeOrNull<TextureRect>(name);
        if (overlay is null)
        {
            overlay = new TextureRect
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ZIndex = 10,
            };
            host.AddChild(overlay);
            overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        overlay.Texture = texture;
    }

    private static IEnumerable<T> Descendants<T>(Node root)
        where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T typed)
                yield return typed;

            foreach (var descendant in Descendants<T>(child))
                yield return descendant;
        }
    }
}
