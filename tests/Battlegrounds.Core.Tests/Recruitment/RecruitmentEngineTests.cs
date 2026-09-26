using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Recruitment;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Recruitment;

public sealed class RecruitmentEngineTests
{
    [Fact]
    public void BeginRecruitment_InitializesRoundState()
    {
        var (match, engine, _) = CreateMatch();

        engine.BeginRecruitment(match);

        Assert.Equal(MatchPhase.Recruitment, match.Phase);
        Assert.Equal(1, match.Round);
        Assert.Equal(1, match.Revision);
        Assert.All(match.Players, player =>
        {
            Assert.Equal(3, player.Gold);
            Assert.Equal(1, player.TavernTier);
            Assert.Equal(5, player.UpgradeCost);
            Assert.Equal(3, player.TavernOffer.Count);
            Assert.False(player.IsReadyForCombat);
            Assert.False(player.IsTavernFrozen);
        });
    }

    [Fact]
    public void BuyMinion_MovesDefinitionIntoRuntimeHandAndSpendsGold()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];
        var boughtDefinition = player.TavernOffer[0];

        var result = engine.Execute(match, new BuyMinionCommand(player.Id, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(0, player.Gold);
        Assert.Equal(2, player.TavernOffer.Count);
        var minion = Assert.Single(player.Hand);
        Assert.Same(boughtDefinition, minion.Definition);
        Assert.Equal(boughtDefinition.BaseAttack, minion.Attack);
        Assert.Equal(boughtDefinition.BaseHealth, minion.Health);
        Assert.Equal(2, match.Revision);
    }

    [Fact]
    public void FailedCommand_DoesNotMutateStateOrRevision()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new BuyMinionCommand(player.Id, 0)).Succeeded);
        var revisionBeforeFailure = match.Revision;

        var result = engine.Execute(match, new BuyMinionCommand(player.Id, 0));

        Assert.False(result.Succeeded);
        Assert.Equal(RecruitmentFailureCode.InsufficientGold, result.FailureCode);
        Assert.Equal(revisionBeforeFailure, match.Revision);
        Assert.Single(player.Hand);
        Assert.Equal(2, player.TavernOffer.Count);
    }

    [Fact]
    public void PlayThenSell_ReturnsMinionCopyToSharedPool()
    {
        var (match, engine, pool) = CreateStartedMatch();
        var player = match.Players[0];
        var boughtCard = player.TavernOffer[0];
        var copiesBeforeBuy = pool.GetAvailableCopies(boughtCard.Id);

        Assert.True(engine.Execute(match, new BuyMinionCommand(player.Id, 0)).Succeeded);
        Assert.Equal(copiesBeforeBuy, pool.GetAvailableCopies(boughtCard.Id));

        Assert.True(engine.Execute(match, new PlayMinionCommand(player.Id, 0)).Succeeded);
        Assert.Empty(player.Hand);
        Assert.Single(player.Board);

        Assert.True(engine.Execute(match, new SellMinionCommand(player.Id, 0)).Succeeded);
        Assert.Empty(player.Board);
        Assert.Equal(1, player.Gold);
        Assert.Equal(copiesBeforeBuy + 1, pool.GetAvailableCopies(boughtCard.Id));
    }

    [Fact]
    public void FreezeTavern_PreservesRemainingOfferAndFillsMissingSlotsNextRound()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];

        Assert.True(engine.Execute(match, new BuyMinionCommand(player.Id, 0)).Succeeded);
        var frozenCards = player.TavernOffer.ToArray();
        Assert.True(engine.Execute(match, new FreezeTavernCommand(player.Id)).Succeeded);

        Assert.True(engine.Execute(match, new EndRecruitmentCommand(player.Id)).Succeeded);
        Assert.True(engine.Execute(match, new EndRecruitmentCommand(match.Players[1].Id)).Succeeded);

        engine.BeginRecruitment(match);

        Assert.True(player.IsTavernFrozen);
        Assert.Equal(3, player.TavernOffer.Count);
        Assert.Same(frozenCards[0], player.TavernOffer[0]);
        Assert.Same(frozenCards[1], player.TavernOffer[1]);
    }

    [Fact]
    public void FreezeTavern_CanBeToggledRepeatedlyWithoutLimit()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];

        for (var index = 0; index < 20; index++)
        {
            Assert.True(engine.Execute(match, new FreezeTavernCommand(player.Id)).Succeeded);
            Assert.True(player.IsTavernFrozen);
            Assert.True(engine.Execute(match, new UnfreezeTavernCommand(player.Id)).Succeeded);
            Assert.False(player.IsTavernFrozen);
        }
    }

    [Fact]
    public void RefreshTavern_ReplacesOfferAndClearsFreeze()
    {
        var (match, engine, _) = CreateStartedMatch();
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new FreezeTavernCommand(player.Id)).Succeeded);

        var result = engine.Execute(match, new RefreshTavernCommand(player.Id));

        Assert.True(result.Succeeded);
        Assert.False(player.IsTavernFrozen);
        Assert.Equal(2, player.Gold);
        Assert.Equal(3, player.TavernOffer.Count);
    }

    [Fact]
    public void EndRecruitment_TransitionsToCombatOnlyWhenEveryPlayerIsReady()
    {
        var (match, engine, _) = CreateStartedMatch();

        Assert.True(engine.Execute(match, new EndRecruitmentCommand(match.Players[0].Id)).Succeeded);
        Assert.Equal(MatchPhase.Recruitment, match.Phase);

        Assert.True(engine.Execute(match, new EndRecruitmentCommand(match.Players[1].Id)).Succeeded);
        Assert.Equal(MatchPhase.Combat, match.Phase);
    }

    [Fact]
    public void NextRecruitmentRound_ResetsGoldAndReducesPendingUpgradeCost()
    {
        var (match, engine, _) = CreateStartedMatch();
        Assert.True(engine.Execute(match, new EndRecruitmentCommand(match.Players[0].Id)).Succeeded);
        Assert.True(engine.Execute(match, new EndRecruitmentCommand(match.Players[1].Id)).Succeeded);

        engine.BeginRecruitment(match);

        var player = match.Players[0];
        Assert.Equal(2, match.Round);
        Assert.Equal(4, player.Gold);
        Assert.Equal(4, player.UpgradeCost);

        var upgrade = engine.Execute(match, new UpgradeTavernCommand(player.Id));
        Assert.True(upgrade.Succeeded);
        Assert.Equal(2, player.TavernTier);
        Assert.Equal(0, player.Gold);
        Assert.Equal(7, player.UpgradeCost);
    }

    private static (MatchState Match, RecruitmentEngine Engine, TavernPool Pool) CreateStartedMatch()
    {
        var setup = CreateMatch();
        setup.Engine.BeginRecruitment(setup.Match);
        return setup;
    }

    private static (MatchState Match, RecruitmentEngine Engine, TavernPool Pool) CreateMatch()
    {
        var definitions = new[]
        {
            new CardDefinition(new CardId("alleycat"), "Alleycat", 1, 1, 1),
            new CardDefinition(new CardId("deck-swabbie"), "Deck Swabbie", 1, 2, 2),
            new CardDefinition(new CardId("scallywag"), "Scallywag", 1, 2, 1),
        };

        var catalog = new CardCatalog(definitions);
        var pool = new TavernPool(
            catalog,
            definitions.Select(definition => new TavernPoolEntry(definition.Id, 15)));
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)]);
        var engine = new RecruitmentEngine(
            RecruitmentRules.Standard,
            pool,
            new SeededRandomSource(1337));

        return (match, engine, pool);
    }
}
