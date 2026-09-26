using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Tests.Cards;

public sealed class CardCatalogTests
{
    [Fact]
    public void Constructor_RejectsDuplicateCardIds()
    {
        var id = new CardId("duplicate");

        var exception = Assert.Throws<ArgumentException>(() => new CardCatalog(
        [
            new CardDefinition(id, "First", 1, 1, 1),
            new CardDefinition(id, "Second", 2, 2, 2),
        ]));

        Assert.Contains("Duplicate card id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void All_IsSortedByStableCardId()
    {
        var catalog = new CardCatalog(
        [
            new CardDefinition(new CardId("z-card"), "Z", 1, 1, 1),
            new CardDefinition(new CardId("a-card"), "A", 1, 1, 1),
        ]);

        Assert.Equal(["a-card", "z-card"], catalog.All.Select(card => card.Id.Value));
    }
}
