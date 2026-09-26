using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Powers;

public sealed record PowerActivationDefinition
{
    public int Cost { get; }
    public int MaxUsesPerTurn { get; }
    public int? MaxUsesPerMatch { get; }

    public PowerActivationDefinition(int cost, int maxUsesPerTurn, int? maxUsesPerMatch = null)
    {
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
        if (maxUsesPerTurn <= 0) throw new ArgumentOutOfRangeException(nameof(maxUsesPerTurn));
        if (maxUsesPerMatch is not null && maxUsesPerMatch.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxUsesPerMatch));

        Cost = cost;
        MaxUsesPerTurn = maxUsesPerTurn;
        MaxUsesPerMatch = maxUsesPerMatch;
    }
}

public sealed class PowerDefinition
{
    private readonly ReadOnlyCollection<TriggerDefinition> _triggers;

    public PowerId Id { get; }
    public string Name { get; }
    public PowerActivationDefinition? Activation { get; }
    public IReadOnlyList<TriggerDefinition> Triggers => _triggers;
    public bool IsActivatable => Activation is not null;

    // Compatibility read-model properties for callers that only care about active powers.
    public int Cost => Activation?.Cost ?? 0;
    public int MaxUsesPerTurn => Activation?.MaxUsesPerTurn ?? 0;
    public int? MaxUsesPerMatch => Activation?.MaxUsesPerMatch;
    public IReadOnlyList<EffectDefinition> Effects =>
        _triggers.FirstOrDefault(trigger => trigger.Event == NativeTriggerKeys.OnActivate)?.Effects ?? [];

    public PowerDefinition(
        PowerId id,
        string name,
        PowerActivationDefinition? activation,
        IEnumerable<TriggerDefinition> triggers)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Power name cannot be empty.", nameof(name));

        ArgumentNullException.ThrowIfNull(triggers);
        var materialized = triggers.ToArray();
        if (materialized.Length == 0)
            throw new ArgumentException("A power must contain at least one trigger.", nameof(triggers));
        if (materialized.Any(trigger => trigger is null))
            throw new ArgumentException("Power triggers cannot contain null values.", nameof(triggers));

        var activationTriggers = materialized.Count(trigger => trigger.Event == NativeTriggerKeys.OnActivate);
        if (activation is null && activationTriggers != 0)
            throw new ArgumentException("A passive-only power cannot define onActivate.", nameof(triggers));
        if (activation is not null && activationTriggers != 1)
            throw new ArgumentException("An activatable power requires exactly one onActivate trigger.", nameof(triggers));

        Id = id;
        Name = name;
        Activation = activation;
        _triggers = Array.AsReadOnly(materialized);
    }

    public PowerDefinition(
        PowerId id,
        string name,
        int cost,
        int maxUsesPerTurn,
        IEnumerable<EffectDefinition> effects,
        int? maxUsesPerMatch = null)
        : this(
            id,
            name,
            new PowerActivationDefinition(cost, maxUsesPerTurn, maxUsesPerMatch),
            [new TriggerDefinition(NativeTriggerKeys.OnActivate, effects)])
    {
    }

    public TriggerDefinition? FindTrigger(NativeTriggerKey eventKey) =>
        _triggers.FirstOrDefault(trigger => trigger.Event == eventKey);
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
