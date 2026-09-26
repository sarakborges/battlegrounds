using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Combat;

public sealed class DeathResolutionSemanticsTests
{
    [Fact]
    public void Resolve_SimultaneousDeathsResolveInStableCreationOrder()
    {
        var token = Unit("token", 0, 1);
        var firstToResolve = Unit(
            "first",
            1,
            1,
            new TriggerDefinition(
                NativeTriggerKeys.OnDeath,
                [new SummonUnitEffectDefinition(token.Id)]));
        var secondToResolve = Unit(
            "second",
            1,
            1,
            new TriggerDefinition(
                NativeTriggerKeys.OnDeath,
                [
                    new DealDamageEffectDefinition(
                        new EffectTargetSelector(EffectTargetScope.Enemy),
                        1),
                ]));

        var input = new CombatInput(
            Participant(0, Snapshot(2, secondToResolve)),
            Participant(1, Snapshot(1, firstToResolve)));
        var engine = Engine(firstToResolve, secondToResolve, token);

        var result = Resolve(engine, input);

        Assert.True(result.IsDraw);
        Assert.Empty(result.LeftSurvivors);
        Assert.Empty(result.RightSurvivors);
    }

    [Fact]
    public void Resolve_DeathEffectUsesFreedSlotBeforeReborn()
    {
        var reborn = new BehaviorDefinition(
            new BehaviorId("reborn"),
            "Reborn",
            NativeBehaviorKeys.ReviveOnce);
        var token = Unit("token", 0, 1);
        var returning = new UnitDefinition(
            new UnitId("returning"),
            "Returning",
            1,
            1,
            1,
            behaviors: [reborn],
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [new SummonUnitEffectDefinition(token.Id)]),
            ]);
        var enemy = Unit("enemy", 1, 1);

        var input = new CombatInput(
            Participant(0, Snapshot(1, returning, reborn)),
            Participant(1, Snapshot(2, enemy)));
        var engine = new CombatEngine(
            fieldCapacity: 1,
            new UnitCatalog([returning, enemy, token]),
            new BehaviorCatalog([reborn]));

        var result = Resolve(engine, input);

        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
        var survivor = Assert.Single(result.LeftSurvivors);
        Assert.Equal(new UnitInstanceId(3), survivor.InstanceId);
    }

    [Fact]
    public void Resolve_RebornIsASummonAndRunsSummonTriggers()
    {
        var reborn = new BehaviorDefinition(
            new BehaviorId("reborn"),
            "Reborn",
            NativeBehaviorKeys.ReviveOnce);
        var returning = new UnitDefinition(
            new UnitId("returning"),
            "Returning",
            1,
            1,
            1,
            behaviors: [reborn],
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnSummon,
                    [new AddResourceEffectDefinition(1)]),
            ]);
        var enemy = Unit("enemy", 1, 1);

        var input = new CombatInput(
            Participant(0, Snapshot(1, returning, reborn)),
            Participant(1, Snapshot(2, enemy)));
        var engine = new CombatEngine(
            fieldCapacity: 2,
            new UnitCatalog([returning, enemy]),
            new BehaviorCatalog([reborn]));

        var result = Resolve(engine, input);

        Assert.Equal(1, result.ResourceDeltas[new PlayerId(0)]);
        Assert.Equal(new UnitInstanceId(1), Assert.Single(result.LeftSurvivors).InstanceId);
    }

    [Fact]
    public void Resolve_AfterFriendlyDeathsRepeatsAtConfiguredCount()
    {
        var fodder = Unit("fodder", 0, 1);
        var avenge = new UnitDefinition(
            new UnitId("listener"),
            "Listener",
            1,
            0,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.AfterFriendlyDeaths,
                    [new AddResourceEffectDefinition(1)],
                    count: 2),
            ]);
        var bomb = new UnitDefinition(
            new UnitId("bomb"),
            "Bomb",
            1,
            0,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombatStart,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Friendly),
                            1),
                    ]),
            ]);
        var enemy = Unit("enemy", 0, 5);

        var input = new CombatInput(
            Participant(
                0,
                Snapshot(1, fodder),
                Snapshot(2, fodder),
                Snapshot(3, fodder),
                Snapshot(4, fodder),
                Snapshot(5, avenge),
                Snapshot(6, bomb)),
            Participant(1, Snapshot(7, enemy)));
        var engine = Engine(fodder, avenge, bomb, enemy);

        var result = Resolve(engine, input);

        Assert.True(result.IsDraw);
        Assert.Equal(CombatEndReason.NoAttackPower, result.EndReason);
        Assert.Equal(2, result.ResourceDeltas[new PlayerId(0)]);
    }

    [Fact]
    public void Resolve_LethalUnitCanBeSavedBeforeNextDeathWave()
    {
        var first = new UnitDefinition(
            new UnitId("first"),
            "First",
            1,
            0,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Friendly),
                            1),
                    ]),
            ]);
        var second = Unit("second", 0, 1);
        var survivor = Unit("survivor", 0, 2);
        var avenge = new UnitDefinition(
            new UnitId("listener"),
            "Listener",
            1,
            0,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.AfterFriendlyDeaths,
                    [
                        new ModifyStatsEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Friendly),
                            0,
                            2),
                    ],
                    count: 2),
            ]);
        var bomb = new UnitDefinition(
            new UnitId("bomb"),
            "Bomb",
            1,
            0,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombatStart,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Friendly),
                            1),
                    ]),
            ]);
        var enemy = Unit("enemy", 0, 5);

        var input = new CombatInput(
            Participant(
                0,
                Snapshot(1, first),
                Snapshot(2, second),
                Snapshot(3, survivor),
                Snapshot(4, avenge),
                Snapshot(5, bomb)),
            Participant(1, Snapshot(6, enemy)));
        var engine = Engine(first, second, survivor, avenge, bomb, enemy);

        var result = Resolve(engine, input);

        Assert.Contains(result.LeftSurvivors, unit => unit.InstanceId == new UnitInstanceId(3));
    }

    private static CombatResult Resolve(CombatEngine engine, CombatInput input) =>
        engine.Resolve(
            input,
            new CombatRules(StartingSidePolicy.Random),
            new MinimumRandomSource());

    private static CombatEngine Engine(params UnitDefinition[] definitions) =>
        new(
            fieldCapacity: 7,
            new UnitCatalog(definitions),
            new BehaviorCatalog(Array.Empty<BehaviorDefinition>()));

    private static UnitDefinition Unit(
        string id,
        int attack,
        int health,
        params TriggerDefinition[] triggers) =>
        new(new UnitId(id), id, 1, attack, health, triggers: triggers);

    private static CombatParticipant Participant(
        int playerId,
        params CombatUnitSnapshot[] units) =>
        new(new PlayerId(playerId), units);

    private static CombatUnitSnapshot Snapshot(
        long instanceId,
        UnitDefinition definition,
        params BehaviorDefinition[] behaviors) =>
        new(
            new UnitInstanceId(instanceId),
            definition.Id,
            definition.Tier,
            definition.BaseAttack,
            definition.BaseHealth,
            behaviors.Select(behavior => new CombatBehaviorSnapshot(behavior.Id, behavior.Handler)),
            definition);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
