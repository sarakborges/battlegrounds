using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class PreparationEngineTests
{
    [Fact]
    public void BeginPreparation_InitializesRoundState()
    {
        var (match, engine, _) = CreateMatch();
        engine.BeginPreparation(match);

        Assert.Equal(MatchPhase.Preparation, match.Phase);
        Assert.Equal(1, match.Round);
        Assert.Equal(1, match.Revision);
        Assert.All(match.Players, player =>
        {
            Assert.Equal(3, player.Resource);
            Assert.Equal(1, player.Tier);
            Assert.Equal(5, player.UpgradeCost);
            Assert.Equal(3, player.Offer.Count);
            Assert.False(player.IsReadyForCombat);
            Assert.False(player.IsOfferFrozen);
        });
    }

    [Fact]
    public void AcquireUnit_MovesDefinitionIntoRuntimeReserveAndSpendsResource()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];
        var acquiredDefinition = player.Offer[0];

        var result = engine.Execute(match, new AcquireUnitCommand(player.Id, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(0, player.Resource);
        Assert.Equal(2, player.Offer.Count);
        var unit = Assert.Single(player.Reserve);
        Assert.Same(acquiredDefinition, unit.Definition);
        Assert.Equal(acquiredDefinition.BaseAttack, unit.Attack);
        Assert.Equal(acquiredDefinition.BaseHealth, unit.Health);
        Assert.Equal(2, match.Revision);
    }

    [Fact]
    public void FailedCommand_DoesNotMutateStateOrRevision()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        var revisionBeforeFailure = match.Revision;

        var result = engine.Execute(match, new AcquireUnitCommand(player.Id, 0));

        Assert.False(result.Succeeded);
        Assert.Equal(PreparationFailureCode.InsufficientResource, result.FailureCode);
        Assert.Equal(revisionBeforeFailure, match.Revision);
        Assert.Single(player.Reserve);
        Assert.Equal(2, player.Offer.Count);
    }

    [Fact]
    public void DeployThenRelease_ReturnsUnitCopyToSharedPool()
    {
        var (match, engine, pool) = CreateStartedMatch();
        var player = match.Players[0];
        var acquiredUnit = player.Offer[0];
        var copiesBeforeAcquire = pool.GetAvailableCopies(acquiredUnit.Id);

        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.Equal(copiesBeforeAcquire, pool.GetAvailableCopies(acquiredUnit.Id));
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        Assert.Empty(player.Reserve);
        Assert.Single(player.Field);

        Assert.True(engine.Execute(match, new ReleaseUnitCommand(player.Id, 0)).Succeeded);
        Assert.Empty(player.Field);
        Assert.Equal(1, player.Resource);
        Assert.Equal(copiesBeforeAcquire + 1, pool.GetAvailableCopies(acquiredUnit.Id));
    }

    [Fact]
    public void FreezeOffer_PreservesRemainingOfferAndFillsMissingSlotsNextRound()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];

        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        var frozenUnits = player.Offer.ToArray();
        Assert.True(engine.Execute(match, new FreezeOfferCommand(player.Id)).Succeeded);
        Assert.True(engine.Execute(match, new EndPreparationCommand(player.Id)).Succeeded);
        Assert.True(engine.Execute(match, new EndPreparationCommand(match.Players[1].Id)).Succeeded);

        engine.BeginPreparation(match);

        Assert.True(player.IsOfferFrozen);
        Assert.Equal(3, player.Offer.Count);
        Assert.Same(frozenUnits[0], player.Offer[0]);
        Assert.Same(frozenUnits[1], player.Offer[1]);
    }

    [Fact]
    public void FreezeOffer_CanBeToggledRepeatedlyWithoutLimit()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];

        for (var index = 0; index < 20; index++)
        {
            Assert.True(engine.Execute(match, new FreezeOfferCommand(player.Id)).Succeeded);
            Assert.True(player.IsOfferFrozen);
            Assert.True(engine.Execute(match, new UnfreezeOfferCommand(player.Id)).Succeeded);
            Assert.False(player.IsOfferFrozen);
        }
    }

    [Fact]
    public void RefreshOffer_ReplacesOfferAndClearsFreeze()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new FreezeOfferCommand(player.Id)).Succeeded);

        var result = engine.Execute(match, new RefreshOfferCommand(player.Id));

        Assert.True(result.Succeeded);
        Assert.False(player.IsOfferFrozen);
        Assert.Equal(2, player.Resource);
        Assert.Equal(3, player.Offer.Count);
    }

    [Fact]
    public void ReorderField_AppliesExactRuntimeUnitOrder()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];

        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new EndPreparationCommand(player.Id)).Succeeded);
        Assert.True(engine.Execute(match, new EndPreparationCommand(match.Players[1].Id)).Succeeded);
        engine.BeginPreparation(match);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var original = player.Field.ToArray();
        Assert.Equal(2, original.Length);
        var revisionBefore = match.Revision;

        var result = engine.Execute(
            match,
            new ReorderFieldCommand(player.Id, [original[1].Id, original[0].Id]));

        Assert.True(result.Succeeded);
        Assert.Equal(revisionBefore + 1, match.Revision);
        Assert.Same(original[1], player.Field[0]);
        Assert.Same(original[0], player.Field[1]);
    }

    [Fact]
    public void ReorderField_RejectsDuplicateOrMissingRuntimeUnitsWithoutMutation()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];

        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new EndPreparationCommand(player.Id)).Succeeded);
        Assert.True(engine.Execute(match, new EndPreparationCommand(match.Players[1].Id)).Succeeded);
        engine.BeginPreparation(match);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var original = player.Field.ToArray();
        Assert.Equal(2, original.Length);
        var revisionBefore = match.Revision;

        var result = engine.Execute(
            match,
            new ReorderFieldCommand(player.Id, [original[0].Id, original[0].Id]));

        Assert.False(result.Succeeded);
        Assert.Equal(PreparationFailureCode.InvalidFieldOrder, result.FailureCode);
        Assert.Equal(revisionBefore, match.Revision);
        Assert.Same(original[0], player.Field[0]);
        Assert.Same(original[1], player.Field[1]);
    }

    [Fact]
    public void EndPreparation_TransitionsToCombatOnlyWhenEveryPlayerIsReady()
    {
        var (match, engine, _) = CreateStartedMatch();

        Assert.True(engine.Execute(match, new EndPreparationCommand(match.Players[0].Id)).Succeeded);
        Assert.Equal(MatchPhase.Preparation, match.Phase);
        Assert.True(engine.Execute(match, new EndPreparationCommand(match.Players[1].Id)).Succeeded);
        Assert.Equal(MatchPhase.Combat, match.Phase);
    }

    [Fact]
    public void NextPreparationRound_ResetsResourceAndReducesPendingUpgradeCost()
    {
        var (match, engine, _) = CreateStartedMatch();
        Assert.True(engine.Execute(match, new EndPreparationCommand(match.Players[0].Id)).Succeeded);
        Assert.True(engine.Execute(match, new EndPreparationCommand(match.Players[1].Id)).Succeeded);
        engine.BeginPreparation(match);

        var player = match.Players[0];
        Assert.Equal(2, match.Round);
        Assert.Equal(4, player.Resource);
        Assert.Equal(4, player.UpgradeCost);

        var upgrade = engine.Execute(match, new UpgradeTierCommand(player.Id));
        Assert.True(upgrade.Succeeded);
        Assert.Equal(2, player.Tier);
        Assert.Equal(0, player.Resource);
        Assert.Equal(7, player.UpgradeCost);
    }

    private static (MatchState Match, PreparationEngine Engine, UnitPool Pool) CreateStartedMatch()
    {
        var setup = CreateMatch();
        setup.Engine.BeginPreparation(setup.Match);
        return setup;
    }

    private static (MatchState Match, PreparationEngine Engine, UnitPool Pool) CreateMatch()
    {
        var definitions = new[]
        {
            new UnitDefinition(new UnitId("alpha"), "Alpha", 1, 1, 1),
            new UnitDefinition(new UnitId("beta"), "Beta", 1, 2, 2),
            new UnitDefinition(new UnitId("gamma"), "Gamma", 1, 2, 1),
        };
        var catalog = new UnitCatalog(definitions);
        var pool = new UnitPool(catalog, definitions.Select(definition => new UnitPoolEntry(definition.Id, 15)));
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 8));
        var rules = new PreparationRules(
            startingResource: 3,
            resourcePerRound: 1,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 6,
            offerSizesByTier: [3, 4, 4, 5, 5, 6],
            initialUpgradeCostsByTier: [5, 7, 8, 11, 11]);
        var engine = new PreparationEngine(rules, pool, new SeededRandomSource(1337));

        return (match, engine, pool);
    }
}
