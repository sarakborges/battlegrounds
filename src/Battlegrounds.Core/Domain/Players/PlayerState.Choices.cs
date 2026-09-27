using Battlegrounds.Core.Domain.Choices;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Players;

public sealed partial class PlayerState
{
    private readonly List<PendingUnitChoice> _pendingChoices = [];
    private long _nextChoiceId = 1;

    public PendingUnitChoice? PendingChoice => _pendingChoices.Count == 0 ? null : _pendingChoices[0];
    internal int PendingChoiceCount => _pendingChoices.Count;

    internal bool QueueUnitChoice(IEnumerable<UnitDefinition> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var materialized = options.ToArray();
        if (materialized.Length == 0)
        {
            return false;
        }

        _pendingChoices.Add(new PendingUnitChoice(new ChoiceId(_nextChoiceId++), materialized));
        return true;
    }

    internal UnitDefinition ResolveUnitChoice(ChoiceId choiceId, int optionIndex)
    {
        var choice = PendingChoice
            ?? throw new InvalidOperationException("Player has no pending choice.");
        if (choice.Id != choiceId)
        {
            throw new InvalidOperationException("Only the current pending choice can be resolved.");
        }
        if (optionIndex < 0 || optionIndex >= choice.Options.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(optionIndex));
        }

        var selected = choice.Options[optionIndex];
        _pendingChoices.RemoveAt(0);
        return selected;
    }
}
