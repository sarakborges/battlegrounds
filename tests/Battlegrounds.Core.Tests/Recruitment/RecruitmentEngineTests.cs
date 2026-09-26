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
        var (match, engine) = CreateMatch();

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
        });
    }

    [Fact]
    public void BuyMinion_MovesDefinitionIntoRuntimeHandAndSpendsGold()
    {
        var (match, engine) = CreateStartedMatch();
        var player = match.Players[0];

        var result = engine.Execute(match, new BuyMinionCommand(player.Id, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(0, player.Gold);
        Assert.Equal(2, player.TavernOffer.Count);
        var minion = Assert.Single(player.Hand);
        Assert.Equal(new CardId("alleycat"), minion.Definition.Id);
        Assert.Equal(1, minion.Attack);
        Assert.Equal(1, minion.Health);
        Assert.Equal(2, match.Revision);
    }

    [Fact]
    public void FailedCommand_DoesNotMutateStateOrRevision()
    {
        var (match, engine) = CreateStartedMatch();
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
    public void PlayThenSell_UsesControlledMutationPaths()
    {
        var (match, engine) = CreateStartedMatch();
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new BuyMinionCommand(player.Id, 0)).Succeeded);

        Assert.True(engine.Execute(match, new PlayMinionCommand(player.Id, 0)).Succeeded);
        Assert.Empty(player.Hand);
        Assert.Single(player.Board);

        Assert.True(engine.Execute(match, new SellMinionCommand(player.Id, 0)).Succeeded);
        Assert.Empty(player.Board);
        Assert.Equal(1, player.Gold);
    }

    [Fact]
    public void EndRecruitment_TransitionsToCombatOnlyWhenEveryPlayerIsReady()
    {
        var (match, engine) = CreateStartedMatch();

        Assert.True(engine.Execute(match, new EndRecruitmentCommand(match.Players[0].Id)).Succeeded);
        Assert.Equal(MatchPhase.Recruitment, match.Phase);

        Assert.True(engine.Execute(match, new EndRecruitmentCommand(match.Players[1].Id)).Succeeded);
        Assert.Equal(MatchPhase.Combat, match.Phase);
    }

    [Fact]
    public void NextRecruitmentRound_ResetsGoldAndReducesPendingUpgradeCost()
    {
        var (match, engine) = CreateStartedMatch();
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

    private static (MatchState Match, RecruitmentEngine Engine) CreateStartedMatch()
    {
        var pair = CreateMatch();
        pair.Engine.BeginRecruitment(pair.Match);
        return pair;
    }

    private static (MatchState Match, RecruitmentEngine Engine) CreateMatch()
    {
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)]);
        var engine = new RecruitmentEngine(
            RecruitmentRules.Standard,
            new RepeatingOfferSource(),
            new SeededRandomSource(1337));

        return (match, engine);
    }

    private sealed class RepeatingOfferSource : ITavernOfferSource
    {
        private static readonly CardDefinition Card = new(
            new CardId("alleycat"),
            "Alleycat",
            tavernTier: 1,
            baseAttack: 1,
            baseHealth: 1);

        public IReadOnlyList<CardDefinition> DrawOffer(
            int tavernTier,
            int count,
            IRandomSource randomSource)
        {
            return Enumerable.Repeat(Card, count).ToArray();
        }
    }
}
