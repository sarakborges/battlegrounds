using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Playables;

namespace Battlegrounds.Core.Domain.Players;

public sealed partial class PlayerState
{
    private readonly List<ActionInstance> _actionReserve = [];
    private readonly List<ActionDefinition> _actionOffer = [];
    private ReadOnlyCollection<ActionInstance>? _actionReserveView;
    private ReadOnlyCollection<ActionDefinition>? _actionOfferView;

    public IReadOnlyList<ActionInstance> ActionReserve => _actionReserveView ??= _actionReserve.AsReadOnly();
    public IReadOnlyList<ActionDefinition> ActionOffer => _actionOfferView ??= _actionOffer.AsReadOnly();
    public int PlayableReserveCount => _reserve.Count + _actionReserve.Count;
    public int PlayableOfferCount => _offer.Count + _actionOffer.Count;

    public IReadOnlyList<PlayableOfferEntry> PlayableOffer
    {
        get
        {
            var result = new List<PlayableOfferEntry>(PlayableOfferCount);
            var slot = 0;
            for (var unitSlot = 0; unitSlot < _offer.Count; unitSlot++)
                result.Add(PlayableOfferEntry.ForUnit(slot++, _offer[unitSlot], IsUnitOfferSlotFrozen(unitSlot)));
            for (var actionSlot = 0; actionSlot < _actionOffer.Count; actionSlot++)
                result.Add(PlayableOfferEntry.ForAction(slot++, _actionOffer[actionSlot], IsActionOfferSlotFrozen(actionSlot)));
            return result.AsReadOnly();
        }
    }

    public IReadOnlyList<PlayableReserveEntry> PlayableReserve
    {
        get
        {
            var result = new List<PlayableReserveEntry>(PlayableReserveCount);
            var slot = 0;
            foreach (var unit in _reserve) result.Add(PlayableReserveEntry.ForUnit(slot++, unit));
            foreach (var action in _actionReserve) result.Add(PlayableReserveEntry.ForAction(slot++, action));
            return result.AsReadOnly();
        }
    }

    internal void ReplaceActionOffer(IEnumerable<ActionDefinition> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actionOffer.Clear();
        _actionOffer.AddRange(actions);
        TrimFrozenOfferSlots(_frozenActionOfferSlots, _actionOffer.Count);
    }

    internal ActionDefinition TakeOfferedAction(int actionSlot)
    {
        var action = _actionOffer[actionSlot];
        _actionOffer.RemoveAt(actionSlot);
        ShiftFrozenOfferSlotsAfterRemoval(_frozenActionOfferSlots, actionSlot);
        return action;
    }

    internal void AddOfferedAction(ActionDefinition definition) =>
        _actionOffer.Add(definition ?? throw new ArgumentNullException(nameof(definition)));

    internal void ReplaceOfferedAction(int slot, ActionDefinition definition) =>
        _actionOffer[slot] = definition ?? throw new ArgumentNullException(nameof(definition));

    internal void AddActionToReserve(ActionInstance action) =>
        _actionReserve.Add(action ?? throw new ArgumentNullException(nameof(action)));

    internal ActionInstance RemoveActionFromReserve(int actionSlot)
    {
        var action = _actionReserve[actionSlot];
        _actionReserve.RemoveAt(actionSlot);
        return action;
    }

    internal bool TryResolvePlayableOfferSlot(int slot, out PlayableOfferEntry entry)
    {
        var entries = PlayableOffer;
        if (slot < 0 || slot >= entries.Count)
        {
            entry = null!;
            return false;
        }
        entry = entries[slot];
        return true;
    }

    internal bool TryResolvePlayableReserveSlot(int slot, out PlayableReserveEntry entry)
    {
        var entries = PlayableReserve;
        if (slot < 0 || slot >= entries.Count)
        {
            entry = null!;
            return false;
        }
        entry = entries[slot];
        return true;
    }
}
