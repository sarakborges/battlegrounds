using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Taxonomy;

public sealed record UnitTypeDefinition
{
    public UnitTypeId Id { get; }
    public string Name { get; }

    public UnitTypeDefinition(UnitTypeId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Unit type name cannot be empty.", nameof(name));
        }

        Id = id;
        Name = name;
    }
}

public sealed record TagDefinition
{
    public TagId Id { get; }
    public string Name { get; }

    public TagDefinition(TagId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name cannot be empty.", nameof(name));
        }

        Id = id;
        Name = name;
    }
}

public sealed class UnitTypeCatalog
{
    private readonly Dictionary<UnitTypeId, UnitTypeDefinition> _byId;
    private readonly ReadOnlyCollection<UnitTypeDefinition> _all;

    public IReadOnlyList<UnitTypeDefinition> All => _all;

    public UnitTypeCatalog(IEnumerable<UnitTypeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var items = definitions.ToArray();
        if (items.Any(item => item is null)) throw new ArgumentException("Unit types cannot contain null definitions.", nameof(definitions));
        var duplicate = items.GroupBy(item => item.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Duplicate unit type id '{duplicate.Key}'.", nameof(definitions));
        var ordered = items.OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray();
        _byId = ordered.ToDictionary(item => item.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public UnitTypeDefinition GetRequired(UnitTypeId id) =>
        _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unit type '{id}' is not present in the catalog.");
}

public sealed class TagCatalog
{
    private readonly Dictionary<TagId, TagDefinition> _byId;
    private readonly ReadOnlyCollection<TagDefinition> _all;

    public IReadOnlyList<TagDefinition> All => _all;

    public TagCatalog(IEnumerable<TagDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var items = definitions.ToArray();
        if (items.Any(item => item is null)) throw new ArgumentException("Tags cannot contain null definitions.", nameof(definitions));
        var duplicate = items.GroupBy(item => item.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Duplicate tag id '{duplicate.Key}'.", nameof(definitions));
        var ordered = items.OrderBy(item => item.Id.Value, StringComparer.Ordinal).ToArray();
        _byId = ordered.ToDictionary(item => item.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public TagDefinition GetRequired(TagId id) =>
        _byId.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Tag '{id}' is not present in the catalog.");
}
