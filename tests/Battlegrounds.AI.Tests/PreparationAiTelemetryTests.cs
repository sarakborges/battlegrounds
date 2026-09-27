using Battlegrounds.AI;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.AI.Tests;

public sealed class PreparationAiTelemetryTests
{
    [Fact]
    public void PlayPreparation_CountsOnlySuccessfulCommandsByType()
    {
        var unit = new UnitDefinition(new UnitId("worker"), "Worker", 1, 2, 2);
        var units = new UnitCatalog([unit]);
        var rules = new PreparationRules(
            startingResource: 3,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 1,
            offerSizesByTier: [1],
            initialUpgradeCostsByTier: []);
        var random = new MinimumRandomSource();
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 4)]);
        var engine = new MatchEngine(
            new MatchRules(2, 2),
            rules,
            new CombatRules(StartingSidePolicy.Random),
            pool,
            random,
            unitCatalog: units);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);
        var agent = new PreparationAiAgent(rules, random);

        var result = agent.PlayPreparation(engine, match, new PlayerId(0));

        Assert.Equal(result.CommandsExecuted, result.CommandCounts.Total);
        Assert.Equal(1, result.CommandCounts.Acquires);
        Assert.Equal(1, result.CommandCounts.Deploys);
        Assert.Equal(1, result.CommandCounts.Ends);
        Assert.Equal(0, result.CommandCounts.Refreshes);
        Assert.Equal(0, result.CommandCounts.Upgrades);
        Assert.True(result.PlayerReady);
    }

    [Fact]
    public void CommandCounts_AdditionPreservesEveryCategory()
    {
        var left = new PreparationAiCommandCounts(
            Acquires: 1,
            Releases: 2,
            Deploys: 3,
            ActionsPlayed: 4,
            Combines: 5,
            Refreshes: 6,
            Upgrades: 7);
        var right = new PreparationAiCommandCounts(
            PowersUsed: 8,
            UnitChoicesResolved: 9,
            ActionChoicesResolved: 10,
            Freezes: 11,
            Unfreezes: 12,
            Ends: 13);

        var total = left + right;

        Assert.Equal(1, total.Acquires);
        Assert.Equal(2, total.Releases);
        Assert.Equal(3, total.Deploys);
        Assert.Equal(4, total.ActionsPlayed);
        Assert.Equal(5, total.Combines);
        Assert.Equal(6, total.Refreshes);
        Assert.Equal(7, total.Upgrades);
        Assert.Equal(8, total.PowersUsed);
        Assert.Equal(9, total.UnitChoicesResolved);
        Assert.Equal(10, total.ActionChoicesResolved);
        Assert.Equal(11, total.Freezes);
        Assert.Equal(12, total.Unfreezes);
        Assert.Equal(13, total.Ends);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
