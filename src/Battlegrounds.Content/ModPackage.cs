using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Content;

public sealed class ModPackage
{
    private readonly ReadOnlyDictionary<string, string> _terminology;
    private readonly ReadOnlyCollection<UnitPoolEntry> _poolEntries;

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, string> Terminology => _terminology;
    public MatchRules MatchRules { get; }
    public PreparationRules PreparationRules { get; }
    public UnitCatalog Units { get; }
    public IReadOnlyList<UnitPoolEntry> PoolEntries => _poolEntries;

    internal ModPackage(
        string id,
        string name,
        IReadOnlyDictionary<string, string> terminology,
        MatchRules matchRules,
        PreparationRules preparationRules,
        UnitCatalog units,
        IEnumerable<UnitPoolEntry> poolEntries)
    {
        Id = id;
        Name = name;
        MatchRules = matchRules;
        PreparationRules = preparationRules;
        Units = units;
        _terminology = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(terminology, StringComparer.Ordinal));
        _poolEntries = Array.AsReadOnly(poolEntries.ToArray());
    }

    public UnitPool CreateUnitPool() => new(Units, _poolEntries);
}
