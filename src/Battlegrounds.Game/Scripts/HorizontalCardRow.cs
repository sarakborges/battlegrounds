using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps dynamic presentation cards in a compact centered horizontal row while the
/// surrounding scene owns the row height. The row reports no content-driven minimum
/// height so cards never inflate the vertical HUD.
///
/// Tavern offers and field pieces are drag sources. While a field piece is lifted,
/// the remaining warband continuously reflows around an insertion gap so the player
/// sees the final order before releasing the mouse.
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
    private int _fieldDragSourceIndex = -1;
    private int _fieldPreviewInsertionIndex = -1;

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

        if (_fieldDragSourceIndex >= cards.Length)
            EndFieldDrag();

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

        if (IsFieldRow && IsFieldDragActive(cards.Length))
            LayoutDraggedField(cards, x, y, cardWidth, cardHeight);
        else
            LayoutCardsNormally(cards, x, y, cardWidth, cardHeight);

        if (IsFieldRow)
            LayoutInsertionZones(cards.Length, x, y, cardWidth, cardHeight);
    }

    internal void PreviewFieldInsertion(int sourceIndex, int insertionIndex)
    {
        if (!IsFieldRow || sourceIndex != _fieldDragSourceIndex)
            return;

        var cardCount = CurrentCards().Length;
        var clamped = Math.Clamp(insertionIndex, 0, cardCount);
        if (_fieldPreviewInsertionIndex == clamped)
            return;

        _fieldPreviewInsertionIndex = clamped;
        QueueSort();
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
                Gap = 16.0f;
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
        var canDrag = main.CanUsePreparationDrag;
        var reorderEnabled = main.CanReorderHumanField && cards.Length > 1 && IsFieldDragActive(cards.Length);

        for (var index = 0; index < cards.Length; index++)
        {
            cards[index].ConfigureFieldDrag(
                index,
                canDrag && !cards[index].Disabled,
                BeginFieldDrag,
                EndFieldDrag);
        }

        SyncInsertionZones(cards.Length, reorderEnabled, main.ReorderHumanFieldAtInsertion);
        QueueSort();
    }

    private void BeginFieldDrag(int sourceIndex)
    {
        var cards = CurrentCards();
        if (sourceIndex < 0 || sourceIndex >= cards.Length)
            return;

        _fieldDragSourceIndex = sourceIndex;
        _fieldPreviewInsertionIndex = sourceIndex;
        QueueSort();
    }

    private void EndFieldDrag()
    {
        if (_fieldDragSourceIndex < 0 && _fieldPreviewInsertionIndex < 0)
            return;

        _fieldDragSourceIndex = -1;
        _fieldPreviewInsertionIndex = -1;

        foreach (var zone in GetChildren().OfType<FieldInsertionDropZone>())
            zone.ResetFeedback();

        QueueSort();
    }

    private bool IsFieldDragActive(int cardCount) =>
        _fieldDragSourceIndex >= 0 &&
        _fieldDragSourceIndex < cardCount &&
        _fieldPreviewInsertionIndex >= 0;

    private void LayoutCardsNormally(
        PresentationCardButton[] cards,
        float x,
        float y,
        float cardWidth,
        float cardHeight)
    {
        for (var index = 0; index < cards.Length; index++)
        {
            FitChildInRect(
                cards[index],
                new Rect2(x + index * (cardWidth + Gap), y, cardWidth, cardHeight));
        }
    }

    private void LayoutDraggedField(
        PresentationCardButton[] cards,
        float x,
        float y,
        float cardWidth,
        float cardHeight)
    {
        var targetIndex = ResolveResultIndex(
            _fieldDragSourceIndex,
            _fieldPreviewInsertionIndex,
            cards.Length);

        var source = cards[_fieldDragSourceIndex];
        FitChildInRect(
            source,
            new Rect2(x + _fieldDragSourceIndex * (cardWidth + Gap), y, cardWidth, cardHeight));

        var remaining = cards
            .Where((_, index) => index != _fieldDragSourceIndex)
            .ToArray();
        var remainingIndex = 0;

        for (var slot = 0; slot < cards.Length; slot++)
        {
            if (slot == targetIndex)
                continue;

            var card = remaining[remainingIndex++];
            FitChildInRect(
                card,
                new Rect2(x + slot * (cardWidth + Gap), y, cardWidth, cardHeight));
        }
    }

    private static int ResolveResultIndex(int sourceIndex, int insertionIndex, int cardCount)
    {
        var targetIndex = insertionIndex > sourceIndex ? insertionIndex - 1 : insertionIndex;
        return Math.Clamp(targetIndex, 0, Math.Max(0, cardCount - 1));
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

    private void LayoutInsertionZones(
        int cardCount,
        float x,
        float y,
        float cardWidth,
        float cardHeight)
    {
        var zones = GetChildren()
            .OfType<FieldInsertionDropZone>()
            .Where(zone => !zone.IsQueuedForDeletion())
            .OrderBy(zone => zone.InsertionIndex)
            .ToArray();
        if (zones.Length < cardCount + 1)
            return;

        var positions = new float[cardCount + 1];
        for (var insertion = 0; insertion <= cardCount; insertion++)
        {
            positions[insertion] = insertion switch
            {
                0 => x - Gap * 0.5f,
                var value when value == cardCount =>
                    x + cardCount * cardWidth + Mathf.Max(0, cardCount - 1) * Gap + Gap * 0.5f,
                _ => x + insertion * (cardWidth + Gap) - Gap * 0.5f,
            };
        }

        var zoneY = y + cardHeight * 0.08f;
        var zoneHeight = cardHeight * 0.84f;
        for (var insertion = 0; insertion <= cardCount; insertion++)
        {
            var left = insertion == 0
                ? Mathf.Max(0.0f, x - cardWidth * 0.45f)
                : (positions[insertion - 1] + positions[insertion]) * 0.5f;
            var right = insertion == cardCount
                ? Mathf.Min(Size.X, positions[insertion] + cardWidth * 0.45f)
                : (positions[insertion] + positions[insertion + 1]) * 0.5f;

            FitChildInRect(
                zones[insertion],
                new Rect2(left, zoneY, Mathf.Max(8.0f, right - left), zoneHeight));
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
