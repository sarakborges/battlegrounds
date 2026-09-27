using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class ActionPlayableTests
{
    [Fact]
    public void Preparation_CanAcquireAndPlayActionThroughGenericPlayableSurface()
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 2, 2);
        var action = new ActionDefinition(
            new ActionId("training"),
            "Training",
            1,
            2,
            [new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Selected), 1, 1)]);
        var units = new UnitCatalog([unit]);
        var actions = new ActionCatalog([action]);
        var rules = new PreparationRules(
            startingResource: 10,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [2, 2],
            initialUpgradeCostsByTier: [5],
            actionOfferSizesByTier: [1, 1]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 10)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));

        engine.BeginPreparation(match);
        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        Assert.Equal(2, player.PlayableOffer.Count);

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        var fieldUnit = Assert.Single(player.Field);

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.Single(player.ActionReserve);
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, 0, fieldUnit.Id)).Succeeded);

        Assert.Empty(player.ActionReserve);
        Assert.Equal(3, fieldUnit.Attack);
        Assert.Equal(3, fieldUnit.Health);
        Assert.Equal(5, player.Resource);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
