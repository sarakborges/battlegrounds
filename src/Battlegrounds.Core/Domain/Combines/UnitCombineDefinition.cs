using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Combines;

public sealed class UnitCombineDefinition
{
    public UnitCombineId Id { get; }
    public string Name { get; }
    public UnitId SourceUnitId { get; }
    public int RequiredCopies { get; }
    public UnitId ResultUnitId { get; }

    public UnitCombineDefinition(
        UnitCombineId id,
        string name,
        UnitId sourceUnitId,
        int requiredCopies,
        UnitId resultUnitId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Unit combine name cannot be empty.", nameof(name));
        if (requiredCopies < 2) throw new ArgumentOutOfRangeException(nameof(requiredCopies), "A unit combine requires at least two copies.");
        Id = id;
        Name = name;
        SourceUnitId = sourceUnitId;
        RequiredCopies = requiredCopies;
        ResultUnitId = resultUnitId;
    }
}

public sealed class UnitCombineCatalog
{
    private readonly ReadOnlyCollection<UnitCombineDefinition> _all;
    private readonly Dictionary<UnitCombineId, UnitCombineDefinition> _byId;

    public IReadOnlyList<UnitCombineDefinition> All => _all;

    public UnitCombineCatalog(IEnumerable<UnitCombineDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var materialized = definitions.ToArray();
        var duplicate = materialized.GroupBy(value => value.Id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Duplicate unit combine id '{duplicate.Key}'.", nameof(definitions));
        _all = Array.AsReadOnly(materialized);
        _byId = materialized.ToDictionary(value => value.Id);
    }

    public bool TryGet(UnitCombineId id, out UnitCombineDefinition definition) =>
        _byId.TryGetValue(id, out definition!);

    public UnitCombineDefinition GetRequired(UnitCombineId id) =>
        TryGet(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown unit combine '{id}'.");
}
