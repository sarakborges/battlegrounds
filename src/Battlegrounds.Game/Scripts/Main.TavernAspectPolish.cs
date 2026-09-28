using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private void ApplyTavernAspectPolish()
    {
        const string controlsPath = "Margin/Shell/CenterStage/PreparationPanel/TavernControls/ControlsRow";
        var controlsRow = GetNodeOrNull<HBoxContainer>(controlsPath);
        if (controlsRow is null)
            return;

        ShrinkVertically(_upgradeButton);
        ShrinkVertically(_refreshButton);
        ShrinkVertically(_freezeButton);

        if (controlsRow.GetNodeOrNull<Control>("TierBadge") is { } tierBadge)
            ShrinkVertically(tierBadge);

        if (controlsRow.GetNodeOrNull<PanelContainer>("ShopkeeperSlot") is { } shopkeeper)
            ApplyShopkeeperAspect(shopkeeper);

        ApplyCompactCardRowHeight(_offerButtons, ModThemeMetricKeys.Row.Offer);
        if (_tavernActionOffers is not null)
            ApplyCompactCardRowHeight(_tavernActionOffers, ModThemeMetricKeys.Row.Offer);

        if (_hudBound)
            ApplyCompactResourceBadge();
    }

    private static void ShrinkVertically(Control control)
    {
        control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }

    private void ApplyCompactCardRowHeight(HorizontalCardRow row, string metricPrefix)
    {
        var preferredHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardHeight(metricPrefix),
            1.0f,
            2048.0f);
        var padding = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.Padding(metricPrefix),
            0.0f,
            512.0f);

        row.CustomMinimumSize = new Vector2(
            row.CustomMinimumSize.X,
            preferredHeight + padding * 2.0f);
        row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.QueueSort();
    }

    private void ApplyShopkeeperAspect(PanelContainer shopkeeper)
    {
        var width = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.TavernShopkeeperWidth,
            1.0f,
            2048.0f);
        var height = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.TavernControlsMinimumHeight,
            1.0f,
            2048.0f);

        Texture2D? frameTexture = null;
        if (_modTheme is not null &&
            _themeBuilder is not null &&
            _modTheme.Components.TryGetValue(ModThemePanelRoles.Shopkeeper, out var style) &&
            style is not null &&
            !string.IsNullOrWhiteSpace(style.BackgroundAsset))
        {
            frameTexture = _themeBuilder.LoadImage(style.BackgroundAsset);
            if (frameTexture is not null && frameTexture.GetWidth() > 0)
                height = width * frameTexture.GetHeight() / frameTexture.GetWidth();
        }

        var size = new Vector2(width, height);
        shopkeeper.CustomMinimumSize = size;
        shopkeeper.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        shopkeeper.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        if (shopkeeper.GetNodeOrNull<Control>("CosmeticLayer") is not { } layer)
            return;

        layer.CustomMinimumSize = size;

        if (layer.GetNodeOrNull<TextureRect>("CosmeticFrame") is { } frame)
        {
            frame.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            if (frameTexture is not null)
                frame.Texture = frameTexture;
        }

        if (layer.GetNodeOrNull<TextureRect>("CosmeticArt") is { } art)
            art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
    }

    private void ApplyCompactResourceBadge()
    {
        var height = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockResourceBadgeHeight,
            1.0f,
            512.0f);

        _hudResourceBadge.CustomMinimumSize = new Vector2(0.0f, height);
        _hudResourceBadge.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _hudResourceBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }
}
