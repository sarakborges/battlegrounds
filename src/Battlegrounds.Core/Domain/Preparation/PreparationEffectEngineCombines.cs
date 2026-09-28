using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Preparation;

internal sealed partial class PreparationEffectEngine
{
    public void ProcessCombinedUnit(
        MatchState match,
        PlayerState owner,
        UnitInstance unit)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(unit);

        var triggers = unit.Definition.Triggers
            .Where(value => value.Event == NativeTriggerKeys.OnCombine)
            .ToArray();
        if (triggers.Length == 0) return;

        if (triggers.Any(trigger =>
                trigger.Conditions.Count != 0 ||
                trigger.ActivationLimit is not null ||
                trigger.Counter is not null ||
                trigger.Count is not null))
        {
            throw new InvalidOperationException(
                "onCombine currently supports direct reward effects only; conditions, counters, and activation limits are not valid in this context.");
        }

        var (world, runtime) = GetRuntime(match);
        runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnCombine, world.Wrap(unit, owner.Id)));
    }
}
