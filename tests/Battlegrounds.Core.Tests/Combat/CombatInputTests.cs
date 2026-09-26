using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Tests.Combat;

public sealed class CombatInputTests
{
    [Fact]
    public void Constructor_RejectsSamePlayerOnBothSides()
    {
        var playerId = new PlayerId(1);

        Assert.Throws<ArgumentException>(() => new CombatInput(
            new CombatParticipant(playerId, []),
            new CombatParticipant(playerId, [])));
    }

    [Fact]
    public void Constructor_RejectsDuplicateInstanceAcrossSides()
    {
        var duplicate = Snapshot(1);

        Assert.Throws<ArgumentException>(() => new CombatInput(
            new CombatParticipant(new PlayerId(0), [duplicate]),
            new CombatParticipant(new PlayerId(1), [duplicate])));
    }

    private static CombatUnitSnapshot Snapshot(long instanceId) =>
        new(new UnitInstanceId(instanceId), new UnitId($"unit-{instanceId}"), 1, 1, 1);
}
