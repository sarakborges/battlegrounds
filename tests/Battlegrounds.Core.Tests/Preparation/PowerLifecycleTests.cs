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

public sealed class PowerLifecycleTests
{
    [Fact]
    public void BeginPreparation_PassivePowerRunsMatchStartThenTurnStart()
    {
        var powerId = new PowerId("income");
        var power = new PowerDefinition(
            powerId,
            "Income",
            activation: null,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnMatchStart,
                    [new AddResourceEffectDefinition(2)]),
                new TriggerDefinition(
                    NativeTriggerKeys.OnTurnStart,
                    [new AddResourceEffectDefinition(1)]),
            ]);
        var setup = CreateSetup(power);

        setup.Engine.BeginPreparation(setup.Match);

        Assert.Equal(6, setup.Match.Players[0].Resource);
    }

    [Fact]
    public void UsePower_PassivePowerCannotBeActivated()
    {
        var power = new PowerDefinition(
            new PowerId("passive"),
            "Passive",
            activation: null,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnTurnStart,
                    [new AddResourceEffectDefinition(1)]),
            ]);
        var setup = CreateSetup(power);
        setup.Engine.BeginPreparation(setup.Match);

        var result = setup.Engine.Execute(
            setup.Match,
            new UsePowerCommand(new PlayerId(0)));

        Assert.False(result.Succeeded);
        Assert.Equal(PreparationFailureCode.PowerNotActivatable, result.FailureCode);
    }

    private static (MatchState Match, PreparationEngine Engine) CreateSetup(PowerDefinition power)
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 1, 1);
        var units = new UnitCatalog([unit]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 20)]);
        var powers = new PowerCatalog([power]);
        var leaders = new LeaderCatalog(
        [
            new LeaderDefinition(new LeaderId("a"), "A", 0, 0, power.Id),
            new LeaderDefinition(new LeaderId("b"), "B", 0, 0, power.Id),
        ]);
        var match = MatchState.Create(
            [
                new PlayerSetup(new PlayerId(0), new LeaderId("a")),
                new PlayerSetup(new PlayerId(1), new LeaderId("b")),
            ],
            new MatchRules(2, 8),
            leaders);
        var rules = new PreparationRules(
            startingResource: 3,
            resourcePerRound: 1,
            maximumResource: 10,
            acquireCost: 0,
            releaseValue: 1,
            refreshCost: 0,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [1, 1],
            initialUpgradeCostsByTier: [5]);
        var engine = new PreparationEngine(
            rules,
            pool,
            new SeededRandomSource(1),
            units,
            new BehaviorCatalog([]),
            powers);
        return (match, engine);
    }
}
