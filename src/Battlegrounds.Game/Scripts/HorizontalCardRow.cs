using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps dynamic presentation cards in a compact centered horizontal row while the
/// surrounding scene owns the row height. The row reports no content-driven minimum
/// height so cards never inflate the vertical HUD.
///
/// Tavern offers and field pieces are drag sources. Field reorder destinations are
/// explicit insertion gaps between pieces, including the leading and trailing edges.
/// </summary>
public partial class HorizontalCardRow : VBoxContainer
{
    [Export] public float Gap { get; set; } = 8.0f;
    [Export] public float PreferredCardWidth { get; set; } = 138.0f;
    [Export] public float MinimumCardWidth { get; set; } = 78.0f;
    [Export] public float PreferredCardHeight { get; set; } = 168.0f;
    [Export] public float Padding { get; set; } = 4.0f;

    private bool IsOfferRow => Name == "OfferButtons";
    private bool IsFieldRow => Name == "FieldButtons";

    public override void _Ready()
    {
        ApplySemanticGeometry();
        SetProcess(IsOfferRow || IsFieldRow);
        QueueSort();
    }

    public override Vector2 _GetMinimumSize() => Vector2.Zero;

    public override void _Process(double delta)
    {
        if (IsOfferRow)
            ConfigureOfferDrag();
        if (IsFieldRow)
            ConfigureFieldDragAndDrop();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren)
            return;

        var cards = CurrentCards();
        if (cards.Length == 0)
            return;

        var innerWidth = Mathf.Max(0.0f, Size.X - Padding * 2.0f);
        var gapsWidth = Gap * Mathf.Max(0, cards.Length - 1);
        var availableCardsWidth = Mathf.Max(0.0f, innerWidth - gapsWidth);
        var fittedWidth = availableCardsWidth / cards.Length;
        var cardWidth = Mathf.Min(PreferredCardWidth, fittedWidth);

        if (availableCardsWidth >= MinimumCardWidth * cards.Length)
            cardWidth = Mathf.Max(MinimumCardWidth, cardWidth);

        var availableHeight = Mathf.Max(0.0f, Size.Y - Padding * 2.0f);
        var cardHeight = Mathf.Min(PreferredCardHeight, availableHeight);
        var totalWidth = cardWidth * cards.Length + gapsWidth;
        var x = Mathf.Max(Padding, (Size.X - totalWidth) * 0.5f);
        var y = Mathf.Max(Padding, (Size.Y - cardHeight) * 0.5f);

        foreach (var card in cards)
        {
            FitChildInRect(card, new Rect2(x, y, cardWidth, cardHeight));
            x += cardWidth + Gap;
        }

        if (IsFieldRow)
            LayoutInsertionZones(cards, y, cardHeight);
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
                Gap = 18.0f;
                PreferredCardWidth = 148.0f;
                MinimumCardWidth = 98.0f;
                PreferredCardHeight = 204.0f;
                Padding = 10.0f;
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

    private void ConfigureOfferDrag()
    {
        var main = FindMain();
        if (main is null)
            return;

        var cards = CurrentCards();
        for (var index = 0; index < cards.Length; index++)
            cards[index].ConfigureOfferDrag(index, main.CanUsePreparationDrag && !cards[index].Disabled);
    }

    private void ConfigureFieldDragAndDrop()
    {
        var main = FindMain();
        if (main is null)
            return;

        var cards = CurrentCards();
        var reorderEnabled = main.CanReorderHumanField && cards.Length > 1;

        for (var index = 0; index < cards.Length; index++)
            cards[index].ConfigureFieldDrag(index, main.CanUsePreparationDrag && !cards[index].Disabled);

        SyncInsertionZones(cards.Length, reorderEnabled, main.ReorderHumanFieldAtInsertion);
        QueueSort();
    }

    private void SyncInsertionZones(int cardCount, bool enabled, Action<int, int> dropHandler)
    {
        var zones = GetChildren()
            .OfType<FieldInsertionDropZone>()
            .Where(zone => !zone.IsQueuedForDeletion())
            .ToList();
        var required = cardCount + 1;

        while (zones.Count < required)
        {
            var zone = new FieldInsertionDropZone();
            AddChild(zone);
            zones.Add(zone);
        }

        for (var index = 0; index < zones.Count; index++)
        {
            if (index >= required)
            {
                zones[index].QueueFree();
                continue;
            }

            zones[index].Configure(index, enabled, dropHandler);
        }
    }

    private void LayoutInsertionZones(PresentationCardButton[] cards, float y, float cardHeight)
    {
        var zones = GetChildren()
            .OfType<FieldInsertionDropZone>()
            .Where(zone => !zone.IsQueuedForDeletion())
            .OrderBy(zone => zone.GetIndex())
            .ToArray();
        if (zones.Length < cards.Length + 1)
            return;

        var zoneHeight = Mathf.Max(32.0f, cardHeight - 24.0f);
        var zoneY = y + (cardHeight - zoneHeight) * 0.5f;

        for (var insertion = 0; insertion <= cards.Length; insertion++)
        {
            float left;
            float right;
            if (insertion == 0)
            {
                right = cards[0].Position.X;
                left = Mathf.Max(0.0f, right - Gap);
            }
            else if (insertion == cards.Length)
            {
                left = cards[^1].Position.X + cards[^1].Size.X;
                right = Mathf.Min(Size.X, left + Gap);
            }
            else
            {
                left = cards[insertion - 1].Position.X + cards[insertion - 1].Size.X;
                right = cards[insertion].Position.X;
            }

            var width = Mathf.Max(8.0f, right - left);
            FitChildInRect(zones[insertion], new Rect2(left, zoneY, width, zoneHeight));
        }
    }

    private PresentationCardButton[] CurrentCards() =>
        GetChildren()
            .OfType<PresentationCardButton>()
            .Where(card => card.Visible && !card.IsQueuedForDeletion())
            .ToArray();

    private Main? FindMain()
    {
        Node? node = GetParent();
        while (node is not null)
        {
            if (node is Main main)
                return main;
            node = node.GetParent();
        }

        return null;
    }
}
