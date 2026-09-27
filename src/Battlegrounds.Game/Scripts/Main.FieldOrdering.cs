using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.Game;

public partial class Main
{
    internal bool CanReorderHumanField => CanUsePreparationDrag;

    internal void ReorderHumanFieldAtInsertion(int fromIndex, int insertionIndex)
    {
        if (!CanReorderHumanField || !TryGetHuman(out var human))
            return;

        var count = human.Field.Count;
        if (fromIndex < 0 || fromIndex >= count || insertionIndex < 0 || insertionIndex > count)
            return;

        var order = human.Field.Select(unit => unit.Id).ToList();
        var moved = order[fromIndex];
        order.RemoveAt(fromIndex);

        var targetIndex = insertionIndex > fromIndex ? insertionIndex - 1 : insertionIndex;
        targetIndex = Math.Clamp(targetIndex, 0, order.Count);
        if (targetIndex == fromIndex)
            return;

        order.Insert(targetIndex, moved);
        SubmitHumanCommand(new ReorderFieldCommand(human.Id, order));
        Render();
    }
}
