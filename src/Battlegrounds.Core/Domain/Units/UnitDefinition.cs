using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Units;

public sealed record UnitDefinition
{
    private readonly ReadOnlyCollection<BehaviorDefinition> _behaviors;

    public UnitId Id { get; }
    public string Name { get; }
    public int Tier { get; }
    public int BaseAttack { get; }
    public int BaseHealth { get; }
    public IReadOnlyList<BehaviorDefinition> Behaviors => _behaviors;

    public UnitDefinition(
        UnitId id,
        string name,
        int tier,
        int baseAttack,
        int baseHealth,
        IEnumerable<BehaviorDefinition>? behaviors = null)
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

        var behaviorArray = behaviors?.ToArray() ?? [];
        if (behaviorArray.Any(behavior => behavior is null))
        {
            throw new ArgumentException("Unit behaviors cannot contain null definitions.", nameof(behaviors));
        }

        var duplicateId = behaviorArray
            .GroupBy(behavior => behavior.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new ArgumentException($"Duplicate unit behavior id '{duplicateId.Key}'.", nameof(behaviors));
        }

        var duplicateHandler = behaviorArray
            .GroupBy(behavior => behavior.Handler)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateHandler is not null)
        {
            throw new ArgumentException(
                $"A unit cannot attach native behavior '{duplicateHandler.Key}' more than once.",
                nameof(behaviors));
        }

        Id = id;
        Name = name;
        Tier = tier;
        BaseAttack = baseAttack;
        BaseHealth = baseHealth;
        _behaviors = Array.AsReadOnly(behaviorArray);
    }
}
