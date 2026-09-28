using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class TargetedOnPlayTests
{
    [Fact]
    public void DeployUnit_SelectedOnPlayTarget_IsValidatedBeforeMutationAndPassedToRuntime()
    {
        var target = new UnitDefinition(new UnitId("target"), "Target", 1, 2, 3);
        var played = new UnitDefinition(
            new UnitId("played"),
            "Played",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Selected), 2, 3)])
            ]);
        var filler = new UnitDefinition(new UnitId("filler"), "Filler", 1, 1, 1);
        var units = new UnitCatalog([target, played, filler]);
        var pool = new UnitPool(units, [new UnitPoolEntry(filler.Id, 4)]);
        var rules = new PreparationRules(10, 0, 20, 0, 1, 1, 7, 10, 1, [1], []);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        player.AddToReserve(match.CreateUnit(target, UnitInstanceOrigin.Generated));
        player.AddToReserve(match.CreateUnit(played, UnitInstanceOrigin.Generated));

        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        var targetInstance = Assert.Single(player.Field);

        var missingTarget = engine.Execute(match, new DeployUnitCommand(player.Id, 0));
        Assert.False(missingTarget.Succeeded);
        Assert.Equal(PreparationFailureCode.InvalidDeployTarget, missingTarget.FailureCode);
        Assert.Single(player.Reserve);
        Assert.Single(player.Field);

        var result = engine.Execute(match, new DeployUnitCommand(player.Id, 0, targetInstance.Id));
        Assert.True(result.Succeeded);
        Assert.Empty(player.Reserve);
        Assert.Equal(4, targetInstance.Attack);
        Assert.Equal(6, targetInstance.Health);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
