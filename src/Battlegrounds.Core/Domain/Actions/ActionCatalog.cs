using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Actions;

public sealed class ActionCatalog
{
    private readonly Dictionary<ActionId, ActionDefinition> _byId;
    private readonly ReadOnlyCollection<ActionDefinition> _all;

    public IReadOnlyList<ActionDefinition> All => _all;

    public ActionCatalog(IEnumerable<ActionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var items = definitions.ToArray();
        if (items.Any(item => item is null)) throw new ArgumentException("Action catalog cannot contain null definitions.", nameof(definitions));
        var duplicate = items.GroupBy(item => item.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Duplicate action id '{duplicate.Key}'.", nameof(definitions));
        var ordered = items.OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray();
        _byId = ordered.ToDictionary(item => item.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public ActionDefinition GetRequired(ActionId id) =>
        _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Action '{id}' is not present in the catalog.");

    public bool TryGet(ActionId id, out ActionDefinition definition) => _byId.TryGetValue(id, out definition!);
}
