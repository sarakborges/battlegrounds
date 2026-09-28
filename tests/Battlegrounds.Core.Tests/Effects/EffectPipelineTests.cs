using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Taxonomy;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Effects;

public sealed class EffectPipelineTests
{
    [Fact]
    public void ResolveEvent_PreservesTriggerAndEffectOrder()
    {
        var definition = Unit(
            new TriggerDefinition(
                NativeTriggerKeys.OnCombatStart,
                [
                    new AddResourceEffectDefinition(1),
                    new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Self), 1, 0),
                ]),
            new TriggerDefinition(
                NativeTriggerKeys.OnCombatStart,
                [new AddResourceEffectDefinition(2)]));
        var context = Context(UnitSnapshot(1, 0, true, position: 0));

        var resolved = new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnCombatStart,
            context,
            new MinimumRandomSource());

        Assert.Equal(3, resolved.Count);
        Assert.Equal([1, 2, 3], resolved.Select(effect => effect.Sequence));
    }

    [Fact]
    public void ResolveEvent_RandomEnemyFiltersBeforeUsingRng()
    {
        var target = new EffectTargetSelector(
            EffectTargetScope.Enemy,
            EffectTargetSelection.Random,
            requiredTypeId: new UnitTypeId("organic"),
            requiredTagId: new TagId("starter"));
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnAttack,
            [new DealDamageEffectDefinition(target, 2)]));
        var context = Context(
            UnitSnapshot(1, 0, true, position: 0),
            UnitSnapshot(2, 1, true, ["organic"], ["starter"], position: 0),
            UnitSnapshot(3, 1, true, ["organic"], ["other"], position: 1),
            UnitSnapshot(4, 1, true, ["construct"], ["starter"], position: 2));

        var resolved = Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnAttack,
            context,
            new MinimumRandomSource()));

        Assert.Equal(new UnitInstanceId(2), Assert.Single(resolved.TargetInstanceIds));
    }

    [Fact]
    public void ResolveEvent_HighestAttackFriendlyCanExcludeSource()
    {
        var target = new EffectTargetSelector(
            EffectTargetScope.Friendly,
            EffectTargetSelection.HighestAttack,
            excludeSource: true);
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnSummon,
            [new ModifyStatsEffectDefinition(target, 1, 1)]));
        var context = Context(
            UnitSnapshot(1, 0, true, attack: 10, position: 1),
            UnitSnapshot(2, 0, true, attack: 4, position: 0),
            UnitSnapshot(3, 0, true, attack: 7, position: 2));

        var resolved = Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnSummon,
            context,
            new MinimumRandomSource()));

        Assert.Equal(new UnitInstanceId(3), Assert.Single(resolved.TargetInstanceIds));
    }

    [Fact]
    public void ResolveEvent_AdjacentUsesFieldPositions()
    {
        var target = new EffectTargetSelector(
            EffectTargetScope.Friendly,
            EffectTargetSelection.Adjacent,
            excludeSource: true);
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnCombatStart,
            [new ModifyStatsEffectDefinition(target, 1, 0)]));
        var context = Context(
            UnitSnapshot(2, 0, true, position: 0),
            UnitSnapshot(1, 0, true, position: 1),
            UnitSnapshot(3, 0, true, position: 2),
            UnitSnapshot(4, 1, true, position: 0));

        var resolved = Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnCombatStart,
            context,
            new MinimumRandomSource()));

        Assert.Equal([new UnitInstanceId(2), new UnitInstanceId(3)], resolved.TargetInstanceIds);
    }

    [Fact]
    public void ResolveEvent_AdjacentCanUseSelectedTargetAsAnchor()
    {
        var target = new EffectTargetSelector(
            EffectTargetScope.Enemy,
            EffectTargetSelection.Adjacent,
            relativeTo: EffectTargetAnchor.Selected);
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnAttack,
            [new DealDamageEffectDefinition(target, 2)]));
        var units = new[]
        {
            UnitSnapshot(1, 0, true, position: 0),
            UnitSnapshot(2, 1, true, position: 0),
            UnitSnapshot(3, 1, true, position: 1),
            UnitSnapshot(4, 1, true, position: 2),
        };
        var context = new EffectResolutionContext(
            new UnitInstanceId(1),
            new PlayerId(0),
            units,
            new UnitInstanceId(3));

        var resolved = Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnAttack,
            context,
            new MinimumRandomSource()));

        Assert.Equal([new UnitInstanceId(2), new UnitInstanceId(4)], resolved.TargetInstanceIds);
    }

    [Fact]
    public void ResolveEvent_ConditionsGateWholeTrigger()
    {
        var conditions = new EffectConditionDefinition[]
        {
            new UnitCountConditionDefinition(
                new EffectUnitQuery(
                    EffectTargetScope.Friendly,
                    excludeSource: true,
                    requiredTypeId: new UnitTypeId("organic")),
                EffectComparison.GreaterThanOrEqual,
                2),
            new SourceStatConditionDefinition(
                EffectStat.Attack,
                EffectComparison.GreaterThanOrEqual,
                5),
        };
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnPlay,
            [new AddResourceEffectDefinition(1)],
            conditions: conditions));
        var passing = Context(
            UnitSnapshot(1, 0, true, ["construct"], attack: 5, position: 0),
            UnitSnapshot(2, 0, true, ["organic"], position: 1),
            UnitSnapshot(3, 0, true, ["organic"], position: 2));
        var failing = Context(
            UnitSnapshot(1, 0, true, ["construct"], attack: 4, position: 0),
            UnitSnapshot(2, 0, true, ["organic"], position: 1));

        Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnPlay,
            passing,
            new MinimumRandomSource()));
        Assert.Empty(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnPlay,
            failing,
            new MinimumRandomSource()));
    }

    [Fact]
    public void ResolveEvent_DifferentEventProducesNoInstructions()
    {
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnPlay,
            [new AddResourceEffectDefinition(1)]));
        var context = Context(UnitSnapshot(1, 0, true, position: 0));

        var resolved = new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnDeath,
            context,
            new MinimumRandomSource());

        Assert.Empty(resolved);
    }

    private static UnitDefinition Unit(params TriggerDefinition[] triggers) =>
        new(new UnitId("source"), "Source", 1, 1, 1, triggers: triggers);

    private static EffectResolutionContext Context(params EffectUnitSnapshot[] units) =>
        new(new UnitInstanceId(1), new PlayerId(0), units);

    private static EffectUnitSnapshot UnitSnapshot(
        long instanceId,
        int owner,
        bool alive,
        string[]? types = null,
        string[]? tags = null,
        int attack = 1,
        int health = 1,
        int position = 0) =>
        new(
            new UnitInstanceId(instanceId),
            new PlayerId(owner),
            alive,
            attack,
            health,
            position,
            isSelectable: true,
            (types ?? []).Select(id => new UnitTypeId(id)),
            (tags ?? []).Select(id => new TagId(id)));

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
