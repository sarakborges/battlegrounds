using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Units;

public sealed record UnitDefinition
{
    public UnitId Id { get; }
    public string Name { get; }
    public int Tier { get; }
    public int BaseAttack { get; }
    public int BaseHealth { get; }

    public UnitDefinition(UnitId id, string name, int tier, int baseAttack, int baseHealth)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Unit name cannot be empty.", nameof(name));
        }

        if (tier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
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
        Tier = tier;
        BaseAttack = baseAttack;
        BaseHealth = baseHealth;
    }
}
