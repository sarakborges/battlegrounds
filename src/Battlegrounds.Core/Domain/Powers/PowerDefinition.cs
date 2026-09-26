using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Powers;

public sealed class PowerDefinition
{
    private readonly ReadOnlyCollection<EffectDefinition> _effects;

    public PowerId Id { get; }
    public string Name { get; }
    public int Cost { get; }
    public int MaxUsesPerTurn { get; }
    public int? MaxUsesPerMatch { get; }
    public IReadOnlyList<EffectDefinition> Effects => _effects;

    public PowerDefinition(
        PowerId id,
        string name,
        int cost,
        int maxUsesPerTurn,
        IEnumerable<EffectDefinition> effects,
        int? maxUsesPerMatch = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Power name cannot be empty.", nameof(name));
        if (cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost));
        if (maxUsesPerTurn <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxUsesPerTurn));
        if (maxUsesPerMatch is not null && maxUsesPerMatch.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxUsesPerMatch));

        ArgumentNullException.ThrowIfNull(effects);
        var materialized = effects.ToArray();
        if (materialized.Length == 0)
            throw new ArgumentException("A power must contain at least one effect.", nameof(effects));
        if (materialized.Any(effect => effect is null))
            throw new ArgumentException("Power effects cannot contain null values.", nameof(effects));

        Id = id;
        Name = name;
        Cost = cost;
        MaxUsesPerTurn = maxUsesPerTurn;
        MaxUsesPerMatch = maxUsesPerMatch;
        _effects = Array.AsReadOnly(materialized);
    }
}

public sealed class PowerCatalog
{
    private readonly Dictionary<PowerId, PowerDefinition> _byId;
    private readonly ReadOnlyCollection<PowerDefinition> _all;

    public IReadOnlyList<PowerDefinition> All => _all;

    public PowerCatalog(IEnumerable<PowerDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        if (materialized.Any(definition => definition is null))
            throw new ArgumentException("Power catalog cannot contain null definitions.", nameof(definitions));

        var duplicate = materialized.GroupBy(definition => definition.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Duplicate power id '{duplicate.Key}'.", nameof(definitions));

        var ordered = materialized.OrderBy(definition => definition.Id.Value, StringComparer.Ordinal).ToArray();
        _byId = ordered.ToDictionary(definition => definition.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public PowerDefinition GetRequired(PowerId id) =>
        _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown power '{id}'.");

    public bool TryGet(PowerId id, out PowerDefinition definition) =>
        _byId.TryGetValue(id, out definition!);
}
