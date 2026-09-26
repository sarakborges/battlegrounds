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
        var context = Context(
            UnitSnapshot(1, 0, true));

        var resolved = new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnCombatStart,
            context,
            new MinimumRandomSource());

        Assert.Equal(3, resolved.Count);
        Assert.Equal([1, 2, 3], resolved.Select(effect => effect.Sequence));
        Assert.IsType<AddResourceEffectDefinition>(resolved[0].Definition);
        Assert.IsType<ModifyStatsEffectDefinition>(resolved[1].Definition);
        Assert.IsType<AddResourceEffectDefinition>(resolved[2].Definition);
    }

    [Fact]
    public void ResolveEvent_RandomEnemyFiltersByTypeAndTagBeforeUsingRng()
    {
        var target = new EffectTargetSelector(
            EffectTargetScope.RandomEnemy,
            new UnitTypeId("organic"),
            new TagId("starter"));
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnAttack,
            [new DealDamageEffectDefinition(target, 2)]));
        var context = Context(
            UnitSnapshot(1, 0, true),
            UnitSnapshot(2, 1, true, ["organic"], ["starter"]),
            UnitSnapshot(3, 1, true, ["organic"], ["other"]),
            UnitSnapshot(4, 1, true, ["construct"], ["starter"]));

        var resolved = Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnAttack,
            context,
            new MinimumRandomSource()));

        Assert.Equal(new UnitInstanceId(2), Assert.Single(resolved.TargetInstanceIds));
    }

    [Fact]
    public void ResolveEvent_AllFriendlyKeepsStableContextOrder()
    {
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnSummon,
            [new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.AllFriendly), 1, 1)]));
        var context = Context(
            UnitSnapshot(1, 0, true),
            UnitSnapshot(4, 0, true),
            UnitSnapshot(2, 1, true),
            UnitSnapshot(5, 0, false));

        var resolved = Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnSummon,
            context,
            new MinimumRandomSource()));

        Assert.Equal(
            [new UnitInstanceId(1), new UnitInstanceId(4)],
            resolved.TargetInstanceIds);
    }

    [Fact]
    public void ResolveEvent_DifferentEventProducesNoInstructions()
    {
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnPlay,
            [new AddResourceEffectDefinition(1)]));
        var context = Context(UnitSnapshot(1, 0, true));

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
        string[]? tags = null) =>
        new(
            new UnitInstanceId(instanceId),
            new PlayerId(owner),
            alive,
            (types ?? []).Select(id => new UnitTypeId(id)),
            (tags ?? []).Select(id => new TagId(id)));

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
