using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps dynamic presentation cards in a compact centered horizontal row while the
/// surrounding scene owns the row height. The row reports no content-driven minimum
/// height so cards never inflate the vertical HUD.
///
/// Preparation cards use spatial dragging: the lifted card follows the pointer, the
/// board reflows around the intended insertion point, and the gesture is resolved from
/// the pointer's final screen position. Godot drop targets remain a fast path, while the
/// spatial fallback keeps selling, deployment, and reordering reliable across nested UI.
/// </summary>
public partial class HorizontalCardRow : Container
{
    [Export] public float Gap { get; set; }
    [Export] public float PreferredCardWidth { get; set; }
    [Export] public float MinimumCardWidth { get; set; }
    [Export] public float PreferredCardHeight { get; set; }
    [Export] public float Padding { get; set; }

    private bool IsOfferRow => Name == "OfferButtons";
    private bool IsFieldRow => Name == "FieldButtons";
    private bool IsReserveRow => Name == "ReserveButtons";
    private int _fieldDragSourceIndex = -1;
    private int _fieldPreviewInsertionIndex = -1;
    private int _reservePreviewInsertionIndex = -1;

    public override void _Ready()
    {
        SetProcess(IsOfferRow || IsFieldRow || IsReserveRow);
        QueueSort();
    }

    public override Vector2 _GetMinimumSize() => Vector2.Zero;

    public override void _Process(double delta)
    {
        if (IsOfferRow)
            ConfigureOfferDrag();

        if (IsReserveRow)
            ConfigureReserveDrag();

        if (!IsFieldRow)
            return;

        ConfigureFieldDragAndDrop();
        UpdateFieldPreviewFromPointer();
        UpdateReservePreviewFromPointer();
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

        if (IsFieldRow && IsFieldDragActive(cards.Length))
        {
            var geometry = ResolveGeometry(cards.Length);
            LayoutDraggedField(cards, geometry);
            LayoutInsertionZones(cards.Length, geometry);
            return;
        }

        if (IsFieldRow && _reservePreviewInsertionIndex >= 0)
        {
            var geometry = ResolveGeometry(cards.Length + 1);
            LayoutCardsAroundExternalInsertion(cards, geometry, _reservePreviewInsertionIndex);
            LayoutInsertionZones(cards.Length, ResolveGeometry(cards.Length));
            return;
        }

        var normalGeometry = ResolveGeometry(cards.Length);
        LayoutCardsNormally(cards, normalGeometry);

        if (IsFieldRow)
            LayoutInsertionZones(cards.Length, normalGeometry);
    }

    private void ConfigureOfferDrag()
    {
        var main = FindMain();
        if (main is null)
            return;

        var cards = CurrentCards();
        for (var index = 0; index < cards.Length; index++)
        {
            var slot = index;
            cards[index].ConfigureOfferDrag(
                slot,
                main.CanUsePreparationDrag && !cards[index].Disabled,
                () => CompleteOfferDrag(slot));
        }
    }

    private void ConfigureReserveDrag()
    {
        var main = FindMain();
        if (main is null)
            return;

        var cards = CurrentCards();
        for (var index = 0; index < cards.Length; index++)
        {
            var slot = index;
            cards[index].ConfigureReserveUnitDrag(
                slot,
                main.CanDragReserveUnitFromDrag(slot) && !cards[index].Disabled,
                () => CompleteReserveDrag(slot));
        }
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
            var sourceIndex = index;
            cards[index].ConfigureFieldDrag(
                sourceIndex,
                canDrag && !cards[index].Disabled,
                BeginFieldDrag,
                () => CompleteFieldDrag(sourceIndex));
        }

        SyncInsertionZones(cards.Length, reorderEnabled, main.ReorderHumanFieldAtInsertion);
    }

    private void CompleteOfferDrag(int offerSlot)
    {
        if (GetViewport().GuiIsDragSuccessful())
            return;

        FindMain()?.CompleteOfferDragFromPointer(offerSlot, GetViewport().GetMousePosition());
    }

    private void CompleteReserveDrag(int reserveSlot)
    {
        if (GetViewport().GuiIsDragSuccessful())
            return;

        var pointer = GetViewport().GetMousePosition();
        var insertion = ResolveFieldInsertionAtPointerFromSibling(pointer);
        FindMain()?.CompleteReserveDragFromPointer(reserveSlot, pointer, insertion);
    }

    private void CompleteFieldDrag(int sourceIndex)
    {
        var successful = GetViewport().GuiIsDragSuccessful();
        var pointer = GetViewport().GetMousePosition();
        var insertion = ResolveFieldInsertionAtPointer(pointer);

        EndFieldDrag();

        if (successful)
            return;

        FindMain()?.CompleteFieldDragFromPointer(sourceIndex, pointer, insertion);
    }

    private void BeginFieldDrag(int sourceIndex)
    {
        var cards = CurrentCards();
        if (sourceIndex < 0 || sourceIndex >= cards.Length)
            return;

        _fieldDragSourceIndex = sourceIndex;
        _fieldPreviewInsertionIndex = sourceIndex;
        _reservePreviewInsertionIndex = -1;
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
        if (_fieldDragSourceIndex < 0 || _fieldDragSourceIndex >= cards.Length)
            return;

        var insertion = ResolveFieldInsertionAtPointer(GetViewport().GetMousePosition());
        var resolved = insertion ?? -1;
        if (resolved == _fieldPreviewInsertionIndex)
            return;

        _fieldPreviewInsertionIndex = resolved;
        QueueSort();
    }

    private void UpdateReservePreviewFromPointer()
    {
        if (_fieldDragSourceIndex >= 0)
        {
            SetReservePreviewInsertion(-1);
            return;
        }

        var viewport = GetViewport();
        if (!viewport.GuiIsDragging() ||
            !PreparationDragPayload.TryReadReserveUnit(viewport.GuiGetDragData(), out var reserveSlot))
        {
            SetReservePreviewInsertion(-1);
            return;
        }

        var main = FindMain();
        if (main is null || !main.CanDeployReserveFromDrag(reserveSlot))
        {
            SetReservePreviewInsertion(-1);
            return;
        }

        var insertion = ResolveFieldInsertionAtPointer(viewport.GetMousePosition()) ?? -1;
        SetReservePreviewInsertion(insertion);
    }

    private void SetReservePreviewInsertion(int insertion)
    {
        if (_reservePreviewInsertionIndex == insertion)
            return;

        _reservePreviewInsertionIndex = insertion;
        QueueSort();
    }

    private int? ResolveFieldInsertionAtPointer(Vector2 pointer)
    {
        if (!IsFieldRow || !ExpandedGlobalRect(22.0f).HasPoint(pointer))
            return null;

        var cards = CurrentCards();
        var geometry = ResolveGeometry(cards.Length);
        var localX = pointer.X - GetGlobalRect().Position.X;
        return ResolveInsertionFromPointer(localX, cards.Length, geometry);
    }

    private int? ResolveFieldInsertionAtPointerFromSibling(Vector2 pointer)
    {
        var main = FindMain();
        var fieldRow = main?.GetNodeOrNull<HorizontalCardRow>("%FieldButtons");
        return fieldRow?.ResolveFieldInsertionAtPointer(pointer);
    }

    private Rect2 ExpandedGlobalRect(float amount)
    {
        var rect = GetGlobalRect();
        var expansion = new Vector2(amount, amount);
        return new Rect2(rect.Position - expansion, rect.Size + expansion * 2.0f);
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

    private void LayoutCardsAroundExternalInsertion(
        PresentationCardButton[] cards,
        RowGeometry geometry,
        int insertionIndex)
    {
        insertionIndex = Math.Clamp(insertionIndex, 0, cards.Length);
        for (var index = 0; index < cards.Length; index++)
        {
            var slot = index >= insertionIndex ? index + 1 : index;
            FitChildInRect(
                cards[index],
                new Rect2(
                    geometry.X + slot * geometry.Pitch,
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
