using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Taxonomy;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Content;

public sealed class ModPackage
{
    private readonly ReadOnlyDictionary<string, string> _terminology;
    private readonly ReadOnlyCollection<UnitPoolEntry> _poolEntries;

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, string> Terminology => _terminology;
    public LeaderSelectionRules LeaderSelectionRules { get; }
    public MatchRules MatchRules { get; }
    public PreparationRules PreparationRules { get; }
    public CombatRules CombatRules { get; }
    public BehaviorCatalog Behaviors { get; }
    public LeaderCatalog Leaders { get; }
    public PowerCatalog Powers { get; }
    public UnitTypeCatalog UnitTypes { get; }
    public TagCatalog Tags { get; }
    public UnitCatalog Units { get; }
    public IReadOnlyList<UnitPoolEntry> PoolEntries => _poolEntries;

    internal ModPackage(
        string id,
        string name,
        IReadOnlyDictionary<string, string> terminology,
        LeaderSelectionRules leaderSelectionRules,
        MatchRules matchRules,
        PreparationRules preparationRules,
        CombatRules combatRules,
        BehaviorCatalog behaviors,
        LeaderCatalog leaders,
        PowerCatalog powers,
        UnitTypeCatalog unitTypes,
        TagCatalog tags,
        UnitCatalog units,
        IEnumerable<UnitPoolEntry> poolEntries)
    {
        Id = id;
        Name = name;
        LeaderSelectionRules = leaderSelectionRules ?? throw new ArgumentNullException(nameof(leaderSelectionRules));
        MatchRules = matchRules;
        PreparationRules = preparationRules;
        CombatRules = combatRules;
        Behaviors = behaviors ?? throw new ArgumentNullException(nameof(behaviors));
        Leaders = leaders ?? throw new ArgumentNullException(nameof(leaders));
        Powers = powers ?? throw new ArgumentNullException(nameof(powers));
        UnitTypes = unitTypes ?? throw new ArgumentNullException(nameof(unitTypes));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        Units = units ?? throw new ArgumentNullException(nameof(units));
        _terminology = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(terminology, StringComparer.Ordinal));
        _poolEntries = Array.AsReadOnly(poolEntries.ToArray());
    }

    public UnitPool CreateUnitPool() => new(Units, _poolEntries);

    public LeaderSelectionState CreateLeaderSelection(
        IEnumerable<PlayerId> playerIds,
        IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        ArgumentNullException.ThrowIfNull(randomSource);
        return LeaderSelectionState.Create(
            playerIds,
            MatchRules,
            LeaderSelectionRules,
            Leaders,
            randomSource);
    }

    public PreparationEngine CreatePreparationEngine(IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        return new PreparationEngine(
            PreparationRules,
            CreateUnitPool(),
            randomSource,
            Units,
            Behaviors,
            Powers);
    }

    public CombatEngine CreateCombatEngine() =>
        new(PreparationRules.FieldCapacity, Units, Behaviors);

    public MatchEngine CreateMatchEngine(IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);
        return new MatchEngine(
            MatchRules,
            PreparationRules,
            CombatRules,
            CreateUnitPool(),
            randomSource,
            Units,
            Behaviors,
            Leaders,
            Powers);
    }
}
