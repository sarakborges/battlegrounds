using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Cards;

public sealed class CardCatalog
{
    private readonly Dictionary<CardId, CardDefinition> _byId;
    private readonly ReadOnlyCollection<CardDefinition> _all;

    public IReadOnlyList<CardDefinition> All => _all;

    public CardCatalog(IEnumerable<CardDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var items = definitions.ToArray();
        if (items.Length == 0)
        {
            throw new ArgumentException("Card catalog cannot be empty.", nameof(definitions));
        }

        if (items.Any(definition => definition is null))
        {
            throw new ArgumentException("Card catalog cannot contain null definitions.", nameof(definitions));
        }

        var duplicate = items
            .GroupBy(definition => definition.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate card id '{duplicate.Key}'.", nameof(definitions));
        }

        var ordered = items
            .OrderBy(definition => definition.Id.Value, StringComparer.Ordinal)
            .ToArray();

        _byId = ordered.ToDictionary(definition => definition.Id);
        _all = Array.AsReadOnly(ordered);
    }

    public CardDefinition GetRequired(CardId cardId)
    {
        if (!_byId.TryGetValue(cardId, out var definition))
        {
            throw new KeyNotFoundException($"Card '{cardId}' is not present in the catalog.");
        }

        return definition;
    }

    public bool TryGet(CardId cardId, out CardDefinition definition) =>
        _byId.TryGetValue(cardId, out definition!);
}
