using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps dynamic presentation cards in a compact centered horizontal row while the
/// surrounding scene owns the row height. The row reports no content-driven minimum
/// height so cards never inflate the vertical HUD by being interpreted as a VBox stack.
///
/// The Field row configures native Godot drag/drop on its cards. Reordering still
/// submits ordinary Core commands through Main; visual child order is not authoritative.
/// </summary>
public partial class HorizontalCardRow : VBoxContainer
{
    [Export] public float Gap { get; set; } = 8.0f;
    [Export] public float PreferredCardWidth { get; set; } = 138.0f;
    [Export] public float MinimumCardWidth { get; set; } = 78.0f;
    [Export] public float PreferredCardHeight { get; set; } = 168.0f;
    [Export] public float Padding { get; set; } = 4.0f;

    private bool IsFieldRow => Name == "FieldButtons";

    public override void _Ready()
    {
        ApplySemanticGeometry();
        SetProcess(IsFieldRow);
        QueueSort();
    }

    public override Vector2 _GetMinimumSize() => Vector2.Zero;

    public override void _Process(double delta)
    {
        if (!IsFieldRow) return;
        ConfigureFieldDragAndDrop();
    }

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

    private void ApplySemanticGeometry()
    {
        switch (Name.ToString())
        {
            case "LeaderButtons":
                Gap = 18.0f;
                PreferredCardWidth = 164.0f;
                MinimumCardWidth = 116.0f;
                PreferredCardHeight = 200.0f;
                Padding = 8.0f;
                break;
            case "OfferButtons":
                Gap = 10.0f;
                PreferredCardWidth = 124.0f;
                MinimumCardWidth = 88.0f;
                PreferredCardHeight = 142.0f;
                Padding = 4.0f;
                break;
            case "FieldButtons":
                Gap = 8.0f;
                PreferredCardWidth = 152.0f;
                MinimumCardWidth = 102.0f;
                PreferredCardHeight = 204.0f;
                Padding = 6.0f;
                break;
            case "ReserveButtons":
                Gap = 4.0f;
                PreferredCardWidth = 96.0f;
                MinimumCardWidth = 70.0f;
                PreferredCardHeight = 70.0f;
                Padding = 2.0f;
                break;
        }
    }

    private void ConfigureFieldDragAndDrop()
    {
        var main = FindMain();
        if (main is null) return;

        var cards = GetChildren()
            .OfType<PresentationCardButton>()
            .Where(card => !card.IsQueuedForDeletion())
            .ToArray();
        var enabled = main.CanReorderHumanField && cards.Length > 1;

        for (var index = 0; index < cards.Length; index++)
            cards[index].ConfigureFieldDrag(index, enabled, main.ReorderHumanField);
    }

    private Main? FindMain()
    {
        Node? node = GetParent();
        while (node is not null)
        {
            if (node is Main main) return main;
            node = node.GetParent();
        }
        return null;
    }
}
