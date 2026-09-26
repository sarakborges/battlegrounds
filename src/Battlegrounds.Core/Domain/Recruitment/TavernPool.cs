using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Recruitment;

public sealed class TavernPool : ITavernPool
{
    private readonly CardCatalog _catalog;
    private readonly Dictionary<CardId, int> _totalCopies;
    private readonly Dictionary<CardId, int> _availableCopies;

    public TavernPool(CardCatalog catalog, IEnumerable<TavernPoolEntry> entries)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        ArgumentNullException.ThrowIfNull(entries);

        var materialized = entries.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("Tavern pool cannot be empty.", nameof(entries));
        }

        var duplicate = materialized
            .GroupBy(entry => entry.CardId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate tavern pool entry '{duplicate.Key}'.", nameof(entries));
        }

        foreach (var entry in materialized)
        {
            _catalog.GetRequired(entry.CardId);
        }

        _totalCopies = materialized.ToDictionary(entry => entry.CardId, entry => entry.Copies);
        _availableCopies = new Dictionary<CardId, int>(_totalCopies);
    }

    public int GetAvailableCopies(CardId cardId) =>
        _availableCopies.TryGetValue(cardId, out var copies) ? copies : 0;

    public IReadOnlyList<CardDefinition> DrawOffer(
        int tavernTier,
        int count,
        IRandomSource randomSource) =>
        ExchangeOffer(Array.Empty<CardDefinition>(), tavernTier, count, randomSource);

    public IReadOnlyList<CardDefinition> ExchangeOffer(
        IReadOnlyCollection<CardDefinition> returnedCards,
        int tavernTier,
        int count,
        IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(returnedCards);
        ArgumentNullException.ThrowIfNull(randomSource);

        if (tavernTier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tavernTier));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var workingCopies = new Dictionary<CardId, int>(_availableCopies);

        foreach (var definition in returnedCards)
        {
            ReturnToWorkingPool(definition, workingCopies);
        }

        var eligibleCopyCount = GetEligibleCopyCount(workingCopies, tavernTier);
        if (eligibleCopyCount < count)
        {
            throw new InvalidOperationException(
                $"Tavern pool has {eligibleCopyCount} eligible copies but {count} are required.");
        }

        var result = new CardDefinition[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = DrawOne(workingCopies, tavernTier, randomSource);
        }

        Commit(workingCopies);
        return result;
    }

    public void ReturnMinion(CardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var workingCopies = new Dictionary<CardId, int>(_availableCopies);
        ReturnToWorkingPool(definition, workingCopies);
        Commit(workingCopies);
    }

    private void ReturnToWorkingPool(
        CardDefinition definition,
        Dictionary<CardId, int> workingCopies)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_catalog.TryGet(definition.Id, out var canonical) || canonical != definition)
        {
            throw new InvalidOperationException($"Card '{definition.Id}' does not match the authoritative catalog.");
        }

        if (!_totalCopies.TryGetValue(definition.Id, out var totalCopies))
        {
            throw new InvalidOperationException($"Card '{definition.Id}' is not part of the tavern pool.");
        }

        var available = workingCopies.GetValueOrDefault(definition.Id);
        if (available >= totalCopies)
        {
            throw new InvalidOperationException($"Returning '{definition.Id}' would exceed its configured pool size.");
        }

        workingCopies[definition.Id] = available + 1;
    }

    private int GetEligibleCopyCount(IReadOnlyDictionary<CardId, int> copies, int tavernTier)
    {
        var total = 0;

        foreach (var definition in _catalog.All)
        {
            if (definition.TavernTier <= tavernTier && copies.TryGetValue(definition.Id, out var available))
            {
                total = checked(total + available);
            }
        }

        return total;
    }

    private CardDefinition DrawOne(
        Dictionary<CardId, int> workingCopies,
        int tavernTier,
        IRandomSource randomSource)
    {
        var totalEligibleCopies = GetEligibleCopyCount(workingCopies, tavernTier);
        if (totalEligibleCopies <= 0)
        {
            throw new InvalidOperationException("No eligible minions remain in the tavern pool.");
        }

        var roll = randomSource.NextInt(0, totalEligibleCopies);

        foreach (var definition in _catalog.All)
        {
            if (definition.TavernTier > tavernTier ||
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

        throw new InvalidOperationException("Tavern pool selection violated its weighting invariant.");
    }

    private void Commit(IReadOnlyDictionary<CardId, int> workingCopies)
    {
        _availableCopies.Clear();
        foreach (var pair in workingCopies)
        {
            _availableCopies.Add(pair.Key, pair.Value);
        }
    }
}
