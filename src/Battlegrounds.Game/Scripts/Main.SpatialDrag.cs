using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;

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
}
