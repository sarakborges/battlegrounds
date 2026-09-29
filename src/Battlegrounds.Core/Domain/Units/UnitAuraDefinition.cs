using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Core.Domain.Units;

public sealed record UnitAuraDefinition
{
    private readonly ReadOnlyCollection<BehaviorDefinition> _grantedBehaviors;

    public EffectTargetSelector Target { get; }
    public int AttackDelta { get; }
    public int HealthDelta { get; }
    public IReadOnlyList<BehaviorDefinition> GrantedBehaviors => _grantedBehaviors;

    public UnitAuraDefinition(
        EffectTargetSelector target,
        int attackDelta = 0,
        int healthDelta = 0,
        IEnumerable<BehaviorDefinition>? grantedBehaviors = null)
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
            throw new ArgumentOutOfRangeException(nameof(attackDelta), "The current aura surface supports non-negative stat bonuses only.");

        var behaviors = (grantedBehaviors ?? []).ToArray();
        if (behaviors.Any(behavior => behavior is null))
            throw new ArgumentException("Aura behavior definitions cannot contain null entries.", nameof(grantedBehaviors));
        var duplicateHandler = behaviors.GroupBy(behavior => behavior.Handler).FirstOrDefault(group => group.Count() > 1);
        if (duplicateHandler is not null)
            throw new ArgumentException($"Aura cannot grant native behavior '{duplicateHandler.Key}' more than once.", nameof(grantedBehaviors));
        if (behaviors.Any(behavior =>
            behavior.Handler != NativeBehaviorKeys.TargetPriority &&
            behavior.Handler != NativeBehaviorKeys.ExtraAttack))
            throw new ArgumentException("Behavior auras currently support targetPriority and extraAttack only.", nameof(grantedBehaviors));
        if (attackDelta == 0 && healthDelta == 0 && behaviors.Length == 0)
            throw new ArgumentException("Unit aura requires a stat bonus or granted behavior.");

        AttackDelta = attackDelta;
        HealthDelta = healthDelta;
        _grantedBehaviors = Array.AsReadOnly(behaviors);
    }
}
