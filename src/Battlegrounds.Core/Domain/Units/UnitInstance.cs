using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Units;

public enum UnitInstanceOrigin
{
    Pooled,
    Generated,
}

public sealed class UnitInstance
{
    private readonly List<BehaviorDefinition> _behaviors;
    private readonly ReadOnlyCollection<BehaviorDefinition> _behaviorsView;
    private readonly List<UnitModifierState> _modifiers = [];
    private readonly ReadOnlyCollection<UnitModifierState> _modifiersView;
    private int _intrinsicAttack;
    private int _intrinsicHealth;
    private int _auraAttack;
    private int _auraHealth;

    public UnitInstanceId Id { get; }
    public UnitDefinition Definition { get; private set; }
    public UnitInstanceOrigin Origin { get; private set; }
    public int Attack => Math.Max(0, _intrinsicAttack + _auraAttack);
    public int Health => _intrinsicHealth + _auraHealth;
    internal int IntrinsicAttack => _intrinsicAttack;
    internal int IntrinsicHealth => _intrinsicHealth;
    public IReadOnlyList<BehaviorDefinition> Behaviors => _behaviorsView;
    public IReadOnlyList<UnitModifierState> Modifiers => _modifiersView;
    public bool IsAlive => Health > 0;
    internal UnitDefinition? PoolReturnDefinition { get; private set; }

    internal UnitInstance(
        UnitInstanceId id,
        UnitDefinition definition,
        UnitInstanceOrigin origin = UnitInstanceOrigin.Pooled)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Origin = origin;
        PoolReturnDefinition = origin == UnitInstanceOrigin.Pooled ? definition : null;
        _intrinsicAttack = definition.BaseAttack;
        _intrinsicHealth = definition.BaseHealth;
        _behaviors = definition.Behaviors.ToList();
        _behaviorsView = _behaviors.AsReadOnly();
        _modifiersView = _modifiers.AsReadOnly();
    }

    internal UnitDefinition? ReleasePoolReturnDefinition()
    {
        var definition = PoolReturnDefinition;
        PoolReturnDefinition = null;
        return definition;
    }

    internal void ModifyStats(int attackDelta, int healthDelta)
    {
        _intrinsicAttack = Math.Max(0, _intrinsicAttack + attackDelta);
        _intrinsicHealth += healthDelta;
    }

    internal void SetAuraContribution(int attackDelta, int healthDelta)
    {
        if (attackDelta < 0 || healthDelta < 0)
  throw new ArgumentOutOfRangeException(nameof(attackDelta));
        _auraAttack = attackDelta;
        _auraHealth = healthDelta;
    }

    internal void ApplyModifier(
        string key,
        int attackDelta,
        int healthDelta,
        UnitModifierDuration duration = UnitModifierDuration.Persistent)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Modifier key cannot be empty.", nameof(key));
        if (attackDelta == 0 && healthDelta == 0) return;

        RemoveModifier(key);
        var modifier = new UnitModifierState(key, attackDelta, healthDelta, duration);
        _modifiers.Add(modifier);
        ModifyStats(attackDelta, healthDelta);
    }

    internal bool RemoveModifier(string key)
    {
        var index = _modifiers.FindIndex(modifier => string.Equals(modifier.Key, key, StringComparison.Ordinal));
        if (index < 0) return false;

        var modifier = _modifiers[index];
        _modifiers.RemoveAt(index);
        ModifyStats(-modifier.AttackDelta, -modifier.HealthDelta);
        return true;
    }

    internal void ExpireModifiers(UnitModifierDuration duration)
    {
        var keys = _modifiers
  .Where(modifier => modifier.Duration == duration)
  .Select(modifier => modifier.Key)
  .ToArray();
        foreach (var key in keys) RemoveModifier(key);
    }

    internal void Transform(UnitDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Origin = UnitInstanceOrigin.Generated;
        PoolReturnDefinition = null;
        _intrinsicAttack = definition.BaseAttack;
        _intrinsicHealth = definition.BaseHealth;
        _auraAttack = 0;
        _auraHealth = 0;
        _behaviors.Clear();
        _behaviors.AddRange(definition.Behaviors);
        _modifiers.Clear();
    }

    internal void CopyRuntimeStateFrom(UnitInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Definition = source.Definition;
        Origin = UnitInstanceOrigin.Generated;
        PoolReturnDefinition = null;
        _intrinsicAttack = source._intrinsicAttack;
        _intrinsicHealth = source._intrinsicHealth;
        _auraAttack = 0;
        _auraHealth = 0;
        _behaviors.Clear();
        _behaviors.AddRange(source.Behaviors);
        _modifiers.Clear();
        _modifiers.AddRange(source.Modifiers.Select(modifier =>
  new UnitModifierState(modifier.Key, modifier.AttackDelta, modifier.HealthDelta, modifier.Duration)));
    }

    internal void TakeDamage(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _intrinsicHealth -= amount;
    }

    internal void Destroy()
    {
        if (Health > 0) _intrinsicHealth -= Health;
    }

    internal void ResetForReborn()
    {
        _intrinsicAttack = Definition.BaseAttack;
        _intrinsicHealth = 1;
        _auraAttack = 0;
        _auraHealth = 0;
        _behaviors.Clear();
        _behaviors.AddRange(
  Definition.Behaviors.Where(behavior => behavior.Handler != NativeBehaviorKeys.ReviveOnce));
        _modifiers.Clear();
    }

    internal bool AddBehavior(BehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        if (_behaviors.Any(existing => existing.Id == behavior.Id || existing.Handler == behavior.Handler)) return false;
        _behaviors.Add(behavior);
        return true;
    }

    internal bool RemoveBehavior(BehaviorId behaviorId)
    {
        var index = _behaviors.FindIndex(behavior => behavior.Id == behaviorId);
        if (index < 0) return false;
        _behaviors.RemoveAt(index);
        return true;
    }

    internal bool RemoveBehavior(NativeBehaviorKey handler)
    {
        var index = _behaviors.FindIndex(behavior => behavior.Handler == handler);
        if (index < 0) return false;
        _behaviors.RemoveAt(index);
        return true;
    }
}
