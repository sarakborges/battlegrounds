using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Cards;

public sealed record CardDefinition
{
    public CardId Id { get; }
    public string Name { get; }
    public int TavernTier { get; }
    public int BaseAttack { get; }
    public int BaseHealth { get; }

    public CardDefinition(CardId id, string name, int tavernTier, int baseAttack, int baseHealth)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Card name cannot be empty.", nameof(name));
        }

        if (tavernTier is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(tavernTier));
        }

        if (baseAttack < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseAttack));
        }

        if (baseHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseHealth));
        }

        Id = id;
        Name = name;
        TavernTier = tavernTier;
        BaseAttack = baseAttack;
        BaseHealth = baseHealth;
    }
}
