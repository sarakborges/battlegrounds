using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Recruitment;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Recruitment;

public sealed class TavernPoolTests
{
    [Fact]
    public void DrawOffer_OnlyUsesEligibleTiersAndConsumesCopies()
    {
        var tierOne = new CardDefinition(new CardId("tier-one"), "Tier One", 1, 1, 1);
        var tierTwo = new CardDefinition(new CardId("tier-two"), "Tier Two", 2, 2, 2);
        var catalog = new CardCatalog([tierOne, tierTwo]);
        var pool = new TavernPool(
            catalog,
            [new TavernPoolEntry(tierOne.Id, 3), new TavernPoolEntry(tierTwo.Id, 3)]);

        var offer = pool.DrawOffer(1, 2, new SeededRandomSource(7));

        Assert.All(offer, card => Assert.Equal(1, card.TavernTier));
        Assert.Equal(1, pool.GetAvailableCopies(tierOne.Id));
        Assert.Equal(3, pool.GetAvailableCopies(tierTwo.Id));
    }

    [Fact]
    public void ExchangeOffer_ReturnsOldCopiesBeforeDrawingNewOnes()
    {
        var card = new CardDefinition(new CardId("only-card"), "Only Card", 1, 1, 1);
        var catalog = new CardCatalog([card]);
        var pool = new TavernPool(catalog, [new TavernPoolEntry(card.Id, 3)]);
        var random = new SeededRandomSource(1);

        var firstOffer = pool.DrawOffer(1, 3, random);
        Assert.Equal(0, pool.GetAvailableCopies(card.Id));

        var replacement = pool.ExchangeOffer(firstOffer, 1, 3, random);

        Assert.Equal(3, replacement.Count);
        Assert.Equal(0, pool.GetAvailableCopies(card.Id));
    }

    [Fact]
    public void FailedExchange_DoesNotCommitReturnedCards()
    {
        var card = new CardDefinition(new CardId("only-card"), "Only Card", 1, 1, 1);
        var catalog = new CardCatalog([card]);
        var pool = new TavernPool(catalog, [new TavernPoolEntry(card.Id, 2)]);
        var random = new SeededRandomSource(1);
        var firstOffer = pool.DrawOffer(1, 1, random);
        Assert.Equal(1, pool.GetAvailableCopies(card.Id));

        Assert.Throws<InvalidOperationException>(() =>
            pool.ExchangeOffer(firstOffer, 1, 3, random));

        Assert.Equal(1, pool.GetAvailableCopies(card.Id));
    }

    [Fact]
    public void SameSeedAndPoolState_ProduceSameOfferOrder()
    {
        var definitions = new[]
        {
            new CardDefinition(new CardId("a"), "A", 1, 1, 1),
            new CardDefinition(new CardId("b"), "B", 1, 1, 1),
            new CardDefinition(new CardId("c"), "C", 1, 1, 1),
        };
        var catalog = new CardCatalog(definitions);
        var entries = definitions.Select(card => new TavernPoolEntry(card.Id, 5)).ToArray();
        var left = new TavernPool(catalog, entries);
        var right = new TavernPool(catalog, entries);

        var leftOffer = left.DrawOffer(1, 6, new SeededRandomSource(42));
        var rightOffer = right.DrawOffer(1, 6, new SeededRandomSource(42));

        Assert.Equal(
            leftOffer.Select(card => card.Id.Value),
            rightOffer.Select(card => card.Id.Value));
    }
}
