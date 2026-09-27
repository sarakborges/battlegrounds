using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Combat;

public sealed class CombatTimelineTests
{
    [Fact]
    public void Resolve_OrdersCombatStartMutationBeforeFirstAttack()
    {
        var opener = new UnitDefinition(
            new UnitId("opener"),
            "Opener",
            1,
            2,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombatStart,
                    [new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Self), 3, 4)]),
            ]);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 1, 20);
        var catalog = new UnitCatalog([opener, enemy]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, opener)),
            Participant(1, Snapshot(2, enemy)));

        var result = new CombatEngine(7, catalog, new BehaviorCatalog([]))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        Assert.NotEmpty(result.Timeline);
        Assert.Equal(Enumerable.Range(1, result.Timeline.Count), result.Timeline.Select(@event => @event.Sequence));

        var trigger = Assert.IsType<CombatTriggerTimelineEvent>(result.Timeline[0]);
        Assert.Equal(NativeTriggerKeys.OnCombatStart, trigger.Trigger);
        Assert.Equal(new UnitInstanceId(1), trigger.SourceUnitInstanceId);

        var stats = Assert.IsType<CombatUnitStatsChangedTimelineEvent>(result.Timeline[1]);
        Assert.Equal(new UnitInstanceId(1), stats.UnitInstanceId);
        Assert.Equal(2, stats.AttackBefore);
        Assert.Equal(5, stats.AttackAfter);
        Assert.Equal(5, stats.HealthBefore);
        Assert.Equal(9, stats.HealthAfter);

        var firstAttackIndex = result.Timeline.ToList().FindIndex(@event => @event is CombatAttackStartedTimelineEvent);
        Assert.True(firstAttackIndex > 1);
    }

    [Fact]
    public void Resolve_DeathSummonCarriesCombatLocalIdentityAndTriggerAttribution()
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
                new TriggerDefinition(NativeTriggerKeys.OnDeath, [new SummonUnitEffectDefinition(token.Id)]),
            ]);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 2, 2);
        var catalog = new UnitCatalog([victim, token, enemy]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, victim)),
            Participant(1, Snapshot(2, enemy)));

        var result = new CombatEngine(7, catalog, new BehaviorCatalog([]))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        var death = Assert.Single(result.Timeline, @event => @event is CombatUnitDiedTimelineEvent died && died.UnitInstanceId == new UnitInstanceId(1));
        var deathTrigger = Assert.Single(result.Timeline, @event =>
            @event is CombatTriggerTimelineEvent trigger &&
            trigger.Trigger == NativeTriggerKeys.OnDeath &&
            trigger.SourceUnitInstanceId == new UnitInstanceId(1));
        var summoned = Assert.Single(result.Timeline, @event => @event is CombatUnitSummonedTimelineEvent value && value.Unit.UnitId == token.Id);
        var deathEvent = Assert.IsType<CombatUnitDiedTimelineEvent>(death);
        var summonEvent = Assert.IsType<CombatUnitSummonedTimelineEvent>(summoned);

        Assert.True(deathEvent.Sequence < deathTrigger.Sequence);
        Assert.True(deathTrigger.Sequence < summonEvent.Sequence);
        Assert.Equal(new PlayerId(0), summonEvent.PlayerId);
        Assert.Equal(token.Id, summonEvent.Unit.UnitId);
        Assert.Equal("Token", summonEvent.Unit.Definition?.Name);
        Assert.True(summonEvent.Unit.InstanceId.Value > 2);
        Assert.Equal(0, summonEvent.Position);
        Assert.Equal(new UnitInstanceId(1), summonEvent.SourceUnitInstanceId);
    }

    [Fact]
    public void Resolve_PowerLifecycleEventsWrapVisiblePowerEffects()
    {
        var nextId = new PowerId("next");
        var shiftId = new PowerId("shift");
        var shift = new PowerDefinition(
            shiftId,
            "Shift",
            activation: null,
            triggers:
            [
                new TriggerDefinition(NativeTriggerKeys.OnCombatStart, [new AddResourceEffectDefinition(2)]),
                new TriggerDefinition(NativeTriggerKeys.OnCombatEnd, [new SetPowerEffectDefinition(nextId)]),
            ]);
        var next = new PowerDefinition(nextId, "Next", activation: null, triggers: []);
        var wall = new UnitDefinition(new UnitId("wall"), "Wall", 1, 0, 5);
        var units = new UnitCatalog([wall]);
        var input = new CombatInput(
            new CombatParticipant(new PlayerId(0), [Snapshot(1, wall)], shiftId),
            new CombatParticipant(new PlayerId(1), [Snapshot(2, wall)]));

        var result = new CombatEngine(7, units, new BehaviorCatalog([]), new PowerCatalog([shift, next]))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        var startTrigger = Assert.Single(result.Timeline, @event =>
            @event is CombatTriggerTimelineEvent trigger && trigger.SourcePowerId == shiftId && trigger.Trigger == NativeTriggerKeys.OnCombatStart);
        var resource = Assert.Single(result.Timeline, @event => @event is CombatResourceChangedTimelineEvent);
        var endTrigger = Assert.Single(result.Timeline, @event =>
            @event is CombatTriggerTimelineEvent trigger && trigger.SourcePowerId == shiftId && trigger.Trigger == NativeTriggerKeys.OnCombatEnd);
        var power = Assert.Single(result.Timeline, @event => @event is CombatPowerChangedTimelineEvent);

        Assert.True(startTrigger.Sequence < resource.Sequence);
        Assert.True(resource.Sequence < endTrigger.Sequence);
        Assert.True(endTrigger.Sequence < power.Sequence);
        Assert.Equal(nextId, Assert.IsType<CombatPowerChangedTimelineEvent>(power).PowerId);
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
