using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Match;

public sealed class MatchmakingPolicyTests
{
    [Fact]
    public void CreatePairings_PrefersOpponentWithFewerPriorMeetings()
    {
        var players = Enumerable.Range(0, 4).Select(value => new PlayerId(value)).ToArray();
        var match = MatchState.Create(players, new MatchRules(2, 4));
        match.BeginPreparation();
        match.BeginCombat();
        match.RecordCombatPairings(
            [
                new CombatPairing(players[0], players[1]),
                new CombatPairing(players[2], players[3]),
            ],
            eliminatedOpponent: null);
        match.BeginPreparation();
        match.BeginCombat();
        var policy = new HistoryAwareCombatPairingPolicy();

        var pairings = policy.CreatePairings(match, new MinimumRandomSource());

        Assert.Equal(2, pairings.Count);
        Assert.Contains(pairings, pairing =>
            pairing.LeftPlayerId == players[0] && pairing.RightPlayerId == players[2]);
        Assert.Contains(pairings, pairing =>
            pairing.LeftPlayerId == players[1] && pairing.RightPlayerId == players[3]);
    }

    [Fact]
    public void CreatePairings_InitialOddCountAssignsAndRotatesByeByHistory()
    {
        var players = Enumerable.Range(0, 3).Select(value => new PlayerId(value)).ToArray();
        var match = MatchState.Create(players, new MatchRules(2, 3));
        match.BeginPreparation();
        match.BeginCombat();
        var policy = new HistoryAwareCombatPairingPolicy();

        var firstPairings = policy.CreatePairings(match, new MinimumRandomSource());

        var firstBye = Assert.Single(firstPairings, pairing => pairing.IsBye);
        Assert.Equal(players[0], firstBye.LeftPlayerId);
        Assert.Single(firstPairings, pairing => !pairing.IsBye && !pairing.UsesEliminatedOpponent);

        match.RecordCombatPairings(firstPairings, eliminatedOpponent: null);
        match.BeginPreparation();
        match.BeginCombat();

        var secondPairings = policy.CreatePairings(match, new MinimumRandomSource());

        var secondBye = Assert.Single(secondPairings, pairing => pairing.IsBye);
        Assert.Equal(players[1], secondBye.LeftPlayerId);
    }

    [Fact]
    public void CreatePairings_DistributesEliminatedOpponentPairingsByHistory()
    {
        var players = Enumerable.Range(0, 4).Select(value => new PlayerId(value)).ToArray();
        var match = MatchState.Create(players, new MatchRules(2, 4));
        match.BeginPreparation();
        match.BeginCombat();
        match.Players[3].TakeDamage(30);
        match.RecordEliminations(
            [players[3]],
            new Dictionary<PlayerId, int> { [players[3]] = 30 });
        match.RecordCombatPairings(
            [
                CombatPairing.VersusEliminatedOpponent(players[0]),
                new CombatPairing(players[1], players[2]),
            ],
            match.LatestEliminatedOpponent);
        match.BeginPreparation();
        match.BeginCombat();
        var policy = new HistoryAwareCombatPairingPolicy();

        var pairings = policy.CreatePairings(match, new MinimumRandomSource());

        var eliminatedOpponentPairing = Assert.Single(pairings, pairing => pairing.UsesEliminatedOpponent);
        Assert.Equal(players[1], eliminatedOpponentPairing.LeftPlayerId);
        var livePairing = Assert.Single(pairings, pairing => !pairing.UsesEliminatedOpponent && !pairing.IsBye);
        Assert.Equal(players[0], livePairing.LeftPlayerId);
        Assert.Equal(players[2], livePairing.RightPlayerId);
    }

    [Fact]
    public void ResolveCombatRound_RecordsPairingsUsedByTheEngine()
    {
        var unit = new UnitDefinition(new UnitId("fixture"), "Fixture", 1, 1, 1);
        var units = new UnitCatalog([unit]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 4)]);
        var random = new MinimumRandomSource();
        var preparationRules = new PreparationRules(
            3, 0, 10, 3, 1, 1, 7, 10, 2,
            offerSizesByTier: [1, 1],
            initialUpgradeCostsByTier: [5]);
        var engine = new MatchEngine(
            new MatchRules(2, 2),
            preparationRules,
            new CombatRules(StartingSidePolicy.Random),
            pool,
            random,
            unitCatalog: units);
        var left = new PlayerId(0);
        var right = new PlayerId(1);
        var match = engine.CreateMatch([left, right]);
        engine.BeginMatch(match);
        Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(left)).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(right)).Succeeded);

        engine.ResolveCombatRound(match, [new CombatPairing(left, right)]);

        var history = Assert.Single(match.CombatPairingHistory);
        Assert.Equal(1, history.Round);
        Assert.Equal(left, history.LeftPlayerId);
        Assert.Equal(right, history.RightPlayerId);
        Assert.False(history.UsesEliminatedOpponent);
        Assert.False(history.IsBye);
        Assert.Null(history.EliminatedOpponentSourcePlayerId);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
