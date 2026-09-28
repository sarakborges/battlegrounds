using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private const string DragPreviewRole = "drag.preview";
    private const string DropTargetValidRole = "dropTarget.valid";
    private const string DropTargetValidActiveRole = "dropTarget.valid.active";
    private const string DropTargetInvalidRole = "dropTarget.invalid";
    private const string DropTargetInvalidActiveRole = "dropTarget.invalid.active";

    internal CursorShape PreparationDragCursor => CursorShape.PointingHand;
    internal CursorShape PreparationValidDropCursor => CursorShape.CanDrop;
    internal CursorShape PreparationInvalidDropCursor => CursorShape.Forbidden;

    internal float PreparationDragSourceOpacity
    {
        get
        {
            if (_modTheme?.Components.TryGetValue(DragPreviewRole, out var style) == true && style.Opacity is double opacity)
                return Mathf.Clamp((float)opacity, 0.0f, 1.0f);
            return 0.12f;
        }
    }

    internal float PreparationDragPreviewScale => 1.045f;
    internal float PreparationDragPreviewRotationDegrees => -1.5f;

    internal float PreparationDropTargetPadding
    {
        get
        {
            if (TryResolveDropTargetPadding(DropTargetValidRole, out var padding) ||
                TryResolveDropTargetPadding(DropTargetInvalidRole, out padding))
                return padding;
            if (_modTheme?.Spacing.TryGetValue("sm", out var spacing) == true)
                return spacing;
            return 8.0f;
        }
    }

    internal StyleBoxFlat BuildPreparationDropTargetStyle(bool valid, bool active)
    {
        var role = (valid, active) switch
        {
            (true, true) => DropTargetValidActiveRole,
            (true, false) => DropTargetValidRole,
            (false, true) => DropTargetInvalidActiveRole,
            _ => DropTargetInvalidRole,
        };

        ModThemeStyle? style = null;
        if (_modTheme is not null)
            _modTheme.Components.TryGetValue(role, out style);

        var box = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = valid
                ? new Color(0.45f, 0.80f, 0.42f, active ? 0.95f : 0.72f)
                : new Color(0.90f, 0.30f, 0.30f, active ? 0.95f : 0.72f),
            BorderWidthLeft = active ? 3 : 2,
            BorderWidthTop = active ? 3 : 2,
            BorderWidthRight = active ? 3 : 2,
            BorderWidthBottom = active ? 3 : 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
        };

        if (style is not null)
        {
            var opacity = Mathf.Clamp((float)(style.Opacity ?? 1.0), 0.0f, 1.0f);
            if (ResolveThemeColor(style.BackgroundColor, out var background))
                box.BgColor = new Color(background, background.A * opacity);
            if (ResolveThemeColor(style.BorderColor, out var border))
                box.BorderColor = new Color(border, border.A * opacity);
            if (style.BorderWidth is int width)
            {
                box.BorderWidthLeft = width;
                box.BorderWidthTop = width;
                box.BorderWidthRight = width;
                box.BorderWidthBottom = width;
            }
            if (style.Radius is not null && _modTheme?.Radii.TryGetValue(style.Radius, out var radius) == true)
            {
                box.CornerRadiusTopLeft = radius;
                box.CornerRadiusTopRight = radius;
                box.CornerRadiusBottomLeft = radius;
                box.CornerRadiusBottomRight = radius;
            }
        }

        box.ShadowColor = box.BorderColor;
        box.ShadowSize = Math.Max(1, box.BorderWidthLeft * 3);
        box.ShadowOffset = Vector2.Zero;
        return box;
    }

    private bool TryResolveDropTargetPadding(string role, out float padding)
    {
        padding = 0;
        if (_modTheme?.Components.TryGetValue(role, out var style) != true ||
            style.Padding?.Horizontal is not string token ||
            !_modTheme.Spacing.TryGetValue(token, out var spacing))
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
