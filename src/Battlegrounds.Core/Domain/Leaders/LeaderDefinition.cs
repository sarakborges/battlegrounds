using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Leaders;

public sealed class LeaderDefinition
{
    public LeaderId Id { get; }
    public string Name { get; }
    public int HealthModifier { get; }
    public int StartingArmor { get; }

    public LeaderDefinition(
        LeaderId id,
        string name,
        int healthModifier,
        int startingArmor)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Leader name cannot be empty.", nameof(name));
        }

        if (startingArmor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startingArmor));
        }

        Id = id;
        Name = name;
        HealthModifier = healthModifier;
        StartingArmor = startingArmor;
    }
}
