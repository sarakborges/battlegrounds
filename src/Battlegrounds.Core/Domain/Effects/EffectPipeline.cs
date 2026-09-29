using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Effects;

public sealed class EffectUnitSnapshot
{
    private readonly HashSet<UnitTypeId> _types;
    private readonly HashSet<TagId> _tags;
    public UnitInstanceId InstanceId { get; }
    public PlayerId OwnerPlayerId { get; }
    public bool IsAlive { get; }
    public bool IsSelectable { get; }
    public int Position { get; }
    public int Attack { get; }
    public int Health { get; }
    public EffectTargetZone Zone { get; }

    public EffectUnitSnapshot(
        UnitInstanceId instanceId,
        PlayerId ownerPlayerId,
        bool isAlive,
        int attack = 0,
        int health = 1,
        int position = -1,
        bool isSelectable = true,
        IEnumerable<UnitTypeId>? types = null,
        IEnumerable<TagId>? tags = null,
        EffectTargetZone zone = EffectTargetZone.Field)
    {
        InstanceId = instanceId;
        OwnerPlayerId = ownerPlayerId;
        IsAlive = isAlive;
        IsSelectable = isSelectable;
        Position = position;
        Attack = attack;
        Health = health;
        Zone = zone;
        _types = new HashSet<UnitTypeId>(types ?? []);
        _tags = new HashSet<TagId>(tags ?? []);
    }

    public bool HasType(UnitTypeId id) => _types.Contains(id);
    public bool HasTag(TagId id) => _tags.Contains(id);
}

public sealed class EffectResolutionContext
{
    private readonly ReadOnlyCollection<EffectUnitSnapshot> _units;
    private readonly EffectHistorySnapshot _history;
    public UnitInstanceId SourceInstanceId { get; }
    public PlayerId SourcePlayerId { get; }
    public UnitInstanceId? SelectedTargetInstanceId { get; }
    public CombatOutcome? CombatOutcome { get; }
    public IReadOnlyList<EffectUnitSnapshot> Units => _units;

    public EffectResolutionContext(UnitInstanceId sourceInstanceId, PlayerId sourcePlayerId, IEnumerable<EffectUnitSnapshot> units, UnitInstanceId? selectedTargetInstanceId = null)
        : this(sourceInstanceId, sourcePlayerId, units, selectedTargetInstanceId, EffectHistorySnapshot.Empty, null) { }

    internal EffectResolutionContext(
        UnitInstanceId sourceInstanceId,
        PlayerId sourcePlayerId,
        IEnumerable<EffectUnitSnapshot> units,
        UnitInstanceId? selectedTargetInstanceId,
        EffectHistorySnapshot history,
        CombatOutcome? combatOutcome = null)
    {
        ArgumentNullException.ThrowIfNull(units);
        var materialized = units.ToArray();
        var duplicate = materialized.GroupBy(unit => unit.InstanceId).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Duplicate effect unit instance id '{duplicate.Key}'.", nameof(units));
        if (!materialized.Any(unit => unit.InstanceId == sourceInstanceId && unit.OwnerPlayerId == sourcePlayerId))
            throw new ArgumentException("Effect source must be present in the resolution context.", nameof(units));
        if (selectedTargetInstanceId is not null && !materialized.Any(unit => unit.InstanceId == selectedTargetInstanceId.Value && unit.IsAlive && unit.IsSelectable))
            throw new ArgumentException("Selected effect target must be a living selectable unit in the resolution context.", nameof(selectedTargetInstanceId));
        SourceInstanceId = sourceInstanceId;
        SourcePlayerId = sourcePlayerId;
        SelectedTargetInstanceId = selectedTargetInstanceId;
        CombatOutcome = combatOutcome;
        _units = Array.AsReadOnly(materialized);
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    internal int GetEventCount(EffectHistoryQuery query) => _history.GetEventCount(query);
}

public sealed record ResolvedEffect(
    int Sequence,
    UnitInstanceId SourceInstanceId,
    NativeTriggerKey Trigger,
    EffectDefinition Definition,
    IReadOnlyList<UnitInstanceId> TargetInstanceIds);

public sealed class EffectPipeline
{
    public IReadOnlyList<ResolvedEffect> ResolveEvent(UnitDefinition sourceDefinition, NativeTriggerKey eventKey, EffectResolutionContext context, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(sourceDefinition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(randomSource);
        if (!NativeTriggerKeys.IsSupported(eventKey)) throw new ArgumentException($"Unsupported trigger '{eventKey}'.", nameof(eventKey));
        var resolved = new List<ResolvedEffect>();
        var sequence = 0;
        foreach (var trigger in sourceDefinition.Triggers)
        {
            if (trigger.Event != eventKey || !ConditionsMatch(trigger, context)) continue;
            foreach (var effect in trigger.Effects)
                resolved.Add(new ResolvedEffect(++sequence, context.SourceInstanceId, eventKey, effect, ResolveTargets(effect, context, randomSource)));
        }
        return resolved;
    }

    public IReadOnlyList<ResolvedEffect> ResolveTrigger(UnitDefinition sourceDefinition, TriggerDefinition trigger, EffectResolutionContext context, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(sourceDefinition);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(randomSource);
        if (!sourceDefinition.Triggers.Contains(trigger)) throw new ArgumentException("Trigger does not belong to the source definition.", nameof(trigger));
        if (!ConditionsMatch(trigger, context)) return [];
        var resolved = new List<ResolvedEffect>(trigger.Effects.Count);
        var sequence = 0;
        foreach (var effect in trigger.Effects)
            resolved.Add(new ResolvedEffect(++sequence, context.SourceInstanceId, trigger.Event, effect, ResolveTargets(effect, context, randomSource)));
        return resolved;
    }

    public int EvaluateValue(EffectValueExpression expression, EffectResolutionContext context, UnitInstanceId? targetInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);
        return expression switch
        {
            ConstantEffectValueExpression constant => constant.Value,
            SourceStatEffectValueExpression sourceStat => GetSourceStat(context, sourceStat.Stat),
            TargetStatEffectValueExpression targetStat => GetTargetStat(context, targetInstanceId, targetStat.Stat),
            UnitCountEffectValueExpression unitCount => QueryUnits(unitCount.Query, context).Count,
            EventCountEffectValueExpression eventCount => context.GetEventCount(eventCount.Query),
            CompositeEffectValueExpression composite => EvaluateComposite(composite, context, targetInstanceId),
            _ => throw new ArgumentOutOfRangeException(nameof(expression), expression.GetType().Name, "Unsupported effect value expression."),
        };
    }

    private int EvaluateComposite(CompositeEffectValueExpression expression, EffectResolutionContext context, UnitInstanceId? targetInstanceId)
    {
        var values = expression.Values.Select(value => EvaluateValue(value, context, targetInstanceId)).ToArray();
        return expression.Operation switch
        {
            EffectValueOperation.Add => values.Aggregate(0, (left, right) => checked(left + right)),
            EffectValueOperation.Multiply => values.Aggregate(1, (left, right) => checked(left * right)),
            EffectValueOperation.Min => values.Min(),
            EffectValueOperation.Max => values.Max(),
            _ => throw new ArgumentOutOfRangeException(nameof(expression.Operation), expression.Operation, "Unsupported effect value operation."),
        };
    }

    private bool ConditionsMatch(TriggerDefinition trigger, EffectResolutionContext context)
    {
        foreach (var condition in trigger.Conditions)
        {
            var matched = condition switch
            {
                CombatOutcomeConditionDefinition outcome => context.CombatOutcome == outcome.Outcome,
                UnitCountConditionDefinition count => Compare(QueryUnits(count.Query, context).Count, count.Comparison, count.Value),
                SourceStatConditionDefinition sourceStat => Compare(GetSourceStat(context, sourceStat.Stat), sourceStat.Comparison, sourceStat.Value),
                ValueConditionDefinition value => Compare(EvaluateValue(value.Left, context), value.Comparison, EvaluateValue(value.Right, context)),
                _ => throw new ArgumentOutOfRangeException(nameof(condition), condition.GetType().Name, "Unsupported effect condition."),
            };
            if (!matched) return false;
        }
        return true;
    }

    private static int GetSourceStat(EffectResolutionContext context, EffectStat stat) =>
        GetStat(context.Units.Single(unit => unit.InstanceId == context.SourceInstanceId), stat);

    private static int GetTargetStat(EffectResolutionContext context, UnitInstanceId? targetInstanceId, EffectStat stat)
    {
        if (targetInstanceId is null) throw new InvalidOperationException("targetStat value expressions require an effect target.");
        var target = context.Units.SingleOrDefault(unit => unit.InstanceId == targetInstanceId.Value)
            ?? throw new InvalidOperationException($"Effect target '{targetInstanceId.Value}' is not present in the value context.");
        return GetStat(target, stat);
    }

    private static int GetStat(EffectUnitSnapshot unit, EffectStat stat) => stat switch
    {
        EffectStat.Attack => unit.Attack,
        EffectStat.Health => unit.Health,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Unsupported effect stat."),
    };

    private static bool Compare(int left, EffectComparison comparison, int right) => comparison switch
    {
        EffectComparison.Equal => left == right,
        EffectComparison.NotEqual => left != right,
        EffectComparison.LessThan => left < right,
        EffectComparison.LessThanOrEqual => left <= right,
        EffectComparison.GreaterThan => left > right,
        EffectComparison.GreaterThanOrEqual => left >= right,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison), comparison, "Unsupported comparison."),
    };

    private static IReadOnlyList<UnitInstanceId> ResolveTargets(EffectDefinition effect, EffectResolutionContext context, IRandomSource randomSource)
    {
        var selector = effect switch
        {
            ModifyStatsEffectDefinition value => value.Target,
            DealDamageEffectDefinition value => value.Target,
            DestroyUnitEffectDefinition value => value.Target,
            TriggerEventEffectDefinition value => value.Target,
            AddBehaviorEffectDefinition value => value.Target,
            RemoveBehaviorEffectDefinition value => value.Target,
            TransformUnitEffectDefinition value => value.Target,
            CopyUnitToReserveEffectDefinition value => value.Target,
            ReturnUnitToReserveEffectDefinition value => value.Target,
            ApplyUnitModifierEffectDefinition value => value.Target,
            RemoveUnitModifierEffectDefinition value => value.Target,
            _ => null,
        };
        if (selector is null) return [];

        var candidates = QueryUnits(selector.Query, context).ToArray();
        IEnumerable<EffectUnitSnapshot> selected = selector.Selection switch
        {
            EffectTargetSelection.All => candidates,
            EffectTargetSelection.Random => SelectRandom(candidates, selector.Limit ?? 1, randomSource),
            EffectTargetSelection.LowestAttack => candidates.OrderBy(unit => unit.Attack).ThenBy(unit => unit.Position),
            EffectTargetSelection.HighestAttack => candidates.OrderByDescending(unit => unit.Attack).ThenBy(unit => unit.Position),
            EffectTargetSelection.LowestHealth => candidates.OrderBy(unit => unit.Health).ThenBy(unit => unit.Position),
            EffectTargetSelection.HighestHealth => candidates.OrderByDescending(unit => unit.Health).ThenBy(unit => unit.Position),
            EffectTargetSelection.Leftmost => candidates.OrderBy(unit => unit.Position),
            EffectTargetSelection.Rightmost => candidates.OrderByDescending(unit => unit.Position),
            EffectTargetSelection.Adjacent => SelectAdjacent(candidates, context, selector.RelativeTo, true, true),
            EffectTargetSelection.LeftAdjacent => SelectAdjacent(candidates, context, selector.RelativeTo, true, false),
            EffectTargetSelection.RightAdjacent => SelectAdjacent(candidates, context, selector.RelativeTo, false, true),
            _ => throw new ArgumentOutOfRangeException(nameof(selector.Selection), selector.Selection, "Unsupported target selection."),
        };

        if (selector.Selection != EffectTargetSelection.Random && selector.Limit is not null)
            selected = selected.Take(selector.Limit.Value);
        else if (selector.Selection is EffectTargetSelection.LowestAttack or EffectTargetSelection.HighestAttack or
                 EffectTargetSelection.LowestHealth or EffectTargetSelection.HighestHealth or
                 EffectTargetSelection.Leftmost or EffectTargetSelection.Rightmost)
            selected = selected.Take(selector.Limit ?? 1);
        return selected.Select(unit => unit.InstanceId).ToArray();
    }

    private static IReadOnlyList<EffectUnitSnapshot> QueryUnits(EffectUnitQuery query, EffectResolutionContext context) =>
        context.Units
            .Where(unit => unit.IsAlive && unit.IsSelectable)
            .Where(unit => unit.Zone == query.Zone)
            .Where(unit => IsInScope(unit, query.Scope, context))
            .Where(unit => !query.ExcludeSource || unit.InstanceId != context.SourceInstanceId)
            .Where(unit => query.RequiredTypeId is null || unit.HasType(query.RequiredTypeId.Value))
            .Where(unit => query.RequiredTagId is null || unit.HasTag(query.RequiredTagId.Value))
            .OrderBy(unit => unit.OwnerPlayerId == context.SourcePlayerId ? 0 : 1)
            .ThenBy(unit => unit.Position)
            .ThenBy(unit => unit.InstanceId.Value)
            .ToArray();

    private static bool IsInScope(EffectUnitSnapshot unit, EffectTargetScope scope, EffectResolutionContext context) => scope switch
    {
        EffectTargetScope.Self => unit.InstanceId == context.SourceInstanceId,
        EffectTargetScope.Selected => context.SelectedTargetInstanceId == unit.InstanceId,
        EffectTargetScope.Friendly => unit.OwnerPlayerId == context.SourcePlayerId,
        EffectTargetScope.Enemy => unit.OwnerPlayerId != context.SourcePlayerId,
        _ => false,
    };

    private static IReadOnlyList<EffectUnitSnapshot> SelectRandom(IReadOnlyList<EffectUnitSnapshot> candidates, int count, IRandomSource randomSource)
    {
        var remaining = candidates.ToList();
        var result = new List<EffectUnitSnapshot>(Math.Min(count, remaining.Count));
        while (remaining.Count > 0 && result.Count < count)
        {
            var index = randomSource.NextInt(0, remaining.Count);
            result.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
        return result;
    }

    private static IReadOnlyList<EffectUnitSnapshot> SelectAdjacent(
        IReadOnlyList<EffectUnitSnapshot> candidates,
        EffectResolutionContext context,
        EffectTargetAnchor relativeTo,
        bool includeLeft,
        bool includeRight)
    {
        EffectUnitSnapshot? anchor = relativeTo switch
        {
            EffectTargetAnchor.Source => context.Units.Single(unit => unit.InstanceId == context.SourceInstanceId),
            EffectTargetAnchor.Selected when context.SelectedTargetInstanceId is UnitInstanceId selectedId =>
                context.Units.SingleOrDefault(unit => unit.InstanceId == selectedId),
            EffectTargetAnchor.Selected => null,
            _ => throw new ArgumentOutOfRangeException(nameof(relativeTo), relativeTo, "Unsupported target anchor."),
        };
        if (anchor is null || anchor.Position < 0) return [];

        var result = new List<EffectUnitSnapshot>(2);
        if (includeLeft)
        {
            var left = candidates.FirstOrDefault(unit => unit.OwnerPlayerId == anchor.OwnerPlayerId && unit.Position == anchor.Position - 1);
            if (left is not null) result.Add(left);
        }
        if (includeRight)
        {
            var right = candidates.FirstOrDefault(unit => unit.OwnerPlayerId == anchor.OwnerPlayerId && unit.Position == anchor.Position + 1);
            if (right is not null) result.Add(right);
        }
        return result;
    }
}
