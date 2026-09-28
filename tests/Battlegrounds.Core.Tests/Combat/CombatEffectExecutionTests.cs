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
    public void OnAttackSelectedTarget_UsesLockedAttackTargetBeforeStrike()
    {
        var attacker = new UnitDefinition(
            new UnitId("attacker"),
            "Attacker",
            1,
            1,
            10,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnAttack,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Selected),
                            2),
                    ]),
            ]);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 0, 5);
        var catalog = new UnitCatalog([attacker, enemy]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, attacker)),
            Participant(1, Snapshot(2, enemy)));

        var result = new CombatEngine(
            7,
            catalog,
            new BehaviorCatalog(Array.Empty<BehaviorDefinition>()))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        var attack = Assert.Single(result.Attacks);
        Assert.Equal(new UnitInstanceId(2), attack.TargetInstanceId);
        Assert.Equal(2, attack.TargetHealthAfter);
        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
    }

    [Fact]
    public void OnDamageSelectedTarget_UsesLivingAttackDamageSource()
    {
        var attacker = new UnitDefinition(new UnitId("attacker"), "Attacker", 1, 3, 10);
        var retaliator = new UnitDefinition(
            new UnitId("retaliator"),
            "Retaliator",
            1,
            0,
            3,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDamage,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Selected),
                            2),
                    ]),
            ]);
        var catalog = new UnitCatalog([attacker, retaliator]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, attacker)),
            Participant(1, Snapshot(2, retaliator)));

        var result = new CombatEngine(7, catalog, new BehaviorCatalog([]))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
        var survivor = Assert.Single(result.LeftSurvivors);
        Assert.Equal(8, survivor.Health);
    }

    [Fact]
    public void OnDamageSelectedTarget_UsesLivingEffectDamageSource()
    {
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            5,
            10,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombatStart,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Enemy),
                            1),
                    ]),
            ]);
        var retaliator = new UnitDefinition(
            new UnitId("retaliator"),
            "Retaliator",
            1,
            0,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDamage,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Selected),
                            2),
                    ]),
            ]);
        var catalog = new UnitCatalog([source, retaliator]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, source)),
            Participant(1, Snapshot(2, retaliator)));

        var result = new CombatEngine(7, catalog, new BehaviorCatalog([]))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
        var survivor = Assert.Single(result.LeftSurvivors);
        Assert.Equal(8, survivor.Health);
        Assert.Empty(result.Attacks);
    }

    [Fact]
    public void OnDamageSelectedTarget_IsAbsentWhenDamageSourceIsNotTargetable()
    {
        var doomed = new UnitDefinition(
            new UnitId("doomed"),
            "Doomed",
            1,
            0,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [
                        new DealDamageEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Enemy),
                            1),
                    ]),
            ]);
        var reactive = new UnitDefinition(
            new UnitId("reactive"),
            "Reactive",
            1,
            2,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDamage,
                    [
                        new DestroyUnitEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Selected)),
                    ]),
            ]);
        var catalog = new UnitCatalog([doomed, reactive]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, doomed)),
            Participant(1, Snapshot(2, reactive)));

        var result = new CombatEngine(7, catalog, new BehaviorCatalog([]))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        Assert.Equal(new PlayerId(1), result.WinnerPlayerId);
        var survivor = Assert.Single(result.RightSurvivors);
        Assert.Equal(4, survivor.Health);
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
