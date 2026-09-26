using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Cards;

public sealed record CardDefinition(
    CardId Id,
    string Name,
    int TavernTier,
    int BaseAttack,
    int BaseHealth)
{
    public CardDefinition : this(Id, Name, TavernTier, BaseAttack, BaseHealth)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Card name cannot be empty.", nameof(Name));
        }

        if (TavernTier is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(TavernTier));
        }

        if (BaseAttack < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseAttack));
        }

        if (BaseHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaseHealth));
        }
    }
}
