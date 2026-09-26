using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Effects;

public enum EffectTargetScope
{
    Self,
    Selected,
    RandomFriendly,
    RandomEnemy,
    AllFriendly,
    AllEnemy,
}

public sealed record EffectTargetSelector
{
    public EffectTargetScope Scope { get; }
    public UnitTypeId? RequiredTypeId { get; }
    public TagId? RequiredTagId { get; }

    public EffectTargetSelector(
        EffectTargetScope scope,
        UnitTypeId? requiredTypeId = null,
        TagId? requiredTagId = null)
    {
        Scope = scope;
        RequiredTypeId = requiredTypeId;
        RequiredTagId = requiredTagId;
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
    public int AttackDelta { get; }
    public int HealthDelta { get; }

    public ModifyStatsEffectDefinition(EffectTargetSelector target, int attackDelta, int healthDelta)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (attackDelta == 0 && healthDelta == 0)
            throw new ArgumentException("modifyStats requires a non-zero attack or health delta.");
        AttackDelta = attackDelta;
        HealthDelta = healthDelta;
    }
}

public sealed record DealDamageEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.DealDamage;
    public EffectTargetSelector Target { get; }
    public int Amount { get; }

    public DealDamageEffectDefinition(EffectTargetSelector target, int amount)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
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
    public int Count { get; }

    public SummonUnitEffectDefinition(UnitId unitId, int count = 1)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        UnitId = unitId;
        Count = count;
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
    public int Amount { get; }

    public AddResourceEffectDefinition(int amount)
    {
        if (amount == 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
    }
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

    public NativeTriggerKey Event { get; }
    public int? Count { get; }
    public IReadOnlyList<EffectDefinition> Effects => _effects;

    public TriggerDefinition(
        NativeTriggerKey @event,
        IEnumerable<EffectDefinition> effects,
        int? count = null)
    {
        if (!NativeTriggerKeys.IsSupported(@event))
            throw new ArgumentException($"Unsupported trigger '{@event}'.", nameof(@event));
        ArgumentNullException.ThrowIfNull(effects);
        var materialized = effects.ToArray();
        if (materialized.Length == 0) throw new ArgumentException("A trigger must contain at least one effect.", nameof(effects));
        if (materialized.Any(effect => effect is null)) throw new ArgumentException("Trigger effects cannot contain null values.", nameof(effects));

        if (@event == NativeTriggerKeys.AfterFriendlyDeaths)
        {
            if (count is null || count.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "afterFriendlyDeaths requires a positive count.");
        }
        else if (count is not null)
        {
            throw new ArgumentException("count is only valid for afterFriendlyDeaths triggers.", nameof(count));
        }

        Event = @event;
        Count = count;
        _effects = Array.AsReadOnly(materialized);
    }
}
