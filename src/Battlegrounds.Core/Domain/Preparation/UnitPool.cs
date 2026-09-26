using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Preparation;

public sealed class UnitPool : IUnitPool
{
    private readonly UnitCatalog _catalog;
    private readonly Dictionary<UnitId, int> _totalCopies;
    private readonly Dictionary<UnitId, int> _availableCopies;

    public UnitPool(UnitCatalog catalog, IEnumerable<UnitPoolEntry> entries)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        ArgumentNullException.ThrowIfNull(entries);

        var materialized = entries.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("Unit pool cannot be empty.", nameof(entries));
        }

        var duplicate = materialized
            .GroupBy(entry => entry.UnitId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate unit pool entry '{duplicate.Key}'.", nameof(entries));
        }

        foreach (var entry in materialized)
        {
            _catalog.GetRequired(entry.UnitId);
        }

        _totalCopies = materialized.ToDictionary(entry => entry.UnitId, entry => entry.Copies);
        _availableCopies = new Dictionary<UnitId, int>(_totalCopies);
    }

    public int GetAvailableCopies(UnitId unitId) =>
        _availableCopies.TryGetValue(unitId, out var copies) ? copies : 0;

    public IReadOnlyList<UnitDefinition> DrawOffer(
        int maximumTier,
        int count,
        IRandomSource randomSource) =>
        ExchangeOffer(Array.Empty<UnitDefinition>(), maximumTier, count, randomSource);

    public IReadOnlyList<UnitDefinition> ExchangeOffer(
        IReadOnlyCollection<UnitDefinition> returnedUnits,
        int maximumTier,
        int count,
        IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(returnedUnits);
        ArgumentNullException.ThrowIfNull(randomSource);

        if (maximumTier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTier));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var workingCopies = new Dictionary<UnitId, int>(_availableCopies);

        foreach (var definition in returnedUnits)
        {
            ReturnToWorkingPool(definition, workingCopies);
        }

        var eligibleCopyCount = GetEligibleCopyCount(workingCopies, maximumTier);
        if (eligibleCopyCount < count)
        {
            throw new InvalidOperationException(
                $"Unit pool has {eligibleCopyCount} eligible copies but {count} are required.");
        }

        var result = new UnitDefinition[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = DrawOne(workingCopies, maximumTier, randomSource);
        }

        Commit(workingCopies);
        return result;
    }

    public void ReturnUnit(UnitDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var workingCopies = new Dictionary<UnitId, int>(_availableCopies);
        ReturnToWorkingPool(definition, workingCopies);
        Commit(workingCopies);
    }

    private void ReturnToWorkingPool(
        UnitDefinition definition,
        Dictionary<UnitId, int> workingCopies)
    {
        if (!_catalog.TryGet(definition.Id, out var canonical) || !ReferenceEquals(canonical, definition))
        {
            throw new InvalidOperationException($"Unit '{definition.Id}' does not match the authoritative catalog.");
        }

        if (!_totalCopies.TryGetValue(definition.Id, out var totalCopies))
        {
            throw new InvalidOperationException($"Unit '{definition.Id}' is not part of the unit pool.");
        }

        var available = workingCopies.GetValueOrDefault(definition.Id);
        if (available >= totalCopies)
        {
            throw new InvalidOperationException($"Returning '{definition.Id}' would exceed its configured pool size.");
        }

        workingCopies[definition.Id] = available + 1;
    }

    private int GetEligibleCopyCount(IReadOnlyDictionary<UnitId, int> copies, int maximumTier)
    {
        var total = 0;

        foreach (var definition in _catalog.All)
        {
            if (definition.Tier <= maximumTier && copies.TryGetValue(definition.Id, out var available))
            {
                total = checked(total + available);
            }
        }

        return total;
    }

    private UnitDefinition DrawOne(
        Dictionary<UnitId, int> workingCopies,
        int maximumTier,
        IRandomSource randomSource)
    {
        var totalEligibleCopies = GetEligibleCopyCount(workingCopies, maximumTier);
        if (totalEligibleCopies <= 0)
        {
            throw new InvalidOperationException("No eligible units remain in the unit pool.");
        }

        var roll = randomSource.NextInt(0, totalEligibleCopies);

        foreach (var definition in _catalog.All)
        {
            if (definition.Tier > maximumTier ||
                !workingCopies.TryGetValue(definition.Id, out var available) ||
                available <= 0)
            {
                continue;
            }

            if (roll < available)
            {
                workingCopies[definition.Id] = available - 1;
                return definition;
            }

            roll -= available;
        }

        throw new InvalidOperationException("Unit pool selection violated its weighting invariant.");
    }

    private void Commit(IReadOnlyDictionary<UnitId, int> workingCopies)
    {
        _availableCopies.Clear();
        foreach (var pair in workingCopies)
        {
            _availableCopies.Add(pair.Key, pair.Value);
        }
    }
}
