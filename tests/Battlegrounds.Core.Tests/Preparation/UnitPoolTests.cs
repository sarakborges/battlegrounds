using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class UnitPoolTests
{
    [Fact]
    public void DrawOffer_OnlyUsesEligibleTiersAndConsumesCopies()
    {
        var tierOne = new UnitDefinition(new UnitId("tier-one"), "Tier One", 1, 1, 1);
        var tierTwo = new UnitDefinition(new UnitId("tier-two"), "Tier Two", 2, 2, 2);
        var catalog = new UnitCatalog([tierOne, tierTwo]);
        var pool = new UnitPool(
            catalog,
            [new UnitPoolEntry(tierOne.Id, 3), new UnitPoolEntry(tierTwo.Id, 3)]);

        var offer = pool.DrawOffer(1, 2, new SeededRandomSource(7));

        Assert.All(offer, unit => Assert.Equal(1, unit.Tier));
        Assert.Equal(1, pool.GetAvailableCopies(tierOne.Id));
        Assert.Equal(3, pool.GetAvailableCopies(tierTwo.Id));
    }

    [Fact]
    public void ExchangeOffer_ReturnsOldCopiesBeforeDrawingNewOnes()
    {
        var unit = new UnitDefinition(new UnitId("only-unit"), "Only Unit", 1, 1, 1);
        var catalog = new UnitCatalog([unit]);
        var pool = new UnitPool(catalog, [new UnitPoolEntry(unit.Id, 3)]);
        var random = new SeededRandomSource(1);

        var firstOffer = pool.DrawOffer(1, 3, random);
        Assert.Equal(0, pool.GetAvailableCopies(unit.Id));

        var replacement = pool.ExchangeOffer(firstOffer, 1, 3, random);

        Assert.Equal(3, replacement.Count);
        Assert.Equal(0, pool.GetAvailableCopies(unit.Id));
    }

    [Fact]
    public void FailedExchange_DoesNotCommitReturnedUnits()
    {
        var unit = new UnitDefinition(new UnitId("only-unit"), "Only Unit", 1, 1, 1);
        var catalog = new UnitCatalog([unit]);
        var pool = new UnitPool(catalog, [new UnitPoolEntry(unit.Id, 2)]);
        var random = new SeededRandomSource(1);
        var firstOffer = pool.DrawOffer(1, 1, random);
        Assert.Equal(1, pool.GetAvailableCopies(unit.Id));

        Assert.Throws<InvalidOperationException>(() => pool.ExchangeOffer(firstOffer, 1, 3, random));

        Assert.Equal(1, pool.GetAvailableCopies(unit.Id));
    }

    [Fact]
    public void SameSeedAndPoolState_ProduceSameOfferOrder()
    {
        var definitions = new[]
        {
            new UnitDefinition(new UnitId("a"), "A", 1, 1, 1),
            new UnitDefinition(new UnitId("b"), "B", 1, 1, 1),
            new UnitDefinition(new UnitId("c"), "C", 1, 1, 1),
        };
        var catalog = new UnitCatalog(definitions);
        var entries = definitions.Select(unit => new UnitPoolEntry(unit.Id, 5)).ToArray();
        var left = new UnitPool(catalog, entries);
        var right = new UnitPool(catalog, entries);

        var leftOffer = left.DrawOffer(1, 6, new SeededRandomSource(42));
        var rightOffer = right.DrawOffer(1, 6, new SeededRandomSource(42));

        Assert.Equal(leftOffer.Select(unit => unit.Id.Value), rightOffer.Select(unit => unit.Id.Value));
    }
}
