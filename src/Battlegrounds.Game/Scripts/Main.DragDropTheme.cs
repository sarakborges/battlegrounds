using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    internal CursorShape PreparationDragCursor => CursorShape.PointingHand;
    internal CursorShape PreparationValidDropCursor => CursorShape.CanDrop;
    internal CursorShape PreparationInvalidDropCursor => CursorShape.Forbidden;

    internal float PreparationDragSourceOpacity
    {
        get
        {
            if (_modTheme?.Components.TryGetValue(ModThemeInteractionRoles.DragPreview, out var style) == true && style.Opacity is double opacity)
                return Mathf.Clamp((float)opacity, 0.0f, 1.0f);

            throw new InvalidOperationException(
                $"Resolved presentation theme is missing required '{ModThemeInteractionRoles.DragPreview}.opacity'.");
        }
    }

    internal float PreparationDragPreviewScale =>
        ResolveThemeMetric(ModThemeMetricKeys.Drag.PreviewScale, 0.5f, 2.0f);

    internal float PreparationDragPreviewRotationDegrees =>
        ResolveThemeMetric(ModThemeMetricKeys.Drag.PreviewRotationDegrees, -45.0f, 45.0f);

    internal float PreparationDropTargetPadding
    {
        get
        {
            if (TryResolveDropTargetPadding(ModThemeInteractionRoles.DropTargetValid, out var padding) ||
                TryResolveDropTargetPadding(ModThemeInteractionRoles.DropTargetInvalid, out padding))
                return padding;

            throw new InvalidOperationException("Resolved presentation theme is missing drop-target padding.");
        }
    }

    internal StyleBoxFlat BuildPreparationDropTargetStyle(bool valid, bool active)
    {
        var role = (valid, active) switch
        {
            (true, true) => ModThemeInteractionRoles.DropTargetValidActive,
            (true, false) => ModThemeInteractionRoles.DropTargetValid,
            (false, true) => ModThemeInteractionRoles.DropTargetInvalidActive,
            _ => ModThemeInteractionRoles.DropTargetInvalid,
        };

        var style = RequireInteractionStyle(role);
        var opacity = Mathf.Clamp((float)(style.Opacity ?? 1.0), 0.0f, 1.0f);

        if (!ResolveThemeColor(style.BackgroundColor, out var background))
            throw new InvalidOperationException($"Resolved presentation theme is missing required '{role}.backgroundColor'.");
        if (!ResolveThemeColor(style.BorderColor, out var border))
            throw new InvalidOperationException($"Resolved presentation theme is missing required '{role}.borderColor'.");
        if (style.BorderWidth is not int width)
            throw new InvalidOperationException($"Resolved presentation theme is missing required '{role}.borderWidth'.");
        if (style.Radius is not string radiusToken ||
            _modTheme?.Radii.TryGetValue(radiusToken, out var radius) != true)
            throw new InvalidOperationException($"Resolved presentation theme is missing required '{role}.radius'.");

        var box = new StyleBoxFlat
        {
            BgColor = new Color(background, background.A * opacity),
            BorderColor = new Color(border, border.A * opacity),
            BorderWidthLeft = width,
            BorderWidthTop = width,
            BorderWidthRight = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = new Color(border, border.A * opacity),
            ShadowSize = Mathf.RoundToInt(width * ResolveThemeMetric(ModThemeMetricKeys.Drag.DropTargetShadowScale, 0.0f, 16.0f)),
            ShadowOffset = Vector2.Zero,
        };
        return box;
    }

    private ModThemeStyle RequireInteractionStyle(string role)
    {
        if (_modTheme is not null && _modTheme.Components.TryGetValue(role, out var style))
            return style;

        throw new InvalidOperationException($"Resolved presentation theme is missing required component role '{role}'.");
    }

    private bool TryResolveDropTargetPadding(string role, out float padding)
    {
        padding = 0;
        var theme = _modTheme;
        if (theme is null ||
            !theme.Components.TryGetValue(role, out var style) ||
            style.Padding?.Horizontal is not string token ||
            !theme.Spacing.TryGetValue(token, out var spacing))
            return false;

        padding = spacing;
        return true;
    }

    private bool ResolveThemeColor(string? value, out Color color)
    {
        color = default;
        if (_modTheme is null || !_modTheme.TryResolveColor(value, out var resolved))
            return false;
        color = Color.FromHtml(resolved);
        return true;
    }
}
