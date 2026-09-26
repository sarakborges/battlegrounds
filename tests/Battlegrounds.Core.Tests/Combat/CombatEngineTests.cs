using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Combat;

public sealed class CombatEngineTests
{
    [Fact]
    public void Resolve_SameInputAndSeedProduceSameOrderedResult()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 3, 4), Snapshot(2, 2, 3)),
            Participant(1, Snapshot(3, 4, 4), Snapshot(4, 1, 5)));
        var rules = new CombatRules(StartingSidePolicy.LargerFieldThenRandom);
        var engine = new CombatEngine();

        var first = engine.Resolve(input, rules, new SeededRandomSource(42));
        var second = engine.Resolve(input, rules, new SeededRandomSource(42));

        Assert.Equal(first.WinnerPlayerId, second.WinnerPlayerId);
        Assert.Equal(first.EndReason, second.EndReason);
        Assert.Equal(first.Attacks, second.Attacks);
        Assert.Equal(first.LeftSurvivors, second.LeftSurvivors);
        Assert.Equal(first.RightSurvivors, second.RightSurvivors);
    }

    [Fact]
    public void Resolve_LargerFieldStartsWhenConfigured()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 10, 10), Snapshot(2, 1, 10)),
            Participant(1, Snapshot(3, 1, 1)));
        var rules = new CombatRules(StartingSidePolicy.LargerFieldThenRandom);

        var result = new CombatEngine().Resolve(input, rules, new SeededRandomSource(7));

        var firstAttack = Assert.Single(result.Attacks);
        Assert.Equal(new PlayerId(0), firstAttack.AttackerPlayerId);
        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
    }

    [Fact]
    public void Resolve_AppliesAttackDamageSimultaneously()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 3, 2)),
            Participant(1, Snapshot(2, 2, 3)));

        var result = Resolve(input);

        var attack = Assert.Single(result.Attacks);
        Assert.Equal(0, attack.AttackerHealthAfter);
        Assert.Equal(0, attack.TargetHealthAfter);
        Assert.True(attack.AttackerDied);
        Assert.True(attack.TargetDied);
        Assert.True(result.IsDraw);
    }

    [Fact]
    public void Resolve_AllSurvivorsHaveZeroAttack_EndsAsDrawWithoutAttacks()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 0, 5)),
            Participant(1, Snapshot(2, 0, 5)));

        var result = Resolve(input);

        Assert.True(result.IsDraw);
        Assert.Equal(CombatEndReason.NoAttackPower, result.EndReason);
        Assert.Empty(result.Attacks);
    }

    [Fact]
    public void Resolve_ZeroAttackSideIsSkippedWhileOpponentCanAttack()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 0, 3)),
            Participant(1, Snapshot(2, 1, 5)));

        var result = Resolve(input);

        Assert.Equal(new PlayerId(1), result.WinnerPlayerId);
        Assert.All(result.Attacks, attack => Assert.Equal(new PlayerId(1), attack.AttackerPlayerId));
        Assert.Equal(3, result.AttackCount);
    }

    [Fact]
    public void Resolve_TargetPriorityRestrictsRandomTargets()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 10, 10)),
            Participant(
                1,
                Snapshot(2, 0, 5),
                Snapshot(3, 0, 1, Behavior("protector", NativeBehaviorKeys.TargetPriority))));

        var result = Resolve(input);

        Assert.Equal(new UnitInstanceId(3), result.Attacks[0].TargetInstanceId);
    }

    [Fact]
    public void Resolve_DamageBarrierBlocksFirstPositiveDamageOnly()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 3, 20)),
            Participant(1, Snapshot(2, 0, 4, Behavior("ward", NativeBehaviorKeys.DamageBarrier))));

        var result = Resolve(input);

        Assert.True(result.Attacks[0].TargetBarrierLost);
        Assert.Equal(0, result.Attacks[0].DamageToTarget);
        Assert.False(result.Attacks[1].TargetBarrierLost);
        Assert.Equal(3, result.Attacks[1].DamageToTarget);
    }

    [Fact]
    public void Resolve_ReviveOnceReturnsAtOneHealthAndCannotRepeat()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 10, 20)),
            Participant(1, Snapshot(2, 0, 1, Behavior("return", NativeBehaviorKeys.ReviveOnce))));

        var result = Resolve(input);

        Assert.Equal(2, result.AttackCount);
        Assert.True(result.Attacks[0].TargetDied);
        Assert.True(result.Attacks[0].TargetRevived);
        Assert.Equal(1, result.Attacks[0].TargetHealthAfter);
        Assert.True(result.Attacks[1].TargetDied);
        Assert.False(result.Attacks[1].TargetRevived);
        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
    }

    [Fact]
    public void Resolve_LethalFirstDamageDoesNotTriggerThroughBarrierAndTriggersOnNextDamage()
    {
        var input = new CombatInput(
            Participant(
                0,
                Snapshot(
                    1,
                    1,
                    20,
                    Behavior("fatal-touch", NativeBehaviorKeys.LethalFirstDamagePerCombat),
                    Behavior("double-strike", NativeBehaviorKeys.ExtraAttack))),
            Participant(1, Snapshot(2, 0, 100, Behavior("ward", NativeBehaviorKeys.DamageBarrier))));

        var result = Resolve(input);

        Assert.Equal(2, result.AttackCount);
        Assert.True(result.Attacks[0].TargetBarrierLost);
        Assert.False(result.Attacks[0].AttackerLethalTriggered);
        Assert.True(result.Attacks[1].AttackerLethalTriggered);
        Assert.True(result.Attacks[1].TargetDied);
        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
    }

    [Fact]
    public void Resolve_ExtraAttackStrikesTwiceBeforeOtherSideActs()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 1, 20, Behavior("double-strike", NativeBehaviorKeys.ExtraAttack))),
            Participant(1, Snapshot(2, 1, 1), Snapshot(3, 1, 1)));

        var result = Resolve(input);

        Assert.Equal(2, result.AttackCount);
        Assert.All(result.Attacks, attack => Assert.Equal(new UnitInstanceId(1), attack.AttackerInstanceId));
        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
    }

    [Fact]
    public void Resolve_DoesNotMutateInputSnapshot()
    {
        var leftUnit = Snapshot(1, 5, 5, Behavior("ward", NativeBehaviorKeys.DamageBarrier));
        var rightUnit = Snapshot(2, 5, 5);
        var input = new CombatInput(Participant(0, leftUnit), Participant(1, rightUnit));

        _ = Resolve(input);

        Assert.Equal(5, input.Left.Units[0].Health);
        Assert.Single(input.Left.Units[0].Behaviors);
    }

    private static CombatResult Resolve(CombatInput input) =>
        new CombatEngine().Resolve(
            input,
            new CombatRules(StartingSidePolicy.Random),
            new MinimumRandomSource());

    private static CombatParticipant Participant(int playerId, params CombatUnitSnapshot[] units) =>
        new(new PlayerId(playerId), units);

    private static CombatUnitSnapshot Snapshot(
        long instanceId,
        int attack,
        int health,
        params CombatBehaviorSnapshot[] behaviors) =>
        new(
            new UnitInstanceId(instanceId),
            new UnitId($"unit-{instanceId}"),
            1,
            attack,
            health,
            behaviors);

    private static CombatBehaviorSnapshot Behavior(string id, NativeBehaviorKey handler) =>
        new(new BehaviorId(id), handler);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
