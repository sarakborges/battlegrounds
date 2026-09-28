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

    internal bool CanAcquireOfferFromDrag(int offerSlot)
    {
        if (!CanUsePreparationDrag || !TryGetHuman(out var human))
            return false;

        return human.PlayableOffer.Any(entry => entry.Slot == offerSlot);
    }

    internal bool CanSellFieldUnitFromDrag(int fieldIndex)
    {
        if (!CanUsePreparationDrag || !TryGetHuman(out var human))
            return false;

        return fieldIndex >= 0 && fieldIndex < human.Field.Count;
    }

    internal bool CanDeployReserveFromDrag(int reserveSlot)
    {
        if (!CanUsePreparationDrag || _session is null || !TryGetHuman(out var human))
            return false;

        return reserveSlot >= 0 &&
               reserveSlot < human.Reserve.Count &&
               human.Field.Count < _session.Mod.PreparationRules.FieldCapacity;
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

        // Buying mirrors Hearthstone/Battlegrounds: drag a tavern offer onto the hero,
        // never onto the shopkeeper. Keep the fallback target exactly on HeroCore so the
        // pointer affordance and the action agree with what the player sees.
        var heroCore = GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroCore");
        if (!ContainsPointer(heroCore, pointer, 12.0f))
            return;

        AcquireOfferFromDrag(offerSlot);
    }

    internal void CompleteFieldDragFromPointer(int fieldIndex, Vector2 pointer, int? insertionIndex)
    {
        if (!CanSellFieldUnitFromDrag(fieldIndex))
            return;

        var shopkeeper = GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/TavernShelf/ShelfRow/ShopkeeperSlot");
        if (ContainsPointer(shopkeeper, pointer, 18.0f))
        {
            SellFieldUnitFromDrag(fieldIndex);
            return;
        }

        if (insertionIndex is not int insertion || !ContainsPointer(_fieldButtons, pointer, 28.0f))
            return;

        ReorderHumanFieldAtInsertion(fieldIndex, insertion);
    }

    internal void CompleteReserveDragFromPointer(int reserveSlot, Vector2 pointer, int? insertionIndex)
    {
        if (!CanDeployReserveFromDrag(reserveSlot) || !ContainsPointer(_fieldButtons, pointer, 28.0f))
            return;

        DeployReserveAtInsertion(reserveSlot, insertionIndex ?? int.MaxValue);
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
