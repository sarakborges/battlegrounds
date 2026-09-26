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
            sequence++;

            var attackerSide = attackingLeft ? left : right;
            var targetSide = attackingLeft ? right : left;

            var attacker = attackerSide.TakeNextAttacker();
            if (attacker is null)
            {
                return BuildResult(left, right, CombatEndReason.Elimination, attacks);
            }

            var targets = targetSide.GetLivingUnits();
            if (targets.Count == 0)
            {
                return BuildResult(left, right, CombatEndReason.Elimination, attacks);
            }

            var target = targets[randomSource.NextInt(0, targets.Count)];

            var attackerDamage = target.Attack;
            var targetDamage = attacker.Attack;

            attacker.Health -= attackerDamage;
            target.Health -= targetDamage;

            attacks.Add(new CombatAttack(
                sequence,
                attackerSide.PlayerId,
                attacker.InstanceId,
                targetSide.PlayerId,
                target.InstanceId,
                attacker.Health,
                target.Health,
                attacker.Health <= 0,
                target.Health <= 0));

            var terminal = CreateResultIfTerminal(left, right, attacks);
            if (terminal is not null)
            {
                return terminal;
            }

            attackingLeft = !attackingLeft;
        }
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
            _units = participant.Units
                .Select(unit => new UnitState(unit.InstanceId, unit.Attack, unit.Health))
                .ToList();
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
                if (candidate.Health <= 0)
                {
                    continue;
                }

                _nextAttackerIndex = (index + 1) % _units.Count;
                return candidate;
            }

            return null;
        }

        public IReadOnlyList<UnitState> GetLivingUnits() =>
            _units.Where(unit => unit.Health > 0).ToArray();

        public IReadOnlyList<CombatSurvivor> GetSurvivors() =>
            _units
                .Where(unit => unit.Health > 0)
                .Select(unit => new CombatSurvivor(unit.InstanceId, unit.Health))
                .ToArray();
    }

    private sealed class UnitState
    {
        public UnitInstanceId InstanceId { get; }
        public int Attack { get; }
        public int Health { get; set; }

        public UnitState(UnitInstanceId instanceId, int attack, int health)
        {
            InstanceId = instanceId;
            Attack = attack;
            Health = health;
        }
    }
}
