using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Actions;

public sealed record ActionDefinition
{
    private readonly ReadOnlyCollection<EffectDefinition> _effects;

    public ActionId Id { get; }
    public string Name { get; }
    public int Tier { get; }
    public int Cost { get; }
    public IReadOnlyList<EffectDefinition> Effects => _effects;

    public ActionDefinition(
        ActionId id,
        string name,
        int tier,
        int cost,
        IEnumerable<EffectDefinition> effects)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Action name cannot be empty.", nameof(name));
        if (tier <= 0) throw new ArgumentOutOfRangeException(nameof(tier));
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        ArgumentNullException.ThrowIfNull(effects);
        var materialized = effects.ToArray();
        if (materialized.Length == 0) throw new ArgumentException("Action requires at least one effect.", nameof(effects));
        if (materialized.Any(effect => effect is null)) throw new ArgumentException("Action effects cannot contain null values.", nameof(effects));

        Id = id;
        Name = name;
        Tier = tier;
        Cost = cost;
        _effects = Array.AsReadOnly(materialized);
    }
}
