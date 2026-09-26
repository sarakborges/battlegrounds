using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Combat;

public sealed class CombatInput
{
    public CombatParticipant Left { get; }
    public CombatParticipant Right { get; }

    public CombatInput(CombatParticipant left, CombatParticipant right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Right = right ?? throw new ArgumentNullException(nameof(right));

        if (left.PlayerId == right.PlayerId)
        {
            throw new ArgumentException("Combat participants must be different players.");
        }

        var duplicateInstance = left.Units
            .Concat(right.Units)
            .GroupBy(unit => unit.InstanceId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateInstance is not null)
        {
            throw new ArgumentException($"Duplicate unit instance id '{duplicateInstance.Key}' in combat input.");
        }
    }

    public static CombatInput FromFields(
        PlayerId leftPlayerId,
        IReadOnlyList<UnitInstance> leftField,
        PlayerId rightPlayerId,
        IReadOnlyList<UnitInstance> rightField)
    {
        ArgumentNullException.ThrowIfNull(leftField);
        ArgumentNullException.ThrowIfNull(rightField);

        return new CombatInput(
            CombatParticipant.FromField(leftPlayerId, leftField),
            CombatParticipant.FromField(rightPlayerId, rightField));
    }
}

public sealed class CombatParticipant
{
    private readonly ReadOnlyCollection<CombatUnitSnapshot> _units;

    public PlayerId PlayerId { get; }
    public IReadOnlyList<CombatUnitSnapshot> Units => _units;

    public CombatParticipant(PlayerId playerId, IEnumerable<CombatUnitSnapshot> units)
    {
        ArgumentNullException.ThrowIfNull(units);

        var materialized = units.ToArray();
        var duplicate = materialized
            .GroupBy(unit => unit.InstanceId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate unit instance id '{duplicate.Key}'.", nameof(units));
        }

        PlayerId = playerId;
        _units = Array.AsReadOnly(materialized);
    }

    internal static CombatParticipant FromField(PlayerId playerId, IReadOnlyList<UnitInstance> field) =>
        new(
            playerId,
            field.Select(unit => new CombatUnitSnapshot(
                unit.Id,
                unit.Definition.Id,
                unit.Definition.Tier,
                unit.Attack,
                unit.Health,
                unit.Behaviors.Select(behavior =>
                    new CombatBehaviorSnapshot(behavior.Id, behavior.Handler)),
                unit.Definition)));
}

public readonly record struct CombatUnitSnapshot
{
    private readonly ReadOnlyCollection<CombatBehaviorSnapshot> _behaviors;

    public UnitInstanceId InstanceId { get; }
    public UnitId UnitId { get; }
    public int Tier { get; }
    public int Attack { get; }
    public int Health { get; }
    public IReadOnlyList<CombatBehaviorSnapshot> Behaviors => _behaviors;
    public UnitDefinition? Definition { get; }

    public CombatUnitSnapshot(
        UnitInstanceId instanceId,
        UnitId unitId,
        int tier,
        int attack,
        int health,
        IEnumerable<CombatBehaviorSnapshot>? behaviors = null,
        UnitDefinition? definition = null)
    {
        if (tier <= 0) throw new ArgumentOutOfRangeException(nameof(tier));
        if (attack < 0) throw new ArgumentOutOfRangeException(nameof(attack));
        if (health <= 0) throw new ArgumentOutOfRangeException(nameof(health));
        if (definition is not null && (definition.Id != unitId || definition.Tier != tier))
        {
            throw new ArgumentException("Combat snapshot definition must match its unit id and tier.", nameof(definition));
        }

        var behaviorArray = behaviors?.ToArray() ?? [];
        var duplicateHandler = behaviorArray
            .GroupBy(behavior => behavior.Handler)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateHandler is not null)
        {
            throw new ArgumentException(
                $"Combat unit cannot attach native behavior '{duplicateHandler.Key}' more than once.",
                nameof(behaviors));
        }

        InstanceId = instanceId;
        UnitId = unitId;
        Tier = tier;
        Attack = attack;
        Health = health;
        _behaviors = Array.AsReadOnly(behaviorArray);
        Definition = definition;
    }
}

public readonly record struct CombatBehaviorSnapshot(BehaviorId Id, NativeBehaviorKey Handler);
