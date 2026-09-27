using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class PersistentUnitMutationTests
{
    [Fact]
    public void ModifierActions_CanApplyAndRemoveNamedPersistentModifier()
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 2, 2);
        var target = new EffectTargetSelector(EffectTargetScope.Selected);
        var apply = new ActionDefinition(
            new ActionId("apply"),
            "Apply",
            1,
            0,
            [new ApplyUnitModifierEffectDefinition(target, "training", 2, 3)]);
        var remove = new ActionDefinition(
            new ActionId("remove"),
            "Remove",
            1,
            0,
            [new RemoveUnitModifierEffectDefinition(target, "training")]);

        var units = new UnitCatalog([unit]);
        var actions = new ActionCatalog([apply, remove]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 10)]);
        var engine = new PreparationEngine(CreateRules(actionOfferSize: 2), pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));

        engine.BeginPreparation(match);
        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        var fieldUnit = Assert.Single(player.Field);

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, 0, fieldUnit.Id)).Succeeded);
        Assert.Equal(4, fieldUnit.Attack);
        Assert.Equal(5, fieldUnit.Health);
        var modifier = Assert.Single(fieldUnit.Modifiers);
        Assert.Equal("training", modifier.Key);
        Assert.Equal(2, modifier.AttackDelta);
        Assert.Equal(3, modifier.HealthDelta);

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, 0, fieldUnit.Id)).Succeeded);
        Assert.Equal(2, fieldUnit.Attack);
        Assert.Equal(2, fieldUnit.Health);
        Assert.Empty(fieldUnit.Modifiers);
    }

    [Fact]
    public void CopyAndTransform_PreserveRuntimeCopyAndDetachTransformedUnitFromPool()
    {
        var alpha = new UnitDefinition(new UnitId("alpha"), "Alpha", 1, 2, 2);
        var beta = new UnitDefinition(new UnitId("beta"), "Beta", 1, 5, 6);
        var selected = new EffectTargetSelector(EffectTargetScope.Selected);
        var copy = new ActionDefinition(
            new ActionId("copy"),
            "Copy",
            1,
            0,
            [
                new ApplyUnitModifierEffectDefinition(selected, "copied-buff", 1, 2),
                new CopyUnitToReserveEffectDefinition(selected),
            ]);
        var transform = new ActionDefinition(
            new ActionId("transform"),
            "Transform",
            1,
            0,
            [new TransformUnitEffectDefinition(selected, beta.Id)]);

        var units = new UnitCatalog([alpha, beta]);
        var actions = new ActionCatalog([copy, transform]);
        var pool = new UnitPool(
            units,
            [new UnitPoolEntry(alpha.Id, 10), new UnitPoolEntry(beta.Id, 10)]);
        var engine = new PreparationEngine(CreateRules(actionOfferSize: 2), pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));

        engine.BeginPreparation(match);
        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        Assert.Equal(9, pool.GetAvailableCopies(alpha.Id));

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        var source = Assert.Single(player.Field);

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, 0, source.Id)).Succeeded);

        Assert.Equal(3, source.Attack);
        Assert.Equal(4, source.Health);
        Assert.Single(source.Modifiers);
        var copied = Assert.Single(player.Reserve);
        Assert.Equal(UnitInstanceOrigin.Generated, copied.Origin);
        Assert.Same(alpha, copied.Definition);
        Assert.Equal(3, copied.Attack);
        Assert.Equal(4, copied.Health);
        Assert.Equal("copied-buff", Assert.Single(copied.Modifiers).Key);
        Assert.Equal(9, pool.GetAvailableCopies(alpha.Id));

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, 1, source.Id)).Succeeded);

        Assert.Same(beta, source.Definition);
        Assert.Equal(UnitInstanceOrigin.Generated, source.Origin);
        Assert.Equal(5, source.Attack);
        Assert.Equal(6, source.Health);
        Assert.Empty(source.Modifiers);
        Assert.Equal(10, pool.GetAvailableCopies(alpha.Id));
        Assert.Equal(10, pool.GetAvailableCopies(beta.Id));

        Assert.True(engine.Execute(match, new ReleaseUnitCommand(player.Id, 0)).Succeeded);
        Assert.Equal(10, pool.GetAvailableCopies(alpha.Id));
        Assert.Equal(10, pool.GetAvailableCopies(beta.Id));
    }

    private static PreparationRules CreateRules(int actionOfferSize) =>
        new(
            startingResource: 10,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 1,
            offerSizesByTier: [1 + actionOfferSize],
            initialUpgradeCostsByTier: [],
            actionOfferSizesByTier: [actionOfferSize]);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
