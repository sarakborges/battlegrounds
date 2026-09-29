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
        // Older builds wrapped every button in an aspect slot. Runtime invariant
        // repair then moved the button back out, so both systems fought every frame.
        // Unwrap any legacy slot once and let the button own its footprint directly.
        var slotName = button.Name + "AspectSlot";
        if (controlsRow.GetNodeOrNull<Control>(slotName) is { } slot)
        {
            if (button.GetParent() == slot)
            {
                slot.RemoveChild(button);
                controlsRow.AddChild(button);
                controlsRow.MoveChild(button, slot.GetIndex());
            }
            slot.QueueFree();
        }

        if (button.GetParent() != controlsRow)
        {
            button.GetParent()?.RemoveChild(button);
            controlsRow.AddChild(button);
        }

        var width = ResolvePresentationMetric(widthMetricKey, 1.0f, 1024.0f);
        var height = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.TavernControlHeight,
            1.0f,
            1024.0f);
        if (texture is not null && texture.GetWidth() > 0)
            height = width * texture.GetHeight() / texture.GetWidth();

        button.CustomMinimumSize = new Vector2(width, height);
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
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

        // The requested shopkeeper presentation is a square frame at 75% of the
        // original configured footprint. Do not let offer-card width inflate it.
        var side = configuredWidth * 0.75f;
        var size = new Vector2(side, side);
        shopkeeper.CustomMinimumSize = size;
        shopkeeper.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        shopkeeper.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        shopkeeper.ClipContents = false;

        if (shopkeeper.GetNodeOrNull<Control>("CosmeticLayer") is not { } layer)
            return;

        FramedCosmeticPortrait.ConfigureSquare(layer, side);
        if (layer.GetNodeOrNull<TextureRect>("CosmeticArt") is { } art)
            FramedCosmeticPortrait.ConfigureArt(art, TextureRect.StretchModeEnum.KeepAspectCovered);

        if (ResolveSharedPortraitFrameTexture() is { } frameTexture)
            FramedCosmeticPortrait.ApplyFrame(layer, frameTexture);
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
