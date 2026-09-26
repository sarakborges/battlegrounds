using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Combat;

public sealed class CombatEffectExecutionTests
{
    [Fact]
    public void OnDeathSummon_UsesSameEffectRuntimeDuringCombat()
    {
        var token = new UnitDefinition(new UnitId("token"), "Token", 1, 5, 5);
        var victim = new UnitDefinition(
            new UnitId("victim"),
            "Victim",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [new SummonUnitEffectDefinition(token.Id)]),
            ]);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 2, 2);
        var catalog = new UnitCatalog([victim, token, enemy]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, victim)),
            Participant(1, Snapshot(2, enemy)));

        var result = new CombatEngine(
            fieldCapacity: 7,
            catalog,
            new BehaviorCatalog(Array.Empty<BehaviorDefinition>()))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
        Assert.Contains(result.LeftSurvivors, survivor => survivor.InstanceId.Value > 2);
    }

    [Fact]
    public void OnCombatStartEffectsRunBeforeAttackSelection()
    {
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 5, 5);
        var opener = new UnitDefinition(
            new UnitId("opener"),
            "Opener",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombatStart,
                    [
                        new DestroyUnitEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Enemy)),
                    ]),
            ]);
        var catalog = new UnitCatalog([opener, enemy]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, opener)),
            Participant(1, Snapshot(2, enemy)));

        var result = new CombatEngine(
            7,
            catalog,
            new BehaviorCatalog(Array.Empty<BehaviorDefinition>()))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
        Assert.Empty(result.Attacks);
    }

    [Fact]
    public void CombatEffectsDoNotMutatePersistentDefinitionsOrInput()
    {
        var buffed = new UnitDefinition(
            new UnitId("buffed"),
            "Buffed",
            1,
            1,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombatStart,
                    [
                        new ModifyStatsEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Self),
                            10,
                            10),
                    ]),
            ]);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 0, 1);
        var catalog = new UnitCatalog([buffed, enemy]);
        var leftSnapshot = Snapshot(1, buffed);
        var input = new CombatInput(
            Participant(0, leftSnapshot),
            Participant(1, Snapshot(2, enemy)));

        _ = new CombatEngine(7, catalog, new BehaviorCatalog(Array.Empty<BehaviorDefinition>()))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        Assert.Equal(1, input.Left.Units[0].Attack);
        Assert.Equal(5, input.Left.Units[0].Health);
        Assert.Equal(1, buffed.BaseAttack);
        Assert.Equal(5, buffed.BaseHealth);
    }

    private static CombatParticipant Participant(int playerId, params CombatUnitSnapshot[] units) =>
        new(new PlayerId(playerId), units);

    private static CombatUnitSnapshot Snapshot(long instanceId, UnitDefinition definition) =>
        new(
            new UnitInstanceId(instanceId),
            definition.Id,
            definition.Tier,
            definition.BaseAttack,
            definition.BaseHealth,
            definition.Behaviors.Select(behavior => new CombatBehaviorSnapshot(behavior.Id, behavior.Handler)),
            definition);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
