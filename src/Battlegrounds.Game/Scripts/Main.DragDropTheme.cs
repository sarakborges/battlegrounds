using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private const string DragPreviewRole = "drag.preview";
    private const string DropTargetRole = "dropTarget";
    private const string DropTargetActiveRole = "dropTarget.active";

    internal CursorShape PreparationDragCursor => CursorShape.PointingHand;
    internal CursorShape PreparationValidDropCursor => CursorShape.CanDrop;

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
            if (_modTheme?.Spacing.TryGetValue("sm", out var spacing) == true)
                return spacing;
            return 8.0f;
        }
    }

    internal StyleBoxFlat BuildPreparationDropTargetStyle(bool active)
    {
        var role = active ? DropTargetActiveRole : DropTargetRole;
        ModThemeStyle? style = null;
        if (_modTheme is not null)
            _modTheme.Components.TryGetValue(role, out style);

        var box = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = active
                ? new Color(1.0f, 0.82f, 0.38f, 0.95f)
                : new Color(0.45f, 0.80f, 0.42f, 0.75f),
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

        var glow = box.BorderColor;
        box.ShadowColor = new Color(glow, active ? glow.A * 0.75f : glow.A * 0.5f);
        box.ShadowSize = Math.Max(active ? 8 : 5, box.BorderWidthLeft * 3);
        box.ShadowOffset = Vector2.Zero;
        return box;
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
