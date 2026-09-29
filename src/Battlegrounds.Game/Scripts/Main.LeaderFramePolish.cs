using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private Texture2D? _sharedCosmeticFrameTexture;

    private void RefreshLeaderFramePolish()
    {
        var frameTexture = ResolveSharedPortraitFrameTexture();
        if (frameTexture is null)
            return;

        ApplyShopkeeperSharedFrame(frameTexture);
        ApplyLocalLeaderFrame(frameTexture);
        ApplyOpponentLeaderFrames(frameTexture);
        ApplyLeaderSelectionFrames(frameTexture);
    }

    private Texture2D? ResolveSharedPortraitFrameTexture()
    {
        if (_sharedCosmeticFrameTexture is not null)
            return _sharedCosmeticFrameTexture;

        if (_modTheme is null ||
            _themeBuilder is null ||
            !_modTheme.Components.TryGetValue(ModThemePanelRoles.Shopkeeper, out var style) ||
            style is null ||
            string.IsNullOrWhiteSpace(style.BackgroundAsset))
        {
            return null;
        }

        _sharedCosmeticFrameTexture = _themeBuilder.LoadImage(style.BackgroundAsset);
        return _sharedCosmeticFrameTexture;
    }

    private void ApplyShopkeeperSharedFrame(Texture2D texture)
    {
        const string layerPath = "Margin/Shell/CenterStage/PreparationPanel/TavernControls/ControlsRow/ShopkeeperSlot/CosmeticLayer";
        var layer = GetNodeOrNull<Control>(layerPath);
        if (layer is null)
            return;

        if (layer.GetNodeOrNull<TextureRect>("CosmeticArt") is { } art)
            FramedCosmeticPortrait.ConfigureArt(art, TextureRect.StretchModeEnum.KeepAspectCovered);

        FramedCosmeticPortrait.ApplyFrame(
            layer,
            texture,
            FramedCosmeticPortrait.DefaultFrameNodeName,
            zIndex: 10);
    }

    private void ApplyLocalLeaderFrame(Texture2D texture)
    {
        if (!_hudBound)
            return;

        var frame = _hudHeroPortrait.GetParent() as Control;
        if (frame is null)
            return;

        FramedCosmeticPortrait.ConfigureArt(_hudHeroPortrait, TextureRect.StretchModeEnum.KeepAspectCovered);
        FramedCosmeticPortrait.ApplyFrame(frame, texture, "LeaderFrameOverlay", zIndex: 10);

        // Health/armor belong on top of the portrait frame, not underneath it.
        _hudHealthBadge.ZIndex = 20;
        _hudArmorBadge.ZIndex = 20;
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

                var art = panel.GetChildren().OfType<TextureRect>()
                    .FirstOrDefault(candidate => candidate.Name != "LeaderFrameOverlay");
                if (art is not null)
                    FramedCosmeticPortrait.ConfigureArt(art, TextureRect.StretchModeEnum.KeepAspectCovered);

                FramedCosmeticPortrait.ApplyFrame(panel, texture, "LeaderFrameOverlay");
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
            var art = Descendants<TextureRect>(card)
                .FirstOrDefault(candidate => candidate.Name != "LeaderFrameOverlay");
            if (art is null)
                continue;

            FramedCosmeticPortrait.ConfigureSquare(art, portraitSize);
            FramedCosmeticPortrait.ConfigureArt(art, TextureRect.StretchModeEnum.KeepAspectCovered);
            FramedCosmeticPortrait.ApplyFrame(art, texture, "LeaderFrameOverlay");
        }
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
