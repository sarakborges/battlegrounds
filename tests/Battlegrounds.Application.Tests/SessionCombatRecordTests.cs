using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.Application.Tests;

public sealed class SessionCombatRecordTests
{
    [Fact]
    public void AdvanceAutomated_CapturesStartingUnitsBeforeCombatSettlement()
    {
        var mod = new ModLoader().Load(FindExampleModDirectory());
        var human = new PlayerId(0);
        var session = SinglePlayerSession.Create(mod, human, [new PlayerId(1)], seed: 12345);
        var leader = session.LeaderSelection.GetOffer(human)[0];
        Assert.True(session.SelectHumanLeader(leader).Succeeded);

        var acquire = session.ExecuteHumanPreparation(new AcquirePlayableCommand(human, 0));
        Assert.True(acquire.Succeeded);
        var deploy = session.ExecuteHumanPreparation(new DeployUnitCommand(human, 0));
        Assert.True(deploy.Succeeded);
        var humanUnit = Assert.Single(session.Match!.Players.Single(player => player.Id == human).Field);

        session.AdvanceAutomated();
        Assert.True(session.ExecuteHumanPreparation(new EndPreparationCommand(human)).Succeeded);
        var advance = session.AdvanceAutomated();

        var record = Assert.IsType<SessionCombatRecord>(session.LastCombat);
        Assert.Equal(1, record.Sequence);
        Assert.Equal(1, record.Round);
        Assert.Same(advance.CombatRound, record.RoundResult);
        Assert.Equal(advance.Pairings, record.Pairings);

        var snapshot = Assert.Single(record.StartingUnits, unit => unit.InstanceId == humanUnit.Id);
        Assert.Equal(human, snapshot.PlayerId);
        Assert.Equal(humanUnit.Definition.Id, snapshot.UnitId);
        Assert.Equal(humanUnit.Definition.Name, snapshot.Name);
        Assert.Equal(humanUnit.Attack, snapshot.Attack);
        Assert.Equal(humanUnit.Health, snapshot.Health);
    }

    private static string FindExampleModDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "mods", "example");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate mods/example from the test output directory.");
    }
}
