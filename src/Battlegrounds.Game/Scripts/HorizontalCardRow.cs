using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps dynamic presentation cards in a compact centered horizontal row while the
/// surrounding scene owns the row height. The row reports no content-driven minimum
/// height so cards never inflate the vertical HUD.
///
/// Tavern offers and field pieces are drag sources. While a field piece is lifted,
/// the row computes the intended insertion directly from pointer position and reflows
/// neighboring minions around that gap. Drop zones remain stable for the whole gesture.
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

        if (!IsFieldRow)
            return;

        ConfigureFieldDragAndDrop();
        UpdateFieldPreviewFromPointer();
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

        var geometry = ResolveGeometry(cards.Length);

        if (IsFieldRow && IsFieldDragActive(cards.Length))
            LayoutDraggedField(cards, geometry);
        else
            LayoutCardsNormally(cards, geometry);

        if (IsFieldRow)
            LayoutInsertionZones(cards.Length, geometry);
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
        var reorderEnabled = main.CanReorderHumanField && cards.Length > 1;

        for (var index = 0; index < cards.Length; index++)
        {
            cards[index].ConfigureFieldDrag(
                index,
                canDrag && !cards[index].Disabled,
                BeginFieldDrag,
                EndFieldDrag);
        }

        SyncInsertionZones(cards.Length, reorderEnabled, main.ReorderHumanFieldAtInsertion);
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

    internal void EndFieldDrag()
    {
        if (_fieldDragSourceIndex < 0 && _fieldPreviewInsertionIndex < 0)
            return;

        _fieldDragSourceIndex = -1;
        _fieldPreviewInsertionIndex = -1;
        QueueSort();
    }

    private void UpdateFieldPreviewFromPointer()
    {
        var cards = CurrentCards();
        if (!IsFieldDragActive(cards.Length))
            return;

        var geometry = ResolveGeometry(cards.Length);
        var insertion = ResolveInsertionFromPointer(GetLocalMousePosition().X, cards.Length, geometry);
        if (insertion == _fieldPreviewInsertionIndex)
            return;

        _fieldPreviewInsertionIndex = insertion;
        QueueSort();
    }

    private bool IsFieldDragActive(int cardCount) =>
        _fieldDragSourceIndex >= 0 &&
        _fieldDragSourceIndex < cardCount &&
        _fieldPreviewInsertionIndex >= 0;

    private RowGeometry ResolveGeometry(int cardCount)
    {
        var innerWidth = Mathf.Max(0.0f, Size.X - Padding * 2.0f);
        var gapsWidth = Gap * Mathf.Max(0, cardCount - 1);
        var availableCardsWidth = Mathf.Max(0.0f, innerWidth - gapsWidth);
        var fittedWidth = cardCount == 0 ? PreferredCardWidth : availableCardsWidth / cardCount;
        var cardWidth = Mathf.Min(PreferredCardWidth, fittedWidth);

        if (cardCount > 0 && availableCardsWidth >= MinimumCardWidth * cardCount)
            cardWidth = Mathf.Max(MinimumCardWidth, cardWidth);

        var availableHeight = Mathf.Max(0.0f, Size.Y - Padding * 2.0f);
        var cardHeight = Mathf.Min(PreferredCardHeight, availableHeight);
        var totalWidth = cardWidth * cardCount + gapsWidth;
        var x = Mathf.Max(Padding, (Size.X - totalWidth) * 0.5f);
        var y = Mathf.Max(Padding, (Size.Y - cardHeight) * 0.5f);
        return new RowGeometry(x, y, cardWidth, cardHeight, cardWidth + Gap);
    }

    private void LayoutCardsNormally(PresentationCardButton[] cards, RowGeometry geometry)
    {
        for (var index = 0; index < cards.Length; index++)
        {
            FitChildInRect(
                cards[index],
                new Rect2(
                    geometry.X + index * geometry.Pitch,
                    geometry.Y,
                    geometry.CardWidth,
                    geometry.CardHeight));
        }
    }

    private void LayoutDraggedField(PresentationCardButton[] cards, RowGeometry geometry)
    {
        var targetIndex = ResolveResultIndex(
            _fieldDragSourceIndex,
            _fieldPreviewInsertionIndex,
            cards.Length);

        var source = cards[_fieldDragSourceIndex];
        FitChildInRect(
            source,
            new Rect2(
                geometry.X + targetIndex * geometry.Pitch,
                geometry.Y,
                geometry.CardWidth,
                geometry.CardHeight));

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
                new Rect2(
                    geometry.X + slot * geometry.Pitch,
                    geometry.Y,
                    geometry.CardWidth,
                    geometry.CardHeight));
        }
    }

    private static int ResolveInsertionFromPointer(float pointerX, int cardCount, RowGeometry geometry)
    {
        if (cardCount <= 0)
            return 0;

        for (var index = 0; index < cardCount; index++)
        {
            var center = geometry.X + index * geometry.Pitch + geometry.CardWidth * 0.5f;
            if (pointerX < center)
                return index;
        }

        return cardCount;
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

    private void LayoutInsertionZones(int cardCount, RowGeometry geometry)
    {
        var zones = GetChildren()
            .OfType<FieldInsertionDropZone>()
            .Where(zone => !zone.IsQueuedForDeletion())
            .OrderBy(zone => zone.InsertionIndex)
            .ToArray();
        if (zones.Length < cardCount + 1)
            return;

        var zoneY = geometry.Y + geometry.CardHeight * 0.08f;
        var zoneHeight = geometry.CardHeight * 0.84f;
        var interiorWidth = Mathf.Max(30.0f, Gap * 2.4f);

        for (var insertion = 0; insertion <= cardCount; insertion++)
        {
            var boundaryX = insertion switch
            {
                0 => geometry.X,
                var value when value == cardCount =>
                    geometry.X + cardCount * geometry.CardWidth + Mathf.Max(0, cardCount - 1) * Gap,
                _ => geometry.X + insertion * geometry.Pitch - Gap * 0.5f,
            };

            var width = insertion is 0 || insertion == cardCount
                ? Mathf.Max(36.0f, geometry.CardWidth * 0.34f)
                : interiorWidth;
            var left = Mathf.Clamp(boundaryX - width * 0.5f, 0.0f, Mathf.Max(0.0f, Size.X - width));

            FitChildInRect(
                zones[insertion],
                new Rect2(left, zoneY, width, zoneHeight));
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

    private readonly record struct RowGeometry(
        float X,
        float Y,
        float CardWidth,
        float CardHeight,
        float Pitch);
}
