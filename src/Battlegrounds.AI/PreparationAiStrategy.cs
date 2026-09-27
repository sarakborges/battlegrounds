using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.AI;

/// <summary>
/// A theme-neutral build preference applied on top of an AI personality.
/// Personality controls economic/action ordering; strategy controls which Units are valued.
/// </summary>
public sealed class PreparationAiStrategy
{
    private readonly HashSet<UnitTypeId> _preferredTypeIds;
    private readonly HashSet<TagId> _preferredTagIds;

    public const long DefaultTypeMatchBonus = 20_000L;
    public const long DefaultTagMatchBonus = 5_000L;

    public static PreparationAiStrategy Balanced { get; } = new("balanced");

    public string Id { get; }
    public IReadOnlySet<UnitTypeId> PreferredTypeIds => _preferredTypeIds;
    public IReadOnlySet<TagId> PreferredTagIds => _preferredTagIds;
    public long TypeMatchBonus { get; }
    public long TagMatchBonus { get; }

    public PreparationAiStrategy(
        string id,
        IEnumerable<UnitTypeId>? preferredTypeIds = null,
        IEnumerable<TagId>? preferredTagIds = null,
        long typeMatchBonus = DefaultTypeMatchBonus,
        long tagMatchBonus = DefaultTagMatchBonus)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("AI strategy id cannot be empty.", nameof(id));
        if (typeMatchBonus < 0) throw new ArgumentOutOfRangeException(nameof(typeMatchBonus));
        if (tagMatchBonus < 0) throw new ArgumentOutOfRangeException(nameof(tagMatchBonus));

        Id = id;
        _preferredTypeIds = new HashSet<UnitTypeId>(preferredTypeIds ?? []);
        _preferredTagIds = new HashSet<TagId>(preferredTagIds ?? []);
        TypeMatchBonus = typeMatchBonus;
        TagMatchBonus = tagMatchBonus;
    }

    public long GetUnitPreferenceBonus(UnitDefinition unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        var bonus = 0L;
        if (_preferredTypeIds.Count > 0 && unit.Types.Any(type => _preferredTypeIds.Contains(type.Id)))
            bonus += TypeMatchBonus;
        if (_preferredTagIds.Count > 0 && unit.Tags.Any(tag => _preferredTagIds.Contains(tag.Id)))
            bonus += TagMatchBonus;
        return bonus;
    }

    public static PreparationAiStrategy PreferType(UnitTypeId typeId) =>
        new($"type:{typeId.Value}", preferredTypeIds: [typeId]);

    public static PreparationAiStrategy PreferTag(TagId tagId) =>
        new($"tag:{tagId.Value}", preferredTagIds: [tagId]);

    public override string ToString() => Id;
}
