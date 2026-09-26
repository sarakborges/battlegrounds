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
        Assert.Equal(CombatEndReason.Elimination, result.EndReason);
    }

    [Fact]
    public void Resolve_AppliesAttackDamageSimultaneously()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 3, 2)),
            Participant(1, Snapshot(2, 2, 3)));
        var rules = new CombatRules(StartingSidePolicy.LargerFieldThenRandom);

        var result = new CombatEngine().Resolve(input, rules, new SeededRandomSource(1));

        var attack = Assert.Single(result.Attacks);
        Assert.Equal(0, attack.AttackerHealthAfter);
        Assert.Equal(0, attack.TargetHealthAfter);
        Assert.True(attack.AttackerDied);
        Assert.True(attack.TargetDied);
        Assert.True(result.IsDraw);
        Assert.Equal(CombatEndReason.Elimination, result.EndReason);
        Assert.Empty(result.LeftSurvivors);
        Assert.Empty(result.RightSurvivors);
    }

    [Fact]
    public void Resolve_AllSurvivorsHaveZeroAttack_EndsAsDrawWithoutAttacks()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 0, 5)),
            Participant(1, Snapshot(2, 0, 5)));
        var rules = new CombatRules(StartingSidePolicy.Random);

        var result = new CombatEngine().Resolve(input, rules, new SeededRandomSource(99));

        Assert.True(result.IsDraw);
        Assert.Equal(CombatEndReason.NoAttackPower, result.EndReason);
        Assert.Equal(0, result.AttackCount);
        Assert.Equal(5, Assert.Single(result.LeftSurvivors).Health);
        Assert.Equal(5, Assert.Single(result.RightSurvivors).Health);
    }

    [Fact]
    public void Resolve_LastPositiveAttackUnitsDie_EndsWhenOnlyZeroAttackSurvivorsRemain()
    {
        var input = new CombatInput(
            Participant(0, Snapshot(1, 2, 2), Snapshot(2, 0, 5)),
            Participant(1, Snapshot(3, 2, 2), Snapshot(4, 0, 5)));
        var rules = new CombatRules(StartingSidePolicy.Random);

        var result = new CombatEngine().Resolve(input, rules, new MinimumRandomSource());

        var attack = Assert.Single(result.Attacks);
        Assert.True(attack.AttackerDied);
        Assert.True(attack.TargetDied);
        Assert.True(result.IsDraw);
        Assert.Equal(CombatEndReason.NoAttackPower, result.EndReason);
        Assert.Equal(new UnitInstanceId(2), Assert.Single(result.LeftSurvivors).InstanceId);
        Assert.Equal(new UnitInstanceId(4), Assert.Single(result.RightSurvivors).InstanceId);
    }

    [Fact]
    public void Resolve_DoesNotMutateInputSnapshot()
    {
        var leftUnit = Snapshot(1, 5, 5);
        var rightUnit = Snapshot(2, 5, 5);
        var input = new CombatInput(Participant(0, leftUnit), Participant(1, rightUnit));
        var rules = new CombatRules(StartingSidePolicy.Random);

        _ = new CombatEngine().Resolve(input, rules, new SeededRandomSource(5));

        Assert.Equal(5, input.Left.Units[0].Health);
        Assert.Equal(5, input.Right.Units[0].Health);
    }

    private static CombatParticipant Participant(int playerId, params CombatUnitSnapshot[] units) =>
        new(new PlayerId(playerId), units);

    private static CombatUnitSnapshot Snapshot(long instanceId, int attack, int health) =>
        new(new UnitInstanceId(instanceId), new UnitId($"unit-{instanceId}"), 1, attack, health);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
