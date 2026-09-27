using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps the presentation adapter's existing VBoxContainer contract while laying
/// dynamic card children out as a compact centered horizontal row. The container
/// owns only geometry; visual styling remains in the mod-driven theme layer.
/// </summary>
public partial class HorizontalCardRow : VBoxContainer
{
    [Export] public float Gap { get; set; } = 8.0f;
    [Export] public float PreferredCardWidth { get; set; } = 138.0f;
    [Export] public float MinimumCardWidth { get; set; } = 78.0f;
    [Export] public float PreferredCardHeight { get; set; } = 168.0f;
    [Export] public float Padding { get; set; } = 4.0f;

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

        var innerWidth = Mathf.Max(0.0f, Size.X - Padding * 2.0f);
        var gapsWidth = Gap * Mathf.Max(0, children.Length - 1);
        var availableCardsWidth = Mathf.Max(0.0f, innerWidth - gapsWidth);
        var fittedWidth = availableCardsWidth / children.Length;
        var cardWidth = Mathf.Min(PreferredCardWidth, fittedWidth);

        if (availableCardsWidth >= MinimumCardWidth * children.Length)
            cardWidth = Mathf.Max(MinimumCardWidth, cardWidth);

        var availableHeight = Mathf.Max(0.0f, Size.Y - Padding * 2.0f);
        var cardHeight = Mathf.Min(PreferredCardHeight, availableHeight);
        var totalWidth = cardWidth * children.Length + gapsWidth;
        var x = Mathf.Max(Padding, (Size.X - totalWidth) * 0.5f);
        var y = Mathf.Max(Padding, (Size.Y - cardHeight) * 0.5f);

        foreach (var child in children)
        {
            FitChildInRect(child, new Rect2(x, y, cardWidth, cardHeight));
            x += cardWidth + Gap;
        }
    }
}
