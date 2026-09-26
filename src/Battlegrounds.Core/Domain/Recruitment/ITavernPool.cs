using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Recruitment;

public interface ITavernPool
{
    IReadOnlyList<CardDefinition> DrawOffer(int tavernTier, int count, IRandomSource randomSource);

    IReadOnlyList<CardDefinition> ExchangeOffer(
        IReadOnlyCollection<CardDefinition> returnedCards,
        int tavernTier,
        int count,
        IRandomSource randomSource);

    void ReturnMinion(CardDefinition definition);

    int GetAvailableCopies(CardId cardId);
}
