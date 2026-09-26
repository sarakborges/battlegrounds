using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Taxonomy;
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
    public CombatRules CombatRules { get; }
    public BehaviorCatalog Behaviors { get; }
    public UnitTypeCatalog UnitTypes { get; }
    public TagCatalog Tags { get; }
    public UnitCatalog Units { get; }
    public IReadOnlyList<UnitPoolEntry> PoolEntries => _poolEntries;

    internal ModPackage(
        string id,
        string name,
        IReadOnlyDictionary<string, string> terminology,
        MatchRules matchRules,
        PreparationRules preparationRules,
        CombatRules combatRules,
        BehaviorCatalog behaviors,
        UnitTypeCatalog unitTypes,
        TagCatalog tags,
        UnitCatalog units,
        IEnumerable<UnitPoolEntry> poolEntries)
    {
        Id = id;
        Name = name;
        MatchRules = matchRules;
        PreparationRules = preparationRules;
        CombatRules = combatRules;
        Behaviors = behaviors ?? throw new ArgumentNullException(nameof(behaviors));
        UnitTypes = unitTypes ?? throw new ArgumentNullException(nameof(unitTypes));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        Units = units ?? throw new ArgumentNullException(nameof(units));
        _terminology = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(terminology, StringComparer.Ordinal));
        _poolEntries = Array.AsReadOnly(poolEntries.ToArray());
    }

    public UnitPool CreateUnitPool() => new(Units, _poolEntries);
}
