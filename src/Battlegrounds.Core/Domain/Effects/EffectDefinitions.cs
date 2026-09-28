using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Effects;

public enum EffectTargetScope
{
    Self,
    Selected,
    Friendly,
    Enemy,
}

public enum EffectTargetSelection
{
    All,
    Random,
    LowestAttack,
    HighestAttack,
    LowestHealth,
    HighestHealth,
    Leftmost,
    Rightmost,
    Adjacent,
    LeftAdjacent,
    RightAdjacent,
}

public enum EffectTargetAnchor
{
    Source,
    Selected,
}

public sealed record EffectUnitQuery
{
    public EffectTargetScope Scope { get; }
    public bool ExcludeSource { get; }
    public UnitTypeId? RequiredTypeId { get; }
    public TagId? RequiredTagId { get; }

    public EffectUnitQuery(
        EffectTargetScope scope,
        bool excludeSource = false,
        UnitTypeId? requiredTypeId = null,
        TagId? requiredTagId = null)
    {
        if (excludeSource && scope is EffectTargetScope.Self or EffectTargetScope.Selected or EffectTargetScope.Enemy)
            throw new ArgumentException("excludeSource is only valid for friendly queries.", nameof(excludeSource));

        Scope = scope;
        ExcludeSource = excludeSource;
        RequiredTypeId = requiredTypeId;
        RequiredTagId = requiredTagId;
    }
}

public sealed record EffectTargetSelector
{
    public EffectUnitQuery Query { get; }
    public EffectTargetSelection Selection { get; }
    public int? Limit { get; }
    public EffectTargetAnchor RelativeTo { get; }

    public EffectTargetScope Scope => Query.Scope;
    public bool ExcludeSource => Query.ExcludeSource;
    public UnitTypeId? RequiredTypeId => Query.RequiredTypeId;
    public TagId? RequiredTagId => Query.RequiredTagId;

    public EffectTargetSelector(
        EffectTargetScope scope,
        EffectTargetSelection selection = EffectTargetSelection.All,
        bool excludeSource = false,
        int? limit = null,
        UnitTypeId? requiredTypeId = null,
        TagId? requiredTagId = null,
        EffectTargetAnchor relativeTo = EffectTargetAnchor.Source)
        : this(new EffectUnitQuery(scope, excludeSource, requiredTypeId, requiredTagId), selection, limit, relativeTo)
    {
    }

    public EffectTargetSelector(
        EffectUnitQuery query,
        EffectTargetSelection selection = EffectTargetSelection.All,
        int? limit = null,
        EffectTargetAnchor relativeTo = EffectTargetAnchor.Source)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (limit is not null && limit.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Target limit must be positive.");
        if (query.Scope is EffectTargetScope.Self or EffectTargetScope.Selected &&
            (selection != EffectTargetSelection.All || limit is not null))
        {
            throw new ArgumentException("Self and selected targets cannot use selection or limit.");
        }
        var isAdjacentSelection = selection is EffectTargetSelection.Adjacent or EffectTargetSelection.LeftAdjacent or EffectTargetSelection.RightAdjacent;
        if (relativeTo == EffectTargetAnchor.Selected && !isAdjacentSelection)
            throw new ArgumentException("Selected-relative targeting is only valid for adjacent selections.", nameof(relativeTo));
        if (isAdjacentSelection && relativeTo == EffectTargetAnchor.Source && query.Scope != EffectTargetScope.Friendly)
            throw new ArgumentException("Source-relative adjacent target selection is only valid for friendly targets.", nameof(selection));

        Selection = selection;
        Limit = limit;
        RelativeTo = relativeTo;
    }
}

public enum EffectComparison
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
}

public enum EffectStat
{
    Attack,
    Health,
}

public abstract record EffectConditionDefinition;

public sealed record UnitCountConditionDefinition : EffectConditionDefinition
{
    public EffectUnitQuery Query { get; }
    public EffectComparison Comparison { get; }
    public int Value { get; }

    public UnitCountConditionDefinition(
        EffectUnitQuery query,
        EffectComparison comparison,
        int value)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        Comparison = comparison;
        Value = value;
    }
}

public sealed record SourceStatConditionDefinition : EffectConditionDefinition
{
    public EffectStat Stat { get; }
    public EffectComparison Comparison { get; }
    public int Value { get; }

    public SourceStatConditionDefinition(
        EffectStat stat,
        EffectComparison comparison,
        int value)
    {
        Stat = stat;
        Comparison = comparison;
        Value = value;
    }
}

public sealed record ValueConditionDefinition : EffectConditionDefinition
{
    public EffectValueExpression Left { get; }
    public EffectComparison Comparison { get; }
    public EffectValueExpression Right { get; }

    public ValueConditionDefinition(
        EffectValueExpression left,
        EffectComparison comparison,
        EffectValueExpression right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Comparison = comparison;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }
}

public abstract record EffectDefinition
{
    public abstract NativeEffectKey Kind { get; }
}

public sealed record ModifyStatsEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.ModifyStats;
    public EffectTargetSelector Target { get; }
    public EffectValueExpression AttackDelta { get; }
    public EffectValueExpression HealthDelta { get; }

    public ModifyStatsEffectDefinition(EffectTargetSelector target, int attackDelta, int healthDelta)
        : this(
            target,
            new ConstantEffectValueExpression(attackDelta),
            new ConstantEffectValueExpression(healthDelta))
    {
        if (attackDelta == 0 && healthDelta == 0)
            throw new ArgumentException("modifyStats requires a non-zero attack or health delta.");
    }

    public ModifyStatsEffectDefinition(
        EffectTargetSelector target,
        EffectValueExpression? attackDelta,
        EffectValueExpression? healthDelta)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (attackDelta is null && healthDelta is null)
            throw new ArgumentException("modifyStats requires an attack or health value expression.");
        AttackDelta = attackDelta ?? new ConstantEffectValueExpression(0);
        HealthDelta = healthDelta ?? new ConstantEffectValueExpression(0);
    }
}

public sealed record DealDamageEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.DealDamage;
    public EffectTargetSelector Target { get; }
    public EffectValueExpression Amount { get; }

    public DealDamageEffectDefinition(EffectTargetSelector target, int amount)
        : this(target, new ConstantEffectValueExpression(amount))
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    public DealDamageEffectDefinition(EffectTargetSelector target, EffectValueExpression amount)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
    }
}

public sealed record DestroyUnitEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.DestroyUnit;
    public EffectTargetSelector Target { get; }

    public DestroyUnitEffectDefinition(EffectTargetSelector target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }
}

public sealed record TriggerEventEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.TriggerEvent;
    public EffectTargetSelector Target { get; }
    public NativeTriggerKey Event { get; }

    public TriggerEventEffectDefinition(EffectTargetSelector target, NativeTriggerKey @event)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (!NativeTriggerKeys.IsSupported(@event))
            throw new ArgumentException($"Unsupported trigger '{@event}'.", nameof(@event));
        Event = @event;
    }
}

public sealed record SummonUnitEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.SummonUnit;
    public UnitId UnitId { get; }
    public EffectValueExpression Count { get; }

    public SummonUnitEffectDefinition(UnitId unitId, int count = 1)
        : this(unitId, new ConstantEffectValueExpression(count))
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
    }

    public SummonUnitEffectDefinition(UnitId unitId, EffectValueExpression count)
    {
        UnitId = unitId;
        Count = count ?? throw new ArgumentNullException(nameof(count));
    }
}

public sealed record AddBehaviorEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.AddBehavior;
    public EffectTargetSelector Target { get; }
    public BehaviorId BehaviorId { get; }

    public AddBehaviorEffectDefinition(EffectTargetSelector target, BehaviorId behaviorId)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        BehaviorId = behaviorId;
    }
}

public sealed record RemoveBehaviorEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.RemoveBehavior;
    public EffectTargetSelector Target { get; }
    public BehaviorId BehaviorId { get; }

    public RemoveBehaviorEffectDefinition(EffectTargetSelector target, BehaviorId behaviorId)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        BehaviorId = behaviorId;
    }
}

public sealed record AddResourceEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.AddResource;
    public EffectValueExpression Amount { get; }

    public AddResourceEffectDefinition(int amount)
        : this(new ConstantEffectValueExpression(amount))
    {
        if (amount == 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    public AddResourceEffectDefinition(EffectValueExpression amount)
    {
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
    }
}

public sealed record AdjustUpgradeCostEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.AdjustUpgradeCost;
    public EffectValueExpression Amount { get; }

    public AdjustUpgradeCostEffectDefinition(int amount)
        : this(new ConstantEffectValueExpression(amount))
    {
        if (amount == 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    public AdjustUpgradeCostEffectDefinition(EffectValueExpression amount)
    {
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
    }
}

public sealed record AddAcquireDiscountEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.AddAcquireDiscount;
    public EffectValueExpression Amount { get; }

    public AddAcquireDiscountEffectDefinition(int amount)
        : this(new ConstantEffectValueExpression(amount))
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
    }

    public AddAcquireDiscountEffectDefinition(EffectValueExpression amount)
    {
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
    }
}

public sealed record RefreshOfferEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.RefreshOffer;
}

public sealed record SetPowerEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.SetPower;
    public PowerId PowerId { get; }

    public SetPowerEffectDefinition(PowerId powerId)
    {
        PowerId = powerId;
    }
}

public sealed class TriggerDefinition
{
    private readonly ReadOnlyCollection<EffectDefinition> _effects;
    private readonly ReadOnlyCollection<EffectConditionDefinition> _conditions;

    public NativeTriggerKey Event { get; }
    public int? Count { get; }
    public IReadOnlyList<EffectConditionDefinition> Conditions => _conditions;
    public IReadOnlyList<EffectDefinition> Effects => _effects;
    public TriggerActivationLimit? ActivationLimit { get; }
    public EffectHistoryQuery? Counter { get; }

    public TriggerDefinition(
        NativeTriggerKey @event,
        IEnumerable<EffectDefinition> effects,
        int? count = null,
        IEnumerable<EffectConditionDefinition>? conditions = null,
        TriggerActivationLimit? activationLimit = null,
        EffectHistoryQuery? counter = null)
    {
        if (!NativeTriggerKeys.IsSupported(@event))
            throw new ArgumentException($"Unsupported trigger '{@event}'.", nameof(@event));
        ArgumentNullException.ThrowIfNull(effects);
        var materialized = effects.ToArray();
        if (materialized.Length == 0) throw new ArgumentException("A trigger must contain at least one effect.", nameof(effects));
        if (materialized.Any(effect => effect is null)) throw new ArgumentException("Trigger effects cannot contain null values.", nameof(effects));

        var conditionArray = conditions?.ToArray() ?? [];
        if (conditionArray.Any(condition => condition is null))
            throw new ArgumentException("Trigger conditions cannot contain null values.", nameof(conditions));

        if (@event is NativeTriggerKey triggerKey &&
            (triggerKey == NativeTriggerKeys.AfterFriendlyDeaths || triggerKey == NativeTriggerKeys.AfterEventCount))
        {
            if (count is null || count.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), $"{@event} requires a positive count.");
        }
        else if (count is not null)
        {
            throw new ArgumentException("count is only valid for counted triggers.", nameof(count));
        }

        if (@event == NativeTriggerKeys.AfterEventCount)
        {
            if (counter is null)
                throw new ArgumentNullException(nameof(counter), "afterEventCount requires a counter query.");
        }
        else if (counter is not null)
        {
            throw new ArgumentException("counter is only valid for afterEventCount triggers.", nameof(counter));
        }

        Event = @event;
        Count = count;
        _effects = Array.AsReadOnly(materialized);
        _conditions = Array.AsReadOnly(conditionArray);
        ActivationLimit = activationLimit;
        Counter = counter;
    }
}
