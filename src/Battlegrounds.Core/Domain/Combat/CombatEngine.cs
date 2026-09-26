using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Combat;

public sealed class CombatEngine
{
    public CombatResult Resolve(CombatInput input, CombatRules rules, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(randomSource);

        var left = new SideState(input.Left);
        var right = new SideState(input.Right);
        var attacks = new List<CombatAttack>();

        var initial = CreateResultIfTerminal(left, right, attacks);
        if (initial is not null)
        {
            return initial;
        }

        var attackingLeft = ChooseStartingSide(left, right, rules.StartingSidePolicy, randomSource);
        var sequence = 0;

        while (true)
        {
            var attackerSide = attackingLeft ? left : right;
            var targetSide = attackingLeft ? right : left;
            var attacker = attackerSide.TakeNextAttacker();

            if (attacker is null)
            {
                attackingLeft = !attackingLeft;
                continue;
            }

            var strikeCount = attacker.Has(NativeBehaviorKeys.ExtraAttack) ? 2 : 1;

            for (var strike = 0; strike < strikeCount; strike++)
            {
                var targets = targetSide.GetValidTargets();
                if (targets.Count == 0)
                {
                    return BuildResult(left, right, CombatEndReason.Elimination, attacks);
                }

                sequence++;
                var target = targets[randomSource.NextInt(0, targets.Count)];
                var damageToAttacker = ApplyDamage(target, attacker);
                var damageToTarget = ApplyDamage(attacker, target);

                var attackerDied = attacker.Health <= 0;
                var targetDied = target.Health <= 0;
                var attackerRevived = attackerDied && attacker.TryRevive();
                var targetRevived = targetDied && target.TryRevive();

                attacks.Add(new CombatAttack(
                    sequence,
                    attackerSide.PlayerId,
                    attacker.InstanceId,
                    targetSide.PlayerId,
                    target.InstanceId,
                    damageToAttacker.DamageDealt,
                    damageToTarget.DamageDealt,
                    damageToAttacker.BarrierLost,
                    damageToTarget.BarrierLost,
                    damageToTarget.LethalTriggered,
                    damageToAttacker.LethalTriggered,
                    attacker.Health,
                    target.Health,
                    attackerDied,
                    targetDied,
                    attackerRevived,
                    targetRevived));

                var terminal = CreateResultIfTerminal(left, right, attacks);
                if (terminal is not null)
                {
                    return terminal;
                }

                if (attackerDied)
                {
                    break;
                }
            }

            attackingLeft = !attackingLeft;
        }
    }

    private static DamageResult ApplyDamage(UnitState source, UnitState target)
    {
        if (source.Attack <= 0)
        {
            return default;
        }

        if (target.Remove(NativeBehaviorKeys.DamageBarrier))
        {
            return new DamageResult(0, true, false);
        }

        target.Health -= source.Attack;

        var lethalTriggered = source.Remove(NativeBehaviorKeys.LethalFirstDamagePerCombat);
        if (lethalTriggered)
        {
            target.Health = Math.Min(target.Health, 0);
        }

        return new DamageResult(source.Attack, false, lethalTriggered);
    }

    private static bool ChooseStartingSide(
        SideState left,
        SideState right,
        StartingSidePolicy policy,
        IRandomSource randomSource)
    {
        return policy switch
        {
            StartingSidePolicy.Random => randomSource.NextInt(0, 2) == 0,
            StartingSidePolicy.LargerFieldThenRandom =>
                left.LivingCount > right.LivingCount
                    ? true
                    : left.LivingCount < right.LivingCount
                        ? false
                        : randomSource.NextInt(0, 2) == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported starting side policy."),
        };
    }

    private static CombatResult? CreateResultIfTerminal(
        SideState left,
        SideState right,
        IReadOnlyCollection<CombatAttack> attacks)
    {
        if (left.LivingCount == 0 || right.LivingCount == 0)
        {
            return BuildResult(left, right, CombatEndReason.Elimination, attacks);
        }

        if (!left.HasAttackPower && !right.HasAttackPower)
        {
            return BuildResult(left, right, CombatEndReason.NoAttackPower, attacks);
        }

        return null;
    }

    private static CombatResult BuildResult(
        SideState left,
        SideState right,
        CombatEndReason reason,
        IEnumerable<CombatAttack> attacks)
    {
        PlayerId? winner = null;
        if (left.LivingCount > 0 && right.LivingCount == 0)
        {
            winner = left.PlayerId;
        }
        else if (right.LivingCount > 0 && left.LivingCount == 0)
        {
            winner = right.PlayerId;
        }

        return new CombatResult(
            winner,
            reason,
            attacks,
            left.GetSurvivors(),
            right.GetSurvivors());
    }

    private sealed class SideState
    {
        private readonly List<UnitState> _units;
        private int _nextAttackerIndex;

        public PlayerId PlayerId { get; }
        public int LivingCount => _units.Count(unit => unit.Health > 0);
        public bool HasAttackPower => _units.Any(unit => unit.Health > 0 && unit.Attack > 0);

        public SideState(CombatParticipant participant)
        {
            PlayerId = participant.PlayerId;
            _units = participant.Units.Select(unit => new UnitState(unit)).ToList();
        }

        public UnitState? TakeNextAttacker()
        {
            if (_units.Count == 0)
            {
                return null;
            }

            for (var offset = 0; offset < _units.Count; offset++)
            {
                var index = (_nextAttackerIndex + offset) % _units.Count;
                var candidate = _units[index];
                if (candidate.Health <= 0 || candidate.Attack <= 0)
                {
                    continue;
                }

                _nextAttackerIndex = (index + 1) % _units.Count;
                return candidate;
            }

            return null;
        }

        public IReadOnlyList<UnitState> GetValidTargets()
        {
            var living = _units.Where(unit => unit.Health > 0).ToArray();
            var priority = living
                .Where(unit => unit.Has(NativeBehaviorKeys.TargetPriority))
                .ToArray();

            return priority.Length > 0 ? priority : living;
        }

        public IReadOnlyList<CombatSurvivor> GetSurvivors() =>
            _units
                .Where(unit => unit.Health > 0)
                .Select(unit => new CombatSurvivor(unit.InstanceId, unit.Health))
                .ToArray();
    }

    private sealed class UnitState
    {
        private readonly Dictionary<NativeBehaviorKey, BehaviorId> _baseBehaviors;
        private Dictionary<NativeBehaviorKey, BehaviorId> _activeBehaviors;
        private bool _reviveUsed;

        public UnitInstanceId InstanceId { get; }
        public int Attack { get; }
        public int Health { get; set; }

        public UnitState(CombatUnitSnapshot snapshot)
        {
            InstanceId = snapshot.InstanceId;
            Attack = snapshot.Attack;
            Health = snapshot.Health;
            _baseBehaviors = snapshot.Behaviors.ToDictionary(behavior => behavior.Handler, behavior => behavior.Id);
            _activeBehaviors = new Dictionary<NativeBehaviorKey, BehaviorId>(_baseBehaviors);
        }

        public bool Has(NativeBehaviorKey behavior) => _activeBehaviors.ContainsKey(behavior);

        public bool Remove(NativeBehaviorKey behavior) => _activeBehaviors.Remove(behavior);

        public bool TryRevive()
        {
            if (_reviveUsed || !_baseBehaviors.ContainsKey(NativeBehaviorKeys.ReviveOnce))
            {
                return false;
            }

            _reviveUsed = true;
            Health = 1;
            _activeBehaviors = new Dictionary<NativeBehaviorKey, BehaviorId>(_baseBehaviors);
            _activeBehaviors.Remove(NativeBehaviorKeys.ReviveOnce);
            return true;
        }
    }

    private readonly record struct DamageResult(int DamageDealt, bool BarrierLost, bool LethalTriggered);
}
