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

    public UnitInstanceId Id { get; }
    public UnitDefinition Definition { get; private set; }
    public UnitInstanceOrigin Origin { get; }
    public int Attack { get; private set; }
    public int Health { get; private set; }
    public IReadOnlyList<BehaviorDefinition> Behaviors => _behaviorsView;
    public IReadOnlyList<UnitModifierState> Modifiers => _modifiersView;
    public bool IsAlive => Health > 0;
    internal UnitDefinition? PoolReturnDefinition { get; }

    internal UnitInstance(
        UnitInstanceId id,
        UnitDefinition definition,
        UnitInstanceOrigin origin = UnitInstanceOrigin.Pooled)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Origin = origin;
        PoolReturnDefinition = origin == UnitInstanceOrigin.Pooled ? definition : null;
        Attack = definition.BaseAttack;
        Health = definition.BaseHealth;
        _behaviors = definition.Behaviors.ToList();
        _behaviorsView = _behaviors.AsReadOnly();
        _modifiersView = _modifiers.AsReadOnly();
    }

    internal void ModifyStats(int attackDelta, int healthDelta)
    {
        Attack = Math.Max(0, Attack + attackDelta);
        Health += healthDelta;
    }

    internal void ApplyModifier(string key, int attackDelta, int healthDelta)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Modifier key cannot be empty.", nameof(key));
        if (attackDelta == 0 && healthDelta == 0) return;

        RemoveModifier(key);
        var modifier = new UnitModifierState(key, attackDelta, healthDelta);
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

    internal void Transform(UnitDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Attack = definition.BaseAttack;
        Health = definition.BaseHealth;
        _behaviors.Clear();
        _behaviors.AddRange(definition.Behaviors);
        _modifiers.Clear();
    }

    internal void CopyRuntimeStateFrom(UnitInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Definition = source.Definition;
        Attack = source.Attack;
        Health = source.Health;
        _behaviors.Clear();
        _behaviors.AddRange(source.Behaviors);
        _modifiers.Clear();
        _modifiers.AddRange(source.Modifiers.Select(modifier =>
            new UnitModifierState(modifier.Key, modifier.AttackDelta, modifier.HealthDelta)));
    }

    internal void TakeDamage(int amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Health -= amount;
    }

    internal void Destroy() => Health = Math.Min(Health, 0);

    internal void ResetForReborn()
    {
        Attack = Definition.BaseAttack;
        Health = 1;
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
