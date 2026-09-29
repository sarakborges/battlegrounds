using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Effects;

public sealed record TransformUnitEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.TransformUnit;
    public EffectTargetSelector Target { get; }
    public UnitId UnitId { get; }

    public TransformUnitEffectDefinition(EffectTargetSelector target, UnitId unitId)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        UnitId = unitId;
    }
}

public sealed record CopyUnitToReserveEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.CopyUnitToReserve;
    public EffectTargetSelector Target { get; }

    public CopyUnitToReserveEffectDefinition(EffectTargetSelector target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }
}

public sealed record ReturnUnitToReserveEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.ReturnUnitToReserve;
    public EffectTargetSelector Target { get; }

    public ReturnUnitToReserveEffectDefinition(EffectTargetSelector target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }
}

public sealed record ApplyUnitModifierEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.ApplyUnitModifier;
    public EffectTargetSelector Target { get; }
    public string ModifierKey { get; }
    public EffectValueExpression AttackDelta { get; }
    public EffectValueExpression HealthDelta { get; }
    public UnitModifierDuration Duration { get; }

    public ApplyUnitModifierEffectDefinition(
        EffectTargetSelector target,
        string modifierKey,
        EffectValueExpression? attackDelta,
        EffectValueExpression? healthDelta,
        UnitModifierDuration duration = UnitModifierDuration.Persistent)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(modifierKey)) throw new ArgumentException("Modifier key cannot be empty.", nameof(modifierKey));
        if (attackDelta is null && healthDelta is null) throw new ArgumentException("A modifier requires attack and/or health.");
        ModifierKey = modifierKey;
        AttackDelta = attackDelta ?? new ConstantEffectValueExpression(0);
        HealthDelta = healthDelta ?? new ConstantEffectValueExpression(0);
        Duration = duration;
    }

    public ApplyUnitModifierEffectDefinition(
        EffectTargetSelector target,
        string modifierKey,
        int attackDelta,
        int healthDelta,
        UnitModifierDuration duration = UnitModifierDuration.Persistent)
        : this(target, modifierKey, new ConstantEffectValueExpression(attackDelta), new ConstantEffectValueExpression(healthDelta), duration) { }
}

public sealed record RemoveUnitModifierEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.RemoveUnitModifier;
    public EffectTargetSelector Target { get; }
    public string ModifierKey { get; }

    public RemoveUnitModifierEffectDefinition(EffectTargetSelector target, string modifierKey)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(modifierKey)) throw new ArgumentException("Modifier key cannot be empty.", nameof(modifierKey));
        ModifierKey = modifierKey;
    }
}

internal interface IPersistentUnitMutationWorld
{
    void TransformUnit(IEffectRuntimeUnit unit, UnitDefinition definition);
    int CopyUnitsToReserve(PlayerId ownerPlayerId, IReadOnlyList<IEffectRuntimeUnit> units);
    int ReturnUnitsToReserve(IReadOnlyList<IEffectRuntimeUnit> units);
    void ApplyModifier(IEffectRuntimeUnit unit, string key, int attackDelta, int healthDelta, UnitModifierDuration duration);
    bool RemoveModifier(IEffectRuntimeUnit unit, string key);
}
