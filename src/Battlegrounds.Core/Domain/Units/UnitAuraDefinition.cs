using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Core.Domain.Units;

public sealed record UnitAuraDefinition
{
    public EffectTargetSelector Target { get; }
    public int AttackDelta { get; }
    public int HealthDelta { get; }

    public UnitAuraDefinition(EffectTargetSelector target, int attackDelta = 0, int healthDelta = 0)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (target.Scope != EffectTargetScope.Friendly)
  throw new ArgumentException("Unit auras currently support friendly targets only.", nameof(target));
        if (target.RelativeTo != EffectTargetAnchor.Source)
  throw new ArgumentException("Unit auras are always source-relative.", nameof(target));
        if (target.Limit is not null)
  throw new ArgumentException("Unit auras do not support target limits.", nameof(target));
        if (target.Selection is not (EffectTargetSelection.All or EffectTargetSelection.Adjacent or EffectTargetSelection.LeftAdjacent or EffectTargetSelection.RightAdjacent))
  throw new ArgumentException("Unit auras support all or source-relative adjacent selections only.", nameof(target));
        if (attackDelta < 0 || healthDelta < 0)
  throw new ArgumentOutOfRangeException(nameof(attackDelta), "The first aura slice supports non-negative stat bonuses only.");
        if (attackDelta == 0 && healthDelta == 0)
  throw new ArgumentException("Unit aura requires a non-zero stat bonus.");

        AttackDelta = attackDelta;
        HealthDelta = healthDelta;
    }
}
