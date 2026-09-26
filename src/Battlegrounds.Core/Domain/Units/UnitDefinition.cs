using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Taxonomy;

namespace Battlegrounds.Core.Domain.Units;

public sealed record UnitDefinition
{
    private readonly ReadOnlyCollection<BehaviorDefinition> _behaviors;
    private readonly ReadOnlyCollection<UnitTypeDefinition> _types;
    private readonly ReadOnlyCollection<TagDefinition> _tags;
    private readonly ReadOnlyCollection<TriggerDefinition> _triggers;

    public UnitId Id { get; }
    public string Name { get; }
    public int Tier { get; }
    public int BaseAttack { get; }
    public int BaseHealth { get; }
    public IReadOnlyList<BehaviorDefinition> Behaviors => _behaviors;
    public IReadOnlyList<UnitTypeDefinition> Types => _types;
    public IReadOnlyList<TagDefinition> Tags => _tags;
    public IReadOnlyList<TriggerDefinition> Triggers => _triggers;

    public UnitDefinition(
        UnitId id,
        string name,
        int tier,
        int baseAttack,
        int baseHealth,
        IEnumerable<BehaviorDefinition>? behaviors = null,
        IEnumerable<UnitTypeDefinition>? types = null,
        IEnumerable<TagDefinition>? tags = null,
        IEnumerable<TriggerDefinition>? triggers = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Unit name cannot be empty.", nameof(name));
        if (tier <= 0) throw new ArgumentOutOfRangeException(nameof(tier));
        if (baseAttack < 0) throw new ArgumentOutOfRangeException(nameof(baseAttack));
        if (baseHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseHealth));

        var behaviorArray = MaterializeUnique(
            behaviors,
            behavior => behavior.Id,
            "Unit behaviors cannot contain null definitions.",
            "Duplicate unit behavior id");
        var duplicateHandler = behaviorArray.GroupBy(behavior => behavior.Handler).FirstOrDefault(group => group.Count() > 1);
        if (duplicateHandler is not null)
            throw new ArgumentException($"A unit cannot attach native behavior '{duplicateHandler.Key}' more than once.", nameof(behaviors));

        var typeArray = MaterializeUnique(
            types,
            type => type.Id,
            "Unit types cannot contain null definitions.",
            "Duplicate unit type id");
        var tagArray = MaterializeUnique(
            tags,
            tag => tag.Id,
            "Unit tags cannot contain null definitions.",
            "Duplicate unit tag id");
        var triggerArray = triggers?.ToArray() ?? [];
        if (triggerArray.Any(trigger => trigger is null))
            throw new ArgumentException("Unit triggers cannot contain null definitions.", nameof(triggers));

        Id = id;
        Name = name;
        Tier = tier;
        BaseAttack = baseAttack;
        BaseHealth = baseHealth;
        _behaviors = Array.AsReadOnly(behaviorArray);
        _types = Array.AsReadOnly(typeArray);
        _tags = Array.AsReadOnly(tagArray);
        _triggers = Array.AsReadOnly(triggerArray);
    }

    private static T[] MaterializeUnique<T, TKey>(
        IEnumerable<T>? values,
        Func<T, TKey> keySelector,
        string nullMessage,
        string duplicateMessage)
        where T : class
        where TKey : notnull
    {
        var materialized = values?.ToArray() ?? [];
        if (materialized.Any(value => value is null)) throw new ArgumentException(nullMessage);
        var duplicate = materialized.GroupBy(keySelector).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"{duplicateMessage} '{duplicate.Key}'.");
        return materialized;
    }
}
