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
        ReadyActive(engine, match);

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
    public void ResolveCombatRound_ExpiresUntilCombatEndModifiersAfterARealCombat()
    {
        var engine = CreateEngine(startingHealth: 10);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);

        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(1));
        var unit = Assert.Single(match.Players[0].Field);
        unit.ApplyModifier("next-combat", 4, 3, UnitModifierDuration.UntilCombatEnd);
        var temporaryBehavior = new BehaviorDefinition(new BehaviorId("temporary-ward"), "Temporary Ward", NativeBehaviorKeys.DamageBarrier);
        Assert.True(unit.AddBehavior(temporaryBehavior, UnitModifierDuration.UntilCombatEnd));
        Assert.Equal(6, unit.Attack);
        Assert.Equal(5, unit.Health);
        ReadyActive(engine, match);

        engine.ResolveCombatRound(match, [new CombatPairing(new PlayerId(0), new PlayerId(1))]);

        Assert.Equal(2, unit.Attack);
        Assert.Equal(2, unit.Health);
        Assert.DoesNotContain(unit.Modifiers, modifier => modifier.Key == "next-combat");
        Assert.DoesNotContain(unit.Behaviors, behavior => behavior.Id == temporaryBehavior.Id);
    }

    [Fact]
    public void ResolveCombatRound_ByeDoesNotConsumeUntilCombatEndModifiers()
    {
        var engine = CreateEngine(startingHealth: 10, minimumPlayers: 2, maximumPlayers: 3);
        var ids = new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2) };
        var match = engine.CreateMatch(ids);
        engine.BeginMatch(match);
        AddUnit(engine, match, ids[2]);
        var byeUnit = Assert.Single(match.Players[2].Field);
        byeUnit.ApplyModifier("next-combat", 3, 0, UnitModifierDuration.UntilCombatEnd);
        var temporaryBehavior = new BehaviorDefinition(new BehaviorId("temporary-guard"), "Temporary Guard", NativeBehaviorKeys.TargetPriority);
        Assert.True(byeUnit.AddBehavior(temporaryBehavior, UnitModifierDuration.UntilCombatEnd));
        ReadyActive(engine, match);

        engine.ResolveCombatRound(match, [new CombatPairing(ids[0], ids[1]), CombatPairing.Bye(ids[2])]);

        Assert.Equal(5, byeUnit.Attack);
        Assert.Contains(byeUnit.Modifiers, modifier => modifier.Key == "next-combat");
        Assert.Contains(byeUnit.Behaviors, behavior => behavior.Id == temporaryBehavior.Id);
    }

    [Fact]
    public void ResolveCombatRound_WhenLastOpponentReachesZero_FinishesMatchAndAssignsPlacements()
    {
        var engine = CreateEngine(startingHealth: 2);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);

        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(1));
        ReadyActive(engine, match);

        var round = engine.ResolveCombatRound(
            match,
            [new CombatPairing(new PlayerId(0), new PlayerId(1))]);

        Assert.True(round.MatchFinished);
        Assert.Equal(MatchPhase.Finished, match.Phase);
        Assert.Equal(new PlayerId(0), round.WinnerPlayerId);
        Assert.Equal(new PlayerId(0), match.WinnerPlayerId);
        Assert.True(match.Players[1].IsEliminated);
        Assert.Equal(0, match.Players[1].Health);
        Assert.True(match.TryGetPlacement(new PlayerId(0), out var winnerPlacement));
        Assert.Equal(1, winnerPlacement);
        Assert.True(match.TryGetPlacement(new PlayerId(1), out var loserPlacement));
        Assert.Equal(2, loserPlacement);
    }

    [Fact]
    public void ResolveCombatRound_EliminationReturnsOwnedAndOfferedPoolCopies()
    {
        var unit = new UnitDefinition(new UnitId("fighter"), "Fighter", 1, 2, 2);
        var catalog = new UnitCatalog([unit]);
        var pool = new UnitPool(catalog, [new UnitPoolEntry(unit.Id, 10)]);
        var engine = new MatchEngine(
            new MatchRules(2, 2, startingHealth: 2),
            new PreparationRules(
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
                initialUpgradeCostsByTier: [5]),
            new CombatRules(StartingSidePolicy.LargerFieldThenRandom, PostCombatDamagePolicy.WinnerTierPlusSurvivorTiers),
            pool,
            new MinimumRandomSource(),
            catalog,
            new BehaviorCatalog([]));
        var winnerId = new PlayerId(0);
        var loserId = new PlayerId(1);
        var match = engine.CreateMatch([winnerId, loserId]);
        engine.BeginMatch(match);

        AddUnit(engine, match, winnerId);
        AddUnit(engine, match, winnerId);
        AddUnit(engine, match, loserId);
        Assert.True(engine.ExecutePreparation(match, new RefreshOfferCommand(loserId)).Succeeded);
        Assert.Equal(6, pool.GetAvailableCopies(unit.Id));

        ReadyActive(engine, match);
        var round = engine.ResolveCombatRound(match, [new CombatPairing(winnerId, loserId)]);

        Assert.True(round.MatchFinished);
        var loser = match.Players.Single(player => player.Id == loserId);
        Assert.True(loser.IsEliminated);
        Assert.Empty(loser.Offer);
        Assert.Single(loser.Field);
        Assert.Equal(8, pool.GetAvailableCopies(unit.Id));
        Assert.NotNull(match.LatestEliminatedOpponent);
        Assert.Equal(loserId, match.LatestEliminatedOpponent!.SourcePlayerId);
        Assert.Single(match.LatestEliminatedOpponent.Participant.Units);
    }

    [Fact]
    public void ResolveCombatRound_DrawDealsNoPlayerDamage()
    {
        var engine = CreateEngine(startingHealth: 10);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);

        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(1));
        ReadyActive(engine, match);

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
    public void ResolveCombatRound_InitialOddPlayerCountUsesByeWithoutFakeSettlement()
    {
        var engine = CreateEngine(startingHealth: 10, minimumPlayers: 2, maximumPlayers: 3);
        var players = new[] { new PlayerId(0), new PlayerId(1), new PlayerId(2) };
        var match = engine.CreateMatch(players);
        engine.BeginMatch(match);
        ReadyActive(engine, match);

        var round = engine.ResolveCombatRound(
            match,
            [
                new CombatPairing(players[0], players[1]),
                CombatPairing.Bye(players[2]),
            ]);

        Assert.Single(round.Settlements);
        Assert.False(round.MatchFinished);
        Assert.Equal(MatchPhase.Preparation, match.Phase);
        Assert.Equal(2, match.Round);
        Assert.Equal(10, match.Players.Single(player => player.Id == players[2]).Health);
        var byeHistory = Assert.Single(match.CombatPairingHistory, entry => entry.IsBye);
        Assert.Equal(players[2], byeHistory.LeftPlayerId);
        Assert.Null(byeHistory.RightPlayerId);
        Assert.Null(byeHistory.EliminatedOpponentSourcePlayerId);
    }

    [Fact]
    public void ResolveCombatRound_OddPlayersUseLatestEliminatedSnapshotAndRecordDeterministicPlacements()
    {
        var engine = CreateEngine(startingHealth: 2, minimumPlayers: 2, maximumPlayers: 4);
        var match = engine.CreateMatch(
            [new PlayerId(0), new PlayerId(1), new PlayerId(2), new PlayerId(3)]);
        engine.BeginMatch(match);

        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(0));
        AddUnit(engine, match, new PlayerId(1));
        ReadyActive(engine, match);

        var firstRound = engine.ResolveCombatRound(
            match,
            [
                new CombatPairing(new PlayerId(0), new PlayerId(1)),
                new CombatPairing(new PlayerId(2), new PlayerId(3)),
            ]);

        Assert.False(firstRound.MatchFinished);
        Assert.True(match.Players.Single(player => player.Id == new PlayerId(1)).IsEliminated);
        Assert.NotNull(match.LatestEliminatedOpponent);
        Assert.Equal(new PlayerId(1), match.LatestEliminatedOpponent!.SourcePlayerId);
        Assert.Equal(1, match.LatestEliminatedOpponent.Tier);
        Assert.Single(match.LatestEliminatedOpponent.Participant.Units);
        Assert.True(match.TryGetPlacement(new PlayerId(1), out var fourth));
        Assert.Equal(4, fourth);

        ReadyActive(engine, match);
        var secondRound = engine.ResolveCombatRound(
            match,
            [
                new CombatPairing(new PlayerId(0), new PlayerId(3)),
                CombatPairing.VersusEliminatedOpponent(new PlayerId(2)),
            ]);

        var archivedSettlement = Assert.Single(
            secondRound.Settlements,
            settlement => settlement.UsesEliminatedOpponent);
        Assert.Equal(new PlayerId(1), archivedSettlement.EliminatedOpponentSourcePlayerId);
        Assert.True(archivedSettlement.EliminatedOpponentWon);
        Assert.Null(archivedSettlement.WinnerPlayerId);
        Assert.Equal(new PlayerId(2), archivedSettlement.DamagedPlayerId);
        Assert.Equal(2, archivedSettlement.PlayerDamage);

        Assert.True(secondRound.MatchFinished);
        Assert.Equal(new PlayerId(0), match.WinnerPlayerId);
        Assert.Equal(new PlayerId(2), match.LatestEliminatedOpponent!.SourcePlayerId);
        Assert.Equal(
            [new PlayerId(1), new PlayerId(3), new PlayerId(2)],
            match.Eliminations.Select(record => record.PlayerId));
        Assert.Equal([4, 3, 2], match.Eliminations.Select(record => record.Placement));
        Assert.True(match.TryGetPlacement(new PlayerId(0), out var first));
        Assert.True(match.TryGetPlacement(new PlayerId(2), out var second));
        Assert.True(match.TryGetPlacement(new PlayerId(3), out var third));
        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(3, third);
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

    private static void ReadyActive(MatchEngine engine, MatchState match)
    {
        foreach (var player in match.Players.Where(player => !player.IsEliminated))
        {
            Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(player.Id)).Succeeded);
        }

        Assert.Equal(MatchPhase.Combat, match.Phase);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
