using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Match;

public sealed class MatchCombineTests
{
    [Fact]
    public void MatchEngine_UsesCombineCatalogCarriedByUnitCatalog()
    {
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 1, 1);
        var result = new UnitDefinition(new UnitId("result"), "Result", 1, 3, 3);
        var combine = new UnitCombineDefinition(new UnitCombineId("merge"), "Merge", source.Id, 3, result.Id);
        var units = new UnitCatalog([source, result], new UnitCombineCatalog([combine]));
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 6)]);
        var preparation = new PreparationRules(10, 0, 10, 0, 1, 1, 7, 10, 2, [3, 3], [5]);
        var engine = new MatchEngine(
            new MatchRules(2, 2),
            preparation,
            new CombatRules(StartingSidePolicy.Random),
            pool,
            new MinimumRandomSource(),
            units);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);

        engine.BeginMatch(match);
        var player = match.Players[0];
        Assert.True(engine.ExecutePreparation(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        var ids = player.Reserve.Select(unit => unit.Id).ToArray();

        var combined = engine.ExecutePreparation(match, new CombineUnitsCommand(player.Id, combine.Id, ids));

        Assert.True(combined.Succeeded);
        Assert.Equal(result.Id, Assert.Single(player.Reserve).Definition.Id);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
