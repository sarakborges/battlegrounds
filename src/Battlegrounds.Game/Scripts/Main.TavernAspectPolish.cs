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

        var controlTexture = ResolveTavernControlTexture();
        ApplyTavernControlAspect(
            controlsRow,
            _upgradeButton,
            ModThemeMetricKeys.Layout.TavernUpgradeButtonWidth,
            controlTexture);
        ApplyTavernControlAspect(
            controlsRow,
            _refreshButton,
            ModThemeMetricKeys.Layout.TavernRefreshButtonWidth,
            controlTexture);
        ApplyTavernControlAspect(
            controlsRow,
            _freezeButton,
            ModThemeMetricKeys.Layout.TavernFreezeButtonWidth,
            controlTexture);

        if (controlsRow.GetNodeOrNull<Control>("TierBadge") is { } tierBadge)
            ShrinkVertically(tierBadge);

        if (controlsRow.GetNodeOrNull<PanelContainer>("ShopkeeperSlot") is { } shopkeeper)
            ApplyShopkeeperAspect(shopkeeper);

        ApplyCompactTavernRow(_offerButtons);
        if (_tavernActionOffers is not null)
            ApplyCompactTavernRow(_tavernActionOffers);

        if (_hudBound)
            ApplyCompactResourceBadge();
    }

    private Texture2D? ResolveTavernControlTexture()
    {
        if (_modTheme is null ||
            _themeBuilder is null ||
            !_modTheme.Components.TryGetValue(ModThemeComponentRoles.ButtonTavernAction, out var style) ||
            style is null ||
            string.IsNullOrWhiteSpace(style.BackgroundAsset))
        {
            return null;
        }

        return _themeBuilder.LoadImage(style.BackgroundAsset);
    }

    private void ApplyTavernControlAspect(
        HBoxContainer controlsRow,
        Button button,
        string widthMetricKey,
        Texture2D? texture)
    {
        var width = ResolvePresentationMetric(widthMetricKey, 1.0f, 1024.0f);
        var height = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.TavernControlHeight,
            1.0f,
            1024.0f);
        if (texture is not null && texture.GetWidth() > 0)
            height = width * texture.GetHeight() / texture.GetWidth();

        var slotName = button.Name + "AspectSlot";
        var slot = controlsRow.GetNodeOrNull<Control>(slotName);
        if (slot is null)
        {
            var index = button.GetIndex();
            controlsRow.RemoveChild(button);
            slot = new Control
            {
                Name = slotName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            controlsRow.AddChild(slot);
            controlsRow.MoveChild(slot, index);
            slot.AddChild(button);
            button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        slot.CustomMinimumSize = new Vector2(width, height);
        slot.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        slot.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        button.CustomMinimumSize = Vector2.Zero;
    }

    private void ApplyCompactTavernRow(HorizontalCardRow row)
    {
        var preferredHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardHeight(ModThemeMetricKeys.Row.Offer),
            1.0f,
            2048.0f);
        var padding = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.Padding(ModThemeMetricKeys.Row.Offer),
            0.0f,
            512.0f);

        row.CustomMinimumSize = new Vector2(
            row.CustomMinimumSize.X,
            preferredHeight + (padding * 2.0f));
        row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.QueueSort();
    }

    private static void ShrinkVertically(Control control)
    {
        control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }

    private void ApplyShopkeeperAspect(PanelContainer shopkeeper)
    {
        var configuredWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.TavernShopkeeperWidth,
            1.0f,
            2048.0f);
        var offerWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardWidth(ModThemeMetricKeys.Row.Offer),
            1.0f,
            2048.0f);
        var width = Mathf.Max(configuredWidth, offerWidth * 1.75f);
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
        layer.ClipContents = true;

        if (layer.GetNodeOrNull<TextureRect>("CosmeticFrame") is { } frame)
        {
            frame.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            if (frameTexture is not null)
                frame.Texture = frameTexture;
        }

        if (layer.GetNodeOrNull<TextureRect>("CosmeticArt") is { } art)
        {
            // Bartender cosmetics are portrait-oriented while the Tavern opening is
            // landscape. Contain the full cosmetic instead of center-cropping the
            // head/shoulders; the foreground frame masks the unused side area.
            art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            art.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
    }

    private void ApplyCompactResourceBadge()
    {
        var height = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockResourceBadgeHeight,
            1.0f,
            512.0f);

        _hudResourceBadge.CustomMinimumSize = new Vector2(0.0f, height);
        _hudResourceBadge.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _hudResourceBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
    }
}
