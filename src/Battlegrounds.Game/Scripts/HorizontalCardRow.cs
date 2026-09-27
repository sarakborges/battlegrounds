using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps the presentation adapter's existing VBoxContainer contract while laying
/// dynamic card children out as a centered horizontal row. This is intentionally
/// structure-only: card visuals continue to come from the mod presentation/theme layer.
/// </summary>
public partial class HorizontalCardRow : VBoxContainer
{
    [Export] public float Gap { get; set; } = 10.0f;
    [Export] public float PreferredCardWidth { get; set; } = 176.0f;
    [Export] public float MinimumCardWidth { get; set; } = 92.0f;

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren)
            return;

        var children = GetChildren()
            .OfType<Control>()
            .Where(child => child.Visible && !child.IsQueuedForDeletion())
            .ToArray();

        if (children.Length == 0)
            return;

        var availableWidth = Mathf.Max(0.0f, Size.X - Gap * (children.Length - 1));
        var fitWidth = children.Length == 0 ? PreferredCardWidth : availableWidth / children.Length;
        var cardWidth = Mathf.Min(PreferredCardWidth, fitWidth);
        if (availableWidth >= MinimumCardWidth * children.Length)
            cardWidth = Mathf.Max(MinimumCardWidth, cardWidth);

        var totalWidth = cardWidth * children.Length + Gap * (children.Length - 1);
        var x = Mathf.Max(0.0f, (Size.X - totalWidth) * 0.5f);
        var rowHeight = Mathf.Max(0.0f, Size.Y);

        foreach (var child in children)
        {
            FitChildInRect(child, new Rect2(x, 0.0f, cardWidth, rowHeight));
            x += cardWidth + Gap;
        }
    }
}
