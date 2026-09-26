using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Recruitment;

public interface ITavernOfferSource
{
    IReadOnlyList<CardDefinition> DrawOffer(int tavernTier, int count, IRandomSource randomSource);
}
