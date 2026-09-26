using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Leaders;

public sealed class LeaderCatalog
{
    private readonly Dictionary<LeaderId, LeaderDefinition> _byId;
    private readonly ReadOnlyCollection<LeaderDefinition> _all;

    public IReadOnlyList<LeaderDefinition> All => _all;

    public LeaderCatalog(IEnumerable<LeaderDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var items = definitions.ToArray();
        if (items.Any(definition => definition is null))
        {
            throw new ArgumentException("Leader catalog cannot contain null definitions.", nameof(definitions));
        }

        var duplicate = items
            .GroupBy(definition => definition.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate leader id '{duplicate.Key}'.", nameof(definitions));
        }

        var ordered = items
            .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ToArray();

        _byId = ordered.ToDictionary(definition => definition.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public LeaderDefinition GetRequired(LeaderId leaderId)
    {
        if (!_byId.TryGetValue(leaderId, out var definition))
        {
            throw new KeyNotFoundException($"Leader '{leaderId}' is not present in the catalog.");
        }

        return definition;
    }

    public bool TryGet(LeaderId leaderId, out LeaderDefinition definition) =>
        _byId.TryGetValue(leaderId, out definition!);
}
