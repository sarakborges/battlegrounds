using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Combat;

public sealed class PowerLifecycleCombatTests
{
    [Fact]
    public void Resolve_PassivePowerRunsCombatStartAndEndLocally()
    {
        var powerId = new PowerId("combat-income");
        var power = new PowerDefinition(
            powerId,
            "Combat Income",
            activation: null,
            triggers:
            [
                new TriggerDefinition(NativeTriggerKeys.OnCombatStart, [new AddResourceEffectDefinition(2)]),
                new TriggerDefinition(NativeTriggerKeys.OnCombatEnd, [new AddResourceEffectDefinition(3)]),
            ]);
        var units = new UnitCatalog([
            new UnitDefinition(new UnitId("wall"), "Wall", 1, 0, 5),
        ]);
        var wall = units.GetRequired(new UnitId("wall"));
        var input = new CombatInput(
            new CombatParticipant(new PlayerId(0), [new CombatUnitSnapshot(new UnitInstanceId(1), wall.Id, 1, 0, 5, definition: wall)], powerId),
            new CombatParticipant(new PlayerId(1), [new CombatUnitSnapshot(new UnitInstanceId(2), wall.Id, 1, 0, 5, definition: wall)]));
        var engine = new CombatEngine(7, units, new BehaviorCatalog([]), new PowerCatalog([power]));

        var result = engine.Resolve(input, new CombatRules(StartingSidePolicy.Random), new SeededRandomSource(1));

        Assert.True(result.IsDraw);
        Assert.Equal(5, result.ResourceDeltas[new PlayerId(0)]);
    }

    [Fact]
    public void Resolve_SetPowerDuringCombatReturnsExplicitPowerChange()
    {
        var nextId = new PowerId("next");
        var shiftId = new PowerId("shift");
        var shift = new PowerDefinition(
            shiftId,
            "Shift",
            activation: null,
            triggers:
            [
                new TriggerDefinition(NativeTriggerKeys.OnCombatEnd, [new SetPowerEffectDefinition(nextId)]),
            ]);
        var next = new PowerDefinition(
            nextId,
            "Next",
            activation: null,
            triggers:
            [
                new TriggerDefinition(NativeTriggerKeys.OnTurnStart, [new AddResourceEffectDefinition(1)]),
            ]);
        var units = new UnitCatalog([
            new UnitDefinition(new UnitId("wall"), "Wall", 1, 0, 5),
        ]);
        var wall = units.GetRequired(new UnitId("wall"));
        var input = new CombatInput(
            new CombatParticipant(new PlayerId(0), [new CombatUnitSnapshot(new UnitInstanceId(1), wall.Id, 1, 0, 5, definition: wall)], shiftId),
            new CombatParticipant(new PlayerId(1), [new CombatUnitSnapshot(new UnitInstanceId(2), wall.Id, 1, 0, 5, definition: wall)]));
        var engine = new CombatEngine(7, units, new BehaviorCatalog([]), new PowerCatalog([shift, next]));

        var result = engine.Resolve(input, new CombatRules(StartingSidePolicy.Random), new SeededRandomSource(1));

        Assert.Equal(nextId, result.PowerChanges[new PlayerId(0)]);
        Assert.Equal(shiftId, input.Left.CurrentPowerId);
    }
}
