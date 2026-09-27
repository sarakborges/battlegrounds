using Battlegrounds.AI;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.AI.Tests;

public sealed class FieldOrderingTests
{
    [Fact]
    public void PlayPreparation_ReordersFieldThroughCoreBeforeEndingTurn()
    {
        var strong = new UnitDefinition(new UnitId("strong"), "Strong", 1, 9, 9);
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [new SummonUnitEffectDefinition(strong.Id)])
            ]);
        var units = new UnitCatalog([source, strong]);
        var rules = new PreparationRules(
            startingResource: 3,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [1, 1],
            initialUpgradeCostsByTier: [5]);
        var random = new MinimumRandomSource();
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 8)]);
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

        var player = match.Players[0];
        Assert.True(result.PlayerReady);
        Assert.Equal(1, result.CommandCounts.Reorders);
        Assert.Equal([strong.Id, source.Id], player.Field.Select(unit => unit.Definition.Id).ToArray());
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
