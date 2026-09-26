using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Match;

public sealed class LeaderSelectionStateTests
{
    [Fact]
    public void UniqueAcrossMatch_OffersDoNotOverlap()
    {
        var state = LeaderSelectionState.Create(
            [new PlayerId(1), new PlayerId(0)],
            new MatchRules(2, 8),
            new LeaderSelectionRules(2, LeaderOfferPolicy.UniqueAcrossMatch),
            CreateCatalog(4),
            new SeededRandomSource(123));

        var first = state.GetOffer(new PlayerId(0));
        var second = state.GetOffer(new PlayerId(1));

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Empty(first.Intersect(second));
        Assert.Equal([new PlayerId(0), new PlayerId(1)], state.Players);
    }

    [Fact]
    public void IndependentOffers_AreDeterministicForSameSeed()
    {
        var rules = new MatchRules(2, 8);
        var selectionRules = new LeaderSelectionRules(3, LeaderOfferPolicy.IndependentPerPlayer);
        var catalog = CreateCatalog(5);
        var players = new[] { new PlayerId(0), new PlayerId(1) };

        var left = LeaderSelectionState.Create(players, rules, selectionRules, catalog, new SeededRandomSource(77));
        var right = LeaderSelectionState.Create(players, rules, selectionRules, catalog, new SeededRandomSource(77));

        Assert.Equal(left.GetOffer(players[0]), right.GetOffer(players[0]));
        Assert.Equal(left.GetOffer(players[1]), right.GetOffer(players[1]));
        Assert.Equal(3, left.GetOffer(players[0]).Distinct().Count());
        Assert.Equal(3, left.GetOffer(players[1]).Distinct().Count());
    }

    [Fact]
    public void Select_RejectsLeaderOutsideOfferAndDuplicateSelection()
    {
        var player = new PlayerId(0);
        var state = LeaderSelectionState.Create(
            [player, new PlayerId(1)],
            new MatchRules(2, 8),
            new LeaderSelectionRules(1, LeaderOfferPolicy.IndependentPerPlayer),
            CreateCatalog(2),
            new SeededRandomSource(2));
        var offered = Assert.Single(state.GetOffer(player));
        var notOffered = CreateCatalog(2).All.Select(leader => leader.Id).Single(id => id != offered);

        var rejected = state.Select(player, notOffered);
        var accepted = state.Select(player, offered);
        var duplicate = state.Select(player, offered);

        Assert.False(rejected.Succeeded);
        Assert.Equal(LeaderSelectionFailureCode.LeaderNotOffered, rejected.FailureCode);
        Assert.True(accepted.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(LeaderSelectionFailureCode.PlayerAlreadySelected, duplicate.FailureCode);
    }

    [Fact]
    public void CompletedSelection_ProducesValidatedPlayerSetups()
    {
        var players = new[] { new PlayerId(0), new PlayerId(1) };
        var state = LeaderSelectionState.Create(
            players,
            new MatchRules(2, 8),
            new LeaderSelectionRules(2, LeaderOfferPolicy.IndependentPerPlayer),
            CreateCatalog(3),
            new SeededRandomSource(5));

        Assert.Throws<InvalidOperationException>(() => state.GetCompletedPlayerSetups());

        foreach (var player in players)
        {
            Assert.True(state.Select(player, state.GetOffer(player)[0]).Succeeded);
        }

        var setups = state.GetCompletedPlayerSetups();
        Assert.True(state.IsComplete);
        Assert.Equal(2, setups.Count);
        Assert.All(setups, setup => Assert.Contains(setup.LeaderId, state.GetOffer(setup.PlayerId)));
    }

    [Fact]
    public void UniqueAcrossMatch_RejectsInsufficientCatalog()
    {
        Assert.Throws<InvalidOperationException>(() => LeaderSelectionState.Create(
            [new PlayerId(0), new PlayerId(1)],
            new MatchRules(2, 8),
            new LeaderSelectionRules(2, LeaderOfferPolicy.UniqueAcrossMatch),
            CreateCatalog(3),
            new SeededRandomSource(1)));
    }

    private static LeaderCatalog CreateCatalog(int count) =>
        new(Enumerable.Range(0, count).Select(index =>
            new LeaderDefinition(
                new LeaderId($"leader-{index}"),
                $"Leader {index}",
                healthModifier: 0,
                startingArmor: 0)));
}
