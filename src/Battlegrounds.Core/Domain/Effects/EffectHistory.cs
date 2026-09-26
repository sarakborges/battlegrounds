using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Effects;

public readonly record struct NativeGameEventKey
{
    public string Value { get; }

    public NativeGameEventKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Game event key cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}

public static class NativeGameEventKeys
{
    public static NativeGameEventKey UnitAcquired { get; } = new("unitAcquired");
    public static NativeGameEventKey UnitReleased { get; } = new("unitReleased");
    public static NativeGameEventKey UnitPlayed { get; } = new("unitPlayed");
    public static NativeGameEventKey UnitSummoned { get; } = new("unitSummoned");
    public static NativeGameEventKey UnitDied { get; } = new("unitDied");
    public static NativeGameEventKey UnitAttacked { get; } = new("unitAttacked");
    public static NativeGameEventKey UnitDamaged { get; } = new("unitDamaged");
    public static NativeGameEventKey PowerActivated { get; } = new("powerActivated");
    public static NativeGameEventKey OfferRefreshed { get; } = new("offerRefreshed");
    public static NativeGameEventKey TierUpgraded { get; } = new("tierUpgraded");

    private static readonly HashSet<NativeGameEventKey> Supported =
    [
        UnitAcquired,
        UnitReleased,
        UnitPlayed,
        UnitSummoned,
        UnitDied,
        UnitAttacked,
        UnitDamaged,
        PowerActivated,
        OfferRefreshed,
        TierUpgraded,
    ];

    public static bool IsSupported(NativeGameEventKey key) => Supported.Contains(key);
}

public enum EffectHistoryScope
{
    Turn,
    Combat,
    Match,
}

public sealed record EffectHistoryQuery
{
    public NativeGameEventKey Event { get; }
    public EffectHistoryScope Scope { get; }
    public UnitTypeId? RequiredTypeId { get; }
    public TagId? RequiredTagId { get; }

    public EffectHistoryQuery(
        NativeGameEventKey @event,
        EffectHistoryScope scope,
        UnitTypeId? requiredTypeId = null,
        TagId? requiredTagId = null)
    {
        if (!NativeGameEventKeys.IsSupported(@event))
            throw new ArgumentException($"Unsupported game event '{@event}'.", nameof(@event));

        Event = @event;
        Scope = scope;
        RequiredTypeId = requiredTypeId;
        RequiredTagId = requiredTagId;
    }
}

public sealed record TriggerActivationLimit
{
    public EffectHistoryScope Scope { get; }
    public int Maximum { get; }

    public TriggerActivationLimit(EffectHistoryScope scope, int maximum)
    {
        if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
        Scope = scope;
        Maximum = maximum;
    }
}

internal readonly record struct EffectEventCounterKey(
    NativeGameEventKey Event,
    UnitTypeId? TypeId,
    TagId? TagId);

internal readonly record struct EffectSourceKey(string Value)
{
    public static EffectSourceKey ForUnit(UnitInstanceId instanceId) => new($"unit:{instanceId.Value}");
    public static EffectSourceKey ForPower(PowerId powerId) => new($"power:{powerId.Value}");
}

internal readonly record struct EffectTriggerCounterKey(
    EffectSourceKey Source,
    int TriggerIndex);

internal sealed class EffectHistorySnapshot
{
    private readonly IReadOnlyDictionary<EffectEventCounterKey, int> _turnEvents;
    private readonly IReadOnlyDictionary<EffectEventCounterKey, int> _combatEvents;
    private readonly IReadOnlyDictionary<EffectEventCounterKey, int> _matchEvents;
    private readonly IReadOnlyDictionary<EffectTriggerCounterKey, int> _turnTriggers;
    private readonly IReadOnlyDictionary<EffectTriggerCounterKey, int> _combatTriggers;
    private readonly IReadOnlyDictionary<EffectTriggerCounterKey, int> _matchTriggers;

    public static EffectHistorySnapshot Empty { get; } = new();

    private EffectHistorySnapshot()
        : this(
            new Dictionary<EffectEventCounterKey, int>(),
            new Dictionary<EffectEventCounterKey, int>(),
            new Dictionary<EffectEventCounterKey, int>(),
            new Dictionary<EffectTriggerCounterKey, int>(),
            new Dictionary<EffectTriggerCounterKey, int>(),
            new Dictionary<EffectTriggerCounterKey, int>())
    {
    }

    internal EffectHistorySnapshot(
        IReadOnlyDictionary<EffectEventCounterKey, int> turnEvents,
        IReadOnlyDictionary<EffectEventCounterKey, int> combatEvents,
        IReadOnlyDictionary<EffectEventCounterKey, int> matchEvents,
        IReadOnlyDictionary<EffectTriggerCounterKey, int> turnTriggers,
        IReadOnlyDictionary<EffectTriggerCounterKey, int> combatTriggers,
        IReadOnlyDictionary<EffectTriggerCounterKey, int> matchTriggers)
    {
        _turnEvents = new Dictionary<EffectEventCounterKey, int>(turnEvents);
        _combatEvents = new Dictionary<EffectEventCounterKey, int>(combatEvents);
        _matchEvents = new Dictionary<EffectEventCounterKey, int>(matchEvents);
        _turnTriggers = new Dictionary<EffectTriggerCounterKey, int>(turnTriggers);
        _combatTriggers = new Dictionary<EffectTriggerCounterKey, int>(combatTriggers);
        _matchTriggers = new Dictionary<EffectTriggerCounterKey, int>(matchTriggers);
    }

    internal int GetEventCount(EffectHistoryQuery query)
    {
        var key = new EffectEventCounterKey(query.Event, query.RequiredTypeId, query.RequiredTagId);
        return query.Scope switch
        {
            EffectHistoryScope.Turn => _turnEvents.GetValueOrDefault(key),
            EffectHistoryScope.Combat => _combatEvents.GetValueOrDefault(key),
            EffectHistoryScope.Match => _matchEvents.GetValueOrDefault(key),
            _ => throw new ArgumentOutOfRangeException(nameof(query.Scope), query.Scope, "Unsupported history scope."),
        };
    }

    internal int GetTriggerActivationCount(
        EffectSourceKey source,
        int triggerIndex,
        EffectHistoryScope scope)
    {
        var key = new EffectTriggerCounterKey(source, triggerIndex);
        return scope switch
        {
            EffectHistoryScope.Turn => _turnTriggers.GetValueOrDefault(key),
            EffectHistoryScope.Combat => _combatTriggers.GetValueOrDefault(key),
            EffectHistoryScope.Match => _matchTriggers.GetValueOrDefault(key),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported history scope."),
        };
    }

    internal IReadOnlyDictionary<EffectEventCounterKey, int> TurnEvents => _turnEvents;
    internal IReadOnlyDictionary<EffectEventCounterKey, int> MatchEvents => _matchEvents;
    internal IReadOnlyDictionary<EffectTriggerCounterKey, int> TurnTriggers => _turnTriggers;
    internal IReadOnlyDictionary<EffectTriggerCounterKey, int> MatchTriggers => _matchTriggers;
}

internal sealed class EffectHistoryDelta
{
    internal IReadOnlyDictionary<EffectEventCounterKey, int> EventCounts { get; }
    internal IReadOnlyDictionary<EffectTriggerCounterKey, int> TurnTriggerCounts { get; }
    internal IReadOnlyDictionary<EffectTriggerCounterKey, int> MatchTriggerCounts { get; }

    internal EffectHistoryDelta(
        IReadOnlyDictionary<EffectEventCounterKey, int> eventCounts,
        IReadOnlyDictionary<EffectTriggerCounterKey, int> turnTriggerCounts,
        IReadOnlyDictionary<EffectTriggerCounterKey, int> matchTriggerCounts)
    {
        EventCounts = new Dictionary<EffectEventCounterKey, int>(eventCounts);
        TurnTriggerCounts = new Dictionary<EffectTriggerCounterKey, int>(turnTriggerCounts);
        MatchTriggerCounts = new Dictionary<EffectTriggerCounterKey, int>(matchTriggerCounts);
    }
}

internal sealed class EffectHistoryState
{
    private readonly Dictionary<EffectEventCounterKey, int> _turnEvents = [];
    private readonly Dictionary<EffectEventCounterKey, int> _matchEvents = [];
    private readonly Dictionary<EffectTriggerCounterKey, int> _turnTriggers = [];
    private readonly Dictionary<EffectTriggerCounterKey, int> _matchTriggers = [];

    internal void BeginTurn()
    {
        _turnEvents.Clear();
        _turnTriggers.Clear();
    }

    internal void RecordEvent(NativeGameEventKey @event, UnitDefinition? unit = null)
    {
        foreach (var key in ExpandEventKeys(@event, unit))
        {
            Increment(_turnEvents, key, 1);
            Increment(_matchEvents, key, 1);
        }
    }

    internal int GetTriggerActivationCount(
        EffectSourceKey source,
        int triggerIndex,
        EffectHistoryScope scope)
    {
        var key = new EffectTriggerCounterKey(source, triggerIndex);
        return scope switch
        {
            EffectHistoryScope.Turn => _turnTriggers.GetValueOrDefault(key),
            EffectHistoryScope.Match => _matchTriggers.GetValueOrDefault(key),
            EffectHistoryScope.Combat => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported history scope."),
        };
    }

    internal void RecordTriggerActivation(
        EffectSourceKey source,
        int triggerIndex,
        EffectHistoryScope scope)
    {
        var key = new EffectTriggerCounterKey(source, triggerIndex);
        switch (scope)
        {
            case EffectHistoryScope.Turn:
                Increment(_turnTriggers, key, 1);
                break;
            case EffectHistoryScope.Match:
                Increment(_matchTriggers, key, 1);
                break;
            case EffectHistoryScope.Combat:
                throw new InvalidOperationException("Combat-scoped trigger activations are local to a combat runtime.");
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported history scope.");
        }
    }

    internal EffectHistorySnapshot Snapshot() =>
        new(
            _turnEvents,
            new Dictionary<EffectEventCounterKey, int>(),
            _matchEvents,
            _turnTriggers,
            new Dictionary<EffectTriggerCounterKey, int>(),
            _matchTriggers);

    internal void ApplyCombatDelta(EffectHistoryDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        foreach (var pair in delta.EventCounts)
        {
            Increment(_turnEvents, pair.Key, pair.Value);
            Increment(_matchEvents, pair.Key, pair.Value);
        }
        foreach (var pair in delta.TurnTriggerCounts)
            Increment(_turnTriggers, pair.Key, pair.Value);
        foreach (var pair in delta.MatchTriggerCounts)
            Increment(_matchTriggers, pair.Key, pair.Value);
    }

    internal static IReadOnlyList<EffectEventCounterKey> ExpandEventKeys(
        NativeGameEventKey @event,
        UnitDefinition? unit)
    {
        if (!NativeGameEventKeys.IsSupported(@event))
            throw new ArgumentException($"Unsupported game event '{@event}'.", nameof(@event));

        var keys = new HashSet<EffectEventCounterKey>
        {
            new(@event, null, null),
        };
        if (unit is null) return keys.ToArray();

        var typeIds = unit.Types.Select(type => (UnitTypeId?)type.Id).ToArray();
        var tagIds = unit.Tags.Select(tag => (TagId?)tag.Id).ToArray();
        foreach (var typeId in typeIds)
            keys.Add(new EffectEventCounterKey(@event, typeId, null));
        foreach (var tagId in tagIds)
            keys.Add(new EffectEventCounterKey(@event, null, tagId));
        foreach (var typeId in typeIds)
        foreach (var tagId in tagIds)
            keys.Add(new EffectEventCounterKey(@event, typeId, tagId));

        return keys.ToArray();
    }

    internal static void Increment<TKey>(Dictionary<TKey, int> dictionary, TKey key, int amount)
        where TKey : notnull
    {
        dictionary[key] = checked(dictionary.GetValueOrDefault(key) + amount);
    }
}

internal sealed class CombatEffectHistoryState
{
    private readonly EffectHistorySnapshot _baseline;
    private readonly Dictionary<EffectEventCounterKey, int> _eventDeltas = [];
    private readonly Dictionary<EffectEventCounterKey, int> _combatEvents = [];
    private readonly Dictionary<EffectTriggerCounterKey, int> _turnTriggerDeltas = [];
    private readonly Dictionary<EffectTriggerCounterKey, int> _matchTriggerDeltas = [];
    private readonly Dictionary<EffectTriggerCounterKey, int> _combatTriggers = [];

    internal CombatEffectHistoryState(EffectHistorySnapshot baseline)
    {
        _baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
    }

    internal void RecordEvent(NativeGameEventKey @event, UnitDefinition? unit = null)
    {
        foreach (var key in EffectHistoryState.ExpandEventKeys(@event, unit))
        {
            EffectHistoryState.Increment(_eventDeltas, key, 1);
            EffectHistoryState.Increment(_combatEvents, key, 1);
        }
    }

    internal int GetTriggerActivationCount(
        EffectSourceKey source,
        int triggerIndex,
        EffectHistoryScope scope)
    {
        var key = new EffectTriggerCounterKey(source, triggerIndex);
        return scope switch
        {
            EffectHistoryScope.Turn => checked(_baseline.TurnTriggers.GetValueOrDefault(key) + _turnTriggerDeltas.GetValueOrDefault(key)),
            EffectHistoryScope.Combat => _combatTriggers.GetValueOrDefault(key),
            EffectHistoryScope.Match => checked(_baseline.MatchTriggers.GetValueOrDefault(key) + _matchTriggerDeltas.GetValueOrDefault(key)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported history scope."),
        };
    }

    internal void RecordTriggerActivation(
        EffectSourceKey source,
        int triggerIndex,
        EffectHistoryScope scope)
    {
        var key = new EffectTriggerCounterKey(source, triggerIndex);
        switch (scope)
        {
            case EffectHistoryScope.Turn:
                EffectHistoryState.Increment(_turnTriggerDeltas, key, 1);
                break;
            case EffectHistoryScope.Combat:
                EffectHistoryState.Increment(_combatTriggers, key, 1);
                break;
            case EffectHistoryScope.Match:
                EffectHistoryState.Increment(_matchTriggerDeltas, key, 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported history scope.");
        }
    }

    internal EffectHistorySnapshot Snapshot()
    {
        var turnEvents = Merge(_baseline.TurnEvents, _eventDeltas);
        var matchEvents = Merge(_baseline.MatchEvents, _eventDeltas);
        var turnTriggers = Merge(_baseline.TurnTriggers, _turnTriggerDeltas);
        var matchTriggers = Merge(_baseline.MatchTriggers, _matchTriggerDeltas);
        return new EffectHistorySnapshot(
            turnEvents,
            _combatEvents,
            matchEvents,
            turnTriggers,
            _combatTriggers,
            matchTriggers);
    }

    internal EffectHistoryDelta CreateDelta() =>
        new(_eventDeltas, _turnTriggerDeltas, _matchTriggerDeltas);

    private static Dictionary<TKey, int> Merge<TKey>(
        IReadOnlyDictionary<TKey, int> baseline,
        IReadOnlyDictionary<TKey, int> delta)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, int>(baseline);
        foreach (var pair in delta)
            result[pair.Key] = checked(result.GetValueOrDefault(pair.Key) + pair.Value);
        return result;
    }
}
