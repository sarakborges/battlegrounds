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

    public UnitInstanceId Id { get; }
    public UnitDefinition Definition { get; }
    public UnitInstanceOrigin Origin { get; }
    public int Attack { get; private set; }
    public int Health { get; private set; }
    public IReadOnlyList<BehaviorDefinition> Behaviors => _behaviorsView;
    public bool IsAlive => Health > 0;

    internal UnitInstance(
        UnitInstanceId id,
        UnitDefinition definition,
        UnitInstanceOrigin origin = UnitInstanceOrigin.Pooled)
    {
        Id = id;
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Origin = origin;
        Attack = definition.BaseAttack;
        Health = definition.BaseHealth;
        _behaviors = definition.Behaviors.ToList();
        _behaviorsView = _behaviors.AsReadOnly();
    }

    internal void ModifyStats(int attackDelta, int healthDelta)
    {
        var nextAttack = Attack + attackDelta;
        if (nextAttack < 0)
        {
            nextAttack = 0;
        }

        Attack = nextAttack;
        Health += healthDelta;
    }

    internal void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Health -= amount;
    }

    internal bool AddBehavior(BehaviorDefinition behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        if (_behaviors.Any(existing => existing.Id == behavior.Id || existing.Handler == behavior.Handler))
        {
            return false;
        }

        _behaviors.Add(behavior);
        return true;
    }

    internal bool RemoveBehavior(BehaviorId behaviorId)
    {
        var index = _behaviors.FindIndex(behavior => behavior.Id == behaviorId);
        if (index < 0)
        {
            return false;
        }

        _behaviors.RemoveAt(index);
        return true;
    }

    internal bool RemoveBehavior(NativeBehaviorKey handler)
    {
        var index = _behaviors.FindIndex(behavior => behavior.Handler == handler);
        if (index < 0)
        {
            return false;
        }

        _behaviors.RemoveAt(index);
        return true;
    }
}
