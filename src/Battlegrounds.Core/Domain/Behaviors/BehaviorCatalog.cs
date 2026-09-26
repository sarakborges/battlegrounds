using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Behaviors;

public sealed class BehaviorCatalog
{
    private readonly Dictionary<BehaviorId, BehaviorDefinition> _byId;
    private readonly ReadOnlyCollection<BehaviorDefinition> _all;

    public IReadOnlyList<BehaviorDefinition> All => _all;

    public BehaviorCatalog(IEnumerable<BehaviorDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var items = definitions.ToArray();
        if (items.Any(definition => definition is null))
        {
            throw new ArgumentException("Behavior catalog cannot contain null definitions.", nameof(definitions));
        }

        var duplicate = items
            .GroupBy(definition => definition.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate behavior id '{duplicate.Key}'.", nameof(definitions));
        }

        var ordered = items
            .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ToArray();

        _byId = ordered.ToDictionary(definition => definition.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public BehaviorDefinition GetRequired(BehaviorId behaviorId)
    {
        if (!_byId.TryGetValue(behaviorId, out var definition))
        {
            throw new KeyNotFoundException($"Behavior '{behaviorId}' is not present in the catalog.");
        }

        return definition;
    }

    public bool TryGet(BehaviorId behaviorId, out BehaviorDefinition definition) =>
        _byId.TryGetValue(behaviorId, out definition!);
}
