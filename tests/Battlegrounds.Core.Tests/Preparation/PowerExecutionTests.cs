using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class PowerExecutionTests
{
    [Fact]
    public void UsePower_SelectedTargetAppliesEffectsAndEnforcesPerTurnLimit()
    {
        var setup = CreateStartedMatch(new PowerId("buff"));
        var player = setup.Match.Players[0];

        Assert.True(setup.Engine.Execute(setup.Match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        var target = Assert.Single(player.Field);
        var attackBefore = target.Attack;
        var healthBefore = target.Health;
        var resourceBefore = player.Resource;

        var result = setup.Engine.Execute(
            setup.Match,
            new UsePowerCommand(player.Id, target.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(attackBefore + 1, target.Attack);
        Assert.Equal(healthBefore + 1, target.Health);
        Assert.Equal(resourceBefore - 1, player.Resource);

        var second = setup.Engine.Execute(
            setup.Match,
            new UsePowerCommand(player.Id, target.Id));
        Assert.False(second.Succeeded);
        Assert.Equal(PreparationFailureCode.PowerUsageLimitReached, second.FailureCode);
    }

    [Fact]
    public void UsePower_SetPowerChangesCurrentPowerWithoutChangingLeaderDefinition()
    {
        var setup = CreateStartedMatch(new PowerId("shift"));
        var player = setup.Match.Players[0];
        var leader = Assert.IsType<LeaderState>(player.Leader);

        Assert.Equal(new PowerId("shift"), leader.CurrentPowerId);
        Assert.Equal(new PowerId("shift"), leader.Definition.InitialPowerId);

        var result = setup.Engine.Execute(setup.Match, new UsePowerCommand(player.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(new PowerId("buff"), leader.CurrentPowerId);
        Assert.Equal(new PowerId("shift"), leader.Definition.InitialPowerId);

        var newPowerWithoutRequiredTarget = setup.Engine.Execute(
            setup.Match,
            new UsePowerCommand(player.Id));
        Assert.False(newPowerWithoutRequiredTarget.Succeeded);
        Assert.Equal(PreparationFailureCode.InvalidPowerTarget, newPowerWithoutRequiredTarget.FailureCode);
    }

    private static (MatchState Match, PreparationEngine Engine) CreateStartedMatch(PowerId initialPowerId)
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 2, 2);
        var unitCatalog = new UnitCatalog([unit]);
        var pool = new UnitPool(unitCatalog, [new UnitPoolEntry(unit.Id, 20)]);
        var behaviorCatalog = new BehaviorCatalog([]);
        var powers = new PowerCatalog([
            new PowerDefinition(
                new PowerId("buff"),
                "Buff",
                cost: 1,
                maxUsesPerTurn: 1,
                effects: [
                    new ModifyStatsEffectDefinition(
                        new EffectTargetSelector(EffectTargetScope.Selected),
                        attackDelta: 1,
                        healthDelta: 1),
                ]),
            new PowerDefinition(
                new PowerId("shift"),
                "Shift",
                cost: 0,
                maxUsesPerTurn: 1,
                effects: [new SetPowerEffectDefinition(new PowerId("buff"))],
                maxUsesPerMatch: 1),
        ]);
        var leaders = new LeaderCatalog([
            new LeaderDefinition(new LeaderId("leader-a"), "Leader A", 0, 0, initialPowerId),
            new LeaderDefinition(new LeaderId("leader-b"), "Leader B", 0, 0, new PowerId("buff")),
        ]);
        var match = MatchState.Create(
            [
                new PlayerSetup(new PlayerId(0), new LeaderId("leader-a")),
                new PlayerSetup(new PlayerId(1), new LeaderId("leader-b")),
            ],
            new MatchRules(2, 8),
            leaders);
        var rules = new PreparationRules(
            startingResource: 5,
            resourcePerRound: 1,
            maximumResource: 10,
            acquireCost: 0,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [1, 1],
            initialUpgradeCostsByTier: [5]);
        var engine = new PreparationEngine(
            rules,
            pool,
            new SeededRandomSource(7),
            unitCatalog,
            behaviorCatalog,
            powers);
        engine.BeginPreparation(match);
        return (match, engine);
    }
}
