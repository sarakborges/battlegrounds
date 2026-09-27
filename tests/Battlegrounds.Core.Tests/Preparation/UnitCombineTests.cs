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
    public void Combine_ConsumesExactOwnedCopies_ReturnsPoolCopies_AndRunsOnCombine()
    {
        var source = new UnitDefinition(new UnitId("scout"), "Scout", 1, 1, 2);
        var result = new UnitDefinition(
            new UnitId("scout-prime"),
            "Scout Prime",
            2,
            3,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombine,
                    [
                        new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Self), 1, 1),
                        new AddResourceEffectDefinition(1),
                    ])
            ]);
        var units = new UnitCatalog([source, result]);
        var combines = new UnitCombineCatalog(
        [
            new UnitCombineDefinition(
                new UnitCombineId("scout-upgrade"),
                "Scout Upgrade",
                source.Id,
                requiredCopies: 3,
                result.Id)
        ]);
        var rules = new PreparationRules(
            startingResource: 5,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 0,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [3, 3],
            initialUpgradeCostsByTier: [5]);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 20)]);
        var engine = new PreparationEngine(
            rules,
            pool,
            new MinimumRandomSource(),
            units,
            behaviorCatalog: null,
            powerCatalog: null,
            actionCatalog: null,
            combineCatalog: combines);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));

        engine.BeginPreparation(match);
        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        for (var index = 0; index < 3; index++)
            Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);

        var componentIds = player.Reserve.Select(unit => unit.Id).ToArray();
        var combineResult = engine.Execute(
            match,
            new CombineUnitsCommand(player.Id, new UnitCombineId("scout-upgrade"), componentIds));

        Assert.True(combineResult.Succeeded);
        var combined = Assert.Single(player.Reserve);
        Assert.Equal(result.Id, combined.Definition.Id);
        Assert.Equal(UnitInstanceOrigin.Generated, combined.Origin);
        Assert.Equal(4, combined.Attack);
        Assert.Equal(6, combined.Health);
        Assert.Equal(6, player.Resource);
        Assert.Equal(17, pool.GetAvailableCopies(source.Id));
    }

    [Fact]
    public void Combine_RejectsDuplicateRuntimeInstancesWithoutMutatingState()
    {
        var source = new UnitDefinition(new UnitId("scout"), "Scout", 1, 1, 2);
        var result = new UnitDefinition(new UnitId("scout-prime"), "Scout Prime", 2, 3, 5);
        var units = new UnitCatalog([source, result]);
        var combines = new UnitCombineCatalog(
        [
            new UnitCombineDefinition(new UnitCombineId("scout-upgrade"), "Scout Upgrade", source.Id, 3, result.Id)
        ]);
        var rules = new PreparationRules(5, 0, 10, 0, 1, 1, 7, 10, 2, [3, 3], [5]);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 20)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, null, combines);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        for (var index = 0; index < 3; index++)
            Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        var first = player.Reserve[0].Id;

        var commandResult = engine.Execute(
            match,
            new CombineUnitsCommand(player.Id, new UnitCombineId("scout-upgrade"), [first, first, first]));

        Assert.False(commandResult.Succeeded);
        Assert.Equal(PreparationFailureCode.InvalidCombineUnits, commandResult.FailureCode);
        Assert.Equal(3, player.Reserve.Count);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
