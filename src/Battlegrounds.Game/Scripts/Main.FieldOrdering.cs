using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.Game;

public partial class Main
{
    internal bool CanReorderHumanField
    {
        get
        {
            if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
                return false;
            if (!TryGetHuman(out var human)) return false;
            return !human.IsEliminated &&
                   !human.IsReadyForCombat &&
                   human.PendingChoice is null &&
                   !_interaction.IsActive;
        }
    }

    internal void ReorderHumanField(int fromIndex, int toIndex)
    {
        if (!CanReorderHumanField || !TryGetHuman(out var human)) return;

        if (fromIndex < 0 || fromIndex >= human.Field.Count ||
            toIndex < 0 || toIndex >= human.Field.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        var order = human.Field.Select(unit => unit.Id).ToList();
        var moved = order[fromIndex];
        order.RemoveAt(fromIndex);
        order.Insert(toIndex, moved);
        SubmitHumanCommand(new ReorderFieldCommand(human.Id, order));
        Render();
    }
}
