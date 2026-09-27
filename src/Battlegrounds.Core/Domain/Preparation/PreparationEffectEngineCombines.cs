using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Preparation;

internal static class PreparationEffectEngineCombines
{
    public static void ProcessCombinedUnit(
        this PreparationEffectEngine engine,
        MatchState match,
        PlayerState owner,
        UnitInstance unit)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(unit);

        var trigger = unit.Definition.Triggers.FirstOrDefault(value => value.Event == NativeTriggerKeys.OnCombine);
        if (trigger is null) return;

        if (trigger.Conditions.Count != 0 || trigger.ActivationLimit is not null || trigger.Counter is not null || trigger.Count is not null)
            throw new InvalidOperationException("onCombine currently supports direct reward effects only; conditions, counters, and activation limits are not valid in this context.");

        var definition = new ActionDefinition(
            new ActionId("__combine__" + unit.Definition.Id.Value + "__" + unit.Id.Value),
            "Combine " + unit.Definition.Name,
            Math.Max(1, unit.Definition.Tier),
            0,
            trigger.Effects);

        engine.ProcessAction(match, owner, match.CreateAction(definition), selectedTargetInstanceId: null);
    }
}
