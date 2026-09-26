using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Match;

public sealed class MatchEngineTests
{
    [Fact]
    public void ResolveCombatRound_AppliesDamageCarriesCombatResourceAndStartsNextPreparation()
    {
        var engine = CreateEngine(startingHealth: 3);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);

        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(1));
        ReadyBoth(engine, match);

        var round = engine.ResolveCombatRound(
            match,
            [new CombatPairing(new PlayerId(0), new PlayerId(1))]);

        var settlement = Assert.Single(round.Settlements);
        Assert.Equal(new PlayerId(0), settlement.WinnerPlayerId);
        Assert.Equal(new PlayerId(1), settlement.DamagedPlayerId);
        Assert.Equal(2, settlement.PlayerDamage);
        Assert.Equal(1, settlement.DamagedPlayerHealthAfter);
        Assert.False(round.MatchFinished);
        Assert.Equal(MatchPhase.Preparation, match.Phase);
        Assert.Equal(2, match.Round);
        Assert.Equal(3, match.Players[0].Health);
        Assert.Equal(1, match.Players[1].Health);
        Assert.Equal(6, match.Players[0].Resource);
        Assert.Equal(4, match.Players[1].Resource);
    }

    [Fact]
    public void ResolveCombatRound_WhenLastOpponentReachesZero_FinishesMatch()
    {
        var engine = CreateEngine(startingHealth: 2);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);

        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(1));
        ReadyBoth(engine, match);

        var round = engine.ResolveCombatRound(
            match,
            [new CombatPairing(new PlayerId(0), new PlayerId(1))]);

        Assert.True(round.MatchFinished);
        Assert.Equal(MatchPhase.Finished, match.Phase);
        Assert.Equal(new PlayerId(0), round.WinnerPlayerId);
        Assert.Equal(new PlayerId(0), match.WinnerPlayerId);
        Assert.True(match.Players[1].IsEliminated);
        Assert.Equal(0, match.Players[1].Health);
    }

    [Fact]
    public void ResolveCombatRound_DrawDealsNoPlayerDamage()
    {
        var engine = CreateEngine(startingHealth: 10);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);

        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(1));
        ReadyBoth(engine, match);

        var round = engine.ResolveCombatRound(
            match,
            [new CombatPairing(new PlayerId(0), new PlayerId(1))]);

        var settlement = Assert.Single(round.Settlements);
        Assert.True(settlement.CombatResult.IsDraw);
        Assert.Equal(0, settlement.PlayerDamage);
        Assert.Null(settlement.DamagedPlayerId);
        Assert.Equal(10, match.Players[0].Health);
        Assert.Equal(10, match.Players[1].Health);
        Assert.Equal(MatchPhase.Preparation, match.Phase);
    }

    [Fact]
    public void ResolveCombatRound_OddActivePlayerCountRequiresGhostAssignmentSupport()
    {
        var engine = CreateEngine(startingHealth: 10, minimumPlayers: 2, maximumPlayers: 3);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1), new PlayerId(2)]);
        engine.BeginMatch(match);

        foreach (var player in match.Players)
        {
            Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(player.Id)).Succeeded);
        }

        Assert.Equal(MatchPhase.Combat, match.Phase);
        Assert.Throws<InvalidOperationException>(() => engine.ResolveCombatRound(
            match,
            [new CombatPairing(new PlayerId(0), new PlayerId(1))]));
    }

    private static MatchEngine CreateEngine(
        int startingHealth,
        int minimumPlayers = 2,
        int maximumPlayers = 8)
    {
        var unit = new UnitDefinition(
            new UnitId("fighter"),
            "Fighter",
            tier: 1,
            baseAttack: 2,
            baseHealth: 2,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnCombatEnd,
                    [new AddResourceEffectDefinition(2)]),
            ]);
        var catalog = new UnitCatalog([unit]);
        var pool = new UnitPool(catalog, [new UnitPoolEntry(unit.Id, 50)]);
        var behaviors = new BehaviorCatalog([]);
        var matchRules = new MatchRules(minimumPlayers, maximumPlayers, startingHealth);
        var preparationRules = new PreparationRules(
            startingResource: 3,
            resourcePerRound: 1,
            maximumResource: 10,
            acquireCost: 0,
            releaseValue: 1,
            refreshCost: 0,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [1, 1],
            initialUpgradeCostsByTier: [5]);
        var combatRules = new CombatRules(
            StartingSidePolicy.LargerFieldThenRandom,
            PostCombatDamagePolicy.WinnerTierPlusSurvivorTiers);

        return new MatchEngine(
            matchRules,
            preparationRules,
            combatRules,
            pool,
            new MinimumRandomSource(),
            catalog,
            behaviors);
    }

    private static void AddUnit(MatchEngine engine, MatchState match, PlayerId playerId)
    {
        var player = match.Players.Single(candidate => candidate.Id == playerId);
        if (player.Offer.Count == 0)
        {
            Assert.True(engine.ExecutePreparation(match, new RefreshOfferCommand(playerId)).Succeeded);
        }

        Assert.True(engine.ExecutePreparation(match, new AcquireUnitCommand(playerId, 0)).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new DeployUnitCommand(playerId, player.Reserve.Count - 1)).Succeeded);
    }

    private static void ReadyBoth(MatchEngine engine, MatchState match)
    {
        Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(new PlayerId(0))).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(new PlayerId(1))).Succeeded);
        Assert.Equal(MatchPhase.Combat, match.Phase);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
