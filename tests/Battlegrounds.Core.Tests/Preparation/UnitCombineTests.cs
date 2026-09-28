using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class UnitCombineTests
{
    [Fact]
    public void Combine_ConsumesExactCopies_ReturnsPoolCopies_AndCreatesGeneratedResult()
    {
        var source = new UnitDefinition(new UnitId("scout"), "Scout", 1, 1, 2);
        var result = new UnitDefinition(
            new UnitId("scout-merged"),
            "Merged Scout",
            1,
            4,
            6,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombine,
                    [new AddResourceEffectDefinition(new SourceStatEffectValueExpression(EffectStat.Attack))]),
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombine,
                    [new AddResourceEffectDefinition(1)])
            ]);
        var combine = new UnitCombineDefinition(
            new UnitCombineId("scout-merge"),
            "Scout Merge",
            source.Id,
            3,
            result.Id);
        var combines = new UnitCombineCatalog([combine]);
        var units = new UnitCatalog([source, result], combines);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 6)]);
        var rules = new PreparationRules(
            startingResource: 10,
            resourcePerRound: 0,
            maximumResource: 20,
            acquireCost: 0,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [3, 3],
            initialUpgradeCostsByTier: [5]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, null, combines);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));

        engine.BeginPreparation(match);
        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        Assert.Equal(3, pool.GetAvailableCopies(source.Id));

        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        var consumedIds = player.Reserve.Select(unit => unit.Id).ToArray();

        var combineResult = engine.Execute(match, new CombineUnitsCommand(player.Id, combine.Id, consumedIds));

        Assert.True(combineResult.Succeeded);
        var merged = Assert.Single(player.Reserve);
        Assert.Equal(result.Id, merged.Definition.Id);
        Assert.Equal(UnitInstanceOrigin.Generated, merged.Origin);
        Assert.Equal(6, pool.GetAvailableCopies(source.Id));
        Assert.Equal(15, player.Resource);
    }

    [Fact]
    public void Combine_CanAggregatePersistentModifiersWithoutCarryingTemporaryOnes()
    {
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 2, 2);
        var result = new UnitDefinition(new UnitId("result"), "Result", 1, 5, 5);
        var combine = new UnitCombineDefinition(
            new UnitCombineId("inherit"),
            "Inherited Combine",
            source.Id,
            3,
            result.Id,
            inheritPersistentModifiers: true);
        var combines = new UnitCombineCatalog([combine]);
        var units = new UnitCatalog([source, result], combines);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 6)]);
        var rules = new PreparationRules(10, 0, 10, 0, 1, 1, 7, 10, 2, [3, 3], [5]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, null, combines);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        player.Reserve[0].ApplyModifier("legacy", 1, 2, UnitModifierDuration.Persistent);
        player.Reserve[1].ApplyModifier("legacy", 3, 4, UnitModifierDuration.Persistent);
        player.Reserve[2].ApplyModifier("temporary", 9, 9, UnitModifierDuration.UntilCombatEnd);
        var ids = player.Reserve.Select(unit => unit.Id).ToArray();

        Assert.True(engine.Execute(match, new CombineUnitsCommand(player.Id, combine.Id, ids)).Succeeded);

        var merged = Assert.Single(player.Reserve);
        Assert.Equal(9, merged.Attack);
        Assert.Equal(11, merged.Health);
        var inherited = Assert.Single(merged.Modifiers);
        Assert.Equal("legacy", inherited.Key);
        Assert.Equal(4, inherited.AttackDelta);
        Assert.Equal(6, inherited.HealthDelta);
        Assert.Equal(UnitModifierDuration.Persistent, inherited.Duration);
    }

    [Fact]
    public void Combine_RejectsDuplicateOrWrongSourceInstances()
    {
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 1, 1);
        var other = new UnitDefinition(new UnitId("other"), "Other", 1, 1, 1);
        var result = new UnitDefinition(new UnitId("result"), "Result", 1, 2, 2);
        var combine = new UnitCombineDefinition(new UnitCombineId("combine"), "Combine", source.Id, 2, result.Id);
        var combines = new UnitCombineCatalog([combine]);
        var units = new UnitCatalog([source, other, result], combines);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 4), new UnitPoolEntry(other.Id, 4)]);
        var rules = new PreparationRules(10, 0, 10, 0, 1, 1, 7, 10, 2, [2, 2], [5]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, null, combines);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);

        var first = player.Reserve[0].Id;
        var duplicate = engine.Execute(match, new CombineUnitsCommand(player.Id, combine.Id, [first, first]));

        Assert.False(duplicate.Succeeded);
        Assert.Equal(PreparationFailureCode.InvalidCombineUnits, duplicate.FailureCode);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
