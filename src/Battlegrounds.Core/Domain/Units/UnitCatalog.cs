using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Units;

public sealed class UnitCatalog
{
    private readonly Dictionary<UnitId, UnitDefinition> _byId;
    private readonly ReadOnlyCollection<UnitDefinition> _all;

    public IReadOnlyList<UnitDefinition> All => _all;

    public UnitCatalog(IEnumerable<UnitDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var items = definitions.ToArray();
        if (items.Length == 0)
        {
            throw new ArgumentException("Unit catalog cannot be empty.", nameof(definitions));
        }

        if (items.Any(definition => definition is null))
        {
            throw new ArgumentException("Unit catalog cannot contain null definitions.", nameof(definitions));
        }

        var duplicate = items
            .GroupBy(definition => definition.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate unit id '{duplicate.Key}'.", nameof(definitions));
        }

        var ordered = items
            .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ToArray();

        _byId = ordered.ToDictionary(definition => definition.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public UnitDefinition GetRequired(UnitId unitId)
    {
        if (!_byId.TryGetValue(unitId, out var definition))
        {
            throw new KeyNotFoundException($"Unit '{unitId}' is not present in the catalog.");
        }

        return definition;
    }

    public bool TryGet(UnitId unitId, out UnitDefinition definition) =>
        _byId.TryGetValue(unitId, out definition!);
}
