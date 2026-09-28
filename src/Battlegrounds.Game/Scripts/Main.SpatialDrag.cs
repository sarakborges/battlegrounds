using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    internal bool CanUsePreparationDrag
    {
        get
        {
            if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
                return false;
            if (!TryGetHuman(out var human))
                return false;

            return !human.IsEliminated &&
                   !human.IsReadyForCombat &&
                   human.PendingChoice is null &&
                   !_interaction.IsActive;
        }
    }

    internal bool CanDragOfferFromDrag(int offerSlot)
    {
        if (!CanUsePreparationDrag || !TryGetHuman(out var human))
            return false;

        return human.PlayableOffer.Any(entry => entry.Slot == offerSlot);
    }

    internal bool CanAcquireOfferFromDrag(int offerSlot)
    {
        if (!CanDragOfferFromDrag(offerSlot) || _session is null || !TryGetHuman(out var human))
            return false;

        var entry = human.PlayableOffer.First(candidate => candidate.Slot == offerSlot);
        var rules = _session.Mod.PreparationRules;
        if (human.PlayableReserveCount >= rules.ReserveCapacity)
            return false;

        var cost = entry.Unit is not null
            ? rules.AcquireCost
            : entry.Action?.Cost ?? int.MaxValue;
        return human.CanAfford(cost);
    }

    internal bool CanSellFieldUnitFromDrag(int fieldIndex)
    {
        if (!CanUsePreparationDrag || !TryGetHuman(out var human))
            return false;

        return fieldIndex >= 0 && fieldIndex < human.Field.Count;
    }

    internal bool CanDragReserveUnitFromDrag(int reserveSlot)
    {
        if (!CanUsePreparationDrag || !TryGetHuman(out var human))
            return false;

        return reserveSlot >= 0 && reserveSlot < human.Reserve.Count;
    }

    internal bool CanDeployReserveFromDrag(int reserveSlot)
    {
        if (!CanDragReserveUnitFromDrag(reserveSlot) || _session is null || !TryGetHuman(out var human))
            return false;

        return human.Field.Count < _session.Mod.PreparationRules.FieldCapacity;
    }

    internal bool CanResolvePreparationDrag(Variant data)
    {
        if (PreparationDragPayload.TryReadOffer(data, out var offerSlot))
            return CanDragOfferFromDrag(offerSlot);
        if (PreparationDragPayload.TryReadField(data, out var fieldIndex))
            return CanSellFieldUnitFromDrag(fieldIndex);
        if (PreparationDragPayload.TryReadReserveUnit(data, out var reserveSlot))
            return CanDragReserveUnitFromDrag(reserveSlot);
        return false;
    }

    internal void ResolvePreparationDropFromPointer(Variant data, Vector2 pointer)
    {
        if (PreparationDragPayload.TryReadOffer(data, out var offerSlot))
        {
            CompleteOfferDragFromPointer(offerSlot, pointer);
            return;
        }

        if (PreparationDragPayload.TryReadField(data, out var fieldIndex))
        {
            CompleteFieldDragFromPointer(fieldIndex, pointer, ResolveFieldInsertionFromPointer(pointer));
            return;
        }

        if (PreparationDragPayload.TryReadReserveUnit(data, out var reserveSlot))
            CompleteReserveDragFromPointer(reserveSlot, pointer, ResolveFieldInsertionFromPointer(pointer));
    }

    internal void AcquireOfferFromDrag(int offerSlot)
    {
        if (!CanAcquireOfferFromDrag(offerSlot))
            return;

        ExecuteHuman(player => new AcquirePlayableCommand(player.Id, offerSlot));
    }

    internal void SellFieldUnitFromDrag(int fieldIndex)
    {
        if (!CanSellFieldUnitFromDrag(fieldIndex))
            return;

        ExecuteHuman(player => new ReleaseUnitCommand(player.Id, fieldIndex));
    }

    internal void CompleteOfferDragFromPointer(int offerSlot, Vector2 pointer)
    {
        if (!CanAcquireOfferFromDrag(offerSlot))
            return;

        var heroCore = GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroCore");
        if (!ContainsPointer(heroCore, pointer, PreparationDropTargetPadding))
            return;

        AcquireOfferFromDrag(offerSlot);
    }

    internal void CompleteFieldDragFromPointer(int fieldIndex, Vector2 pointer, int? insertionIndex)
    {
        if (!CanSellFieldUnitFromDrag(fieldIndex))
            return;

        var shopkeeper = GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/TavernShelf/ShelfRow/ShopkeeperSlot");
        if (ContainsPointer(shopkeeper, pointer, PreparationDropTargetPadding))
        {
            SellFieldUnitFromDrag(fieldIndex);
            return;
        }

        if (insertionIndex is not int insertion || !ContainsPointer(_fieldButtons, pointer, PreparationDropTargetPadding + 20.0f))
            return;

        ReorderHumanFieldAtInsertion(fieldIndex, insertion);
    }

    internal void CompleteReserveDragFromPointer(int reserveSlot, Vector2 pointer, int? insertionIndex)
    {
        if (!CanDeployReserveFromDrag(reserveSlot) ||
            !ContainsPointer(_fieldButtons, pointer, PreparationDropTargetPadding + 20.0f))
            return;

        DeployReserveAtInsertion(reserveSlot, insertionIndex ?? int.MaxValue);
    }

    private int? ResolveFieldInsertionFromPointer(Vector2 pointer)
    {
        if (!ContainsPointer(_fieldButtons, pointer, PreparationDropTargetPadding + 20.0f))
            return null;

        var cards = _fieldButtons.GetChildren()
            .OfType<PresentationCardButton>()
            .Where(card => card.Visible && !card.IsQueuedForDeletion())
            .ToArray();

        for (var index = 0; index < cards.Length; index++)
        {
            if (pointer.X < cards[index].GetGlobalRect().GetCenter().X)
                return index;
        }

        return cards.Length;
    }

    private void DeployReserveAtInsertion(int reserveSlot, int insertionIndex)
    {
        if (!CanDeployReserveFromDrag(reserveSlot) || !TryGetHuman(out var human))
            return;

        var deployingUnitId = human.Reserve[reserveSlot].Id;
        var result = SubmitHumanCommand(new DeployUnitCommand(human.Id, reserveSlot));
        if (!result.HasValue || !result.Value.Succeeded)
        {
            Render();
            return;
        }

        if (TryGetHuman(out var updatedHuman))
        {
            var currentOrder = updatedHuman.Field.Select(unit => unit.Id).ToArray();
            var order = currentOrder.ToList();
            var deployedIndex = order.IndexOf(deployingUnitId);
            if (deployedIndex >= 0)
            {
                order.RemoveAt(deployedIndex);
                var targetIndex = Math.Clamp(insertionIndex, 0, order.Count);
                order.Insert(targetIndex, deployingUnitId);

                if (!order.SequenceEqual(currentOrder))
                    SubmitHumanCommand(new ReorderFieldCommand(updatedHuman.Id, order));
            }
        }

        Render();
    }

    private static bool ContainsPointer(Control? target, Vector2 pointer, float padding)
    {
        if (target is null || !target.Visible)
            return false;

        var rect = target.GetGlobalRect();
        var expansion = new Vector2(padding, padding);
        return new Rect2(rect.Position - expansion, rect.Size + expansion * 2.0f).HasPoint(pointer);
    }
}
