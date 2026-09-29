using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool CanUseWebPreparationDrag()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
            return false;
        if (!TryGetHuman(out var human))
            return false;

        return !human.IsEliminated &&
               !human.IsReadyForCombat &&
               human.PendingChoice is null &&
               !_interaction.IsActive &&
               _session.CurrentPreparationPlayerId == human.Id;
    }

    private void ReorderHumanFieldAtInsertion(int fromIndex, int insertionIndex)
    {
        if (!CanUseWebPreparationDrag() || !TryGetHuman(out var human))
            return;

        var count = human.Field.Count;
        if (fromIndex < 0 || fromIndex >= count || insertionIndex < 0 || insertionIndex > count)
            return;

        var order = human.Field.Select(unit => unit.Id).ToList();
        var moved = order[fromIndex];
        order.RemoveAt(fromIndex);

        var targetIndex = insertionIndex > fromIndex ? insertionIndex - 1 : insertionIndex;
        targetIndex = Math.Clamp(targetIndex, 0, order.Count);
        order.Insert(targetIndex, moved);

        var current = human.Field.Select(unit => unit.Id);
        if (!order.SequenceEqual(current))
            SubmitHumanCommand(new ReorderFieldCommand(human.Id, order));
    }

    private void DeployReserveAtInsertion(int reserveSlot, int insertionIndex)
    {
        if (!CanUseWebPreparationDrag() || _session is null || !TryGetHuman(out var human))
            return;
        if (reserveSlot < 0 || reserveSlot >= human.Reserve.Count)
            return;
        if (human.Field.Count >= _session.Mod.PreparationRules.FieldCapacity)
            return;

        var deployingUnitId = human.Reserve[reserveSlot].Id;
        var result = SubmitHumanCommand(new DeployUnitCommand(human.Id, reserveSlot));
        if (!result.HasValue || !result.Value.Succeeded || !TryGetHuman(out var updatedHuman))
            return;

        var currentOrder = updatedHuman.Field.Select(unit => unit.Id).ToArray();
        var order = currentOrder.ToList();
        var deployedIndex = order.IndexOf(deployingUnitId);
        if (deployedIndex < 0)
            return;

        order.RemoveAt(deployedIndex);
        var targetIndex = Math.Clamp(insertionIndex, 0, order.Count);
        order.Insert(targetIndex, deployingUnitId);

        if (!order.SequenceEqual(currentOrder))
            SubmitHumanCommand(new ReorderFieldCommand(updatedHuman.Id, order));
    }
}
