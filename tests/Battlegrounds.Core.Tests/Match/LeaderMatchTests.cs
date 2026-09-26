using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Match;

public sealed class LeaderMatchTests
{
    [Fact]
    public void CreateMatch_AppliesLeaderHealthModifierAndStartingArmor()
    {
        var engine = CreateEngine();

        var match = engine.CreateMatch(
        [
            new PlayerSetup(new PlayerId(0), new LeaderId("vital")),
            new PlayerSetup(new PlayerId(1), new LeaderId("armored")),
        ]);

        Assert.Equal(15, match.Players[0].Health);
        Assert.Equal(new LeaderId("vital"), match.Players[0].Leader!.Definition.Id);
        Assert.Equal(0, match.Players[0].Leader!.Armor);
        Assert.Equal(10, match.Players[1].Health);
        Assert.Equal(new LeaderId("armored"), match.Players[1].Leader!.Definition.Id);
        Assert.Equal(3, match.Players[1].Leader!.Armor);
    }

    [Fact]
    public void ResolveCombatRound_ArmorAbsorbsPlayerDamageBeforeHealth()
    {
        var engine = CreateEngine();
        var match = engine.CreateMatch(
        [
            new PlayerSetup(new PlayerId(0), new LeaderId("vital")),
            new PlayerSetup(new PlayerId(1), new LeaderId("armored")),
        ]);
        engine.BeginMatch(match);

        Assert.True(engine.ExecutePreparation(match, new AcquireUnitCommand(new PlayerId(0), 0)).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new DeployUnitCommand(new PlayerId(0), 0)).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(new PlayerId(0))).Succeeded);
        Assert.True(engine.ExecutePreparation(match, new EndPreparationCommand(new PlayerId(1))).Succeeded);

        var result = engine.ResolveCombatRound(
            match,
            [new CombatPairing(new PlayerId(0), new PlayerId(1))]);

        var settlement = Assert.Single(result.Settlements);
        Assert.Equal(2, settlement.PlayerDamage);
        Assert.Equal(2, settlement.ArmorAbsorbed);
        Assert.Equal(1, settlement.DamagedPlayerArmorAfter);
        Assert.Equal(10, settlement.DamagedPlayerHealthAfter);
        Assert.Equal(1, match.Players[1].Leader!.Armor);
        Assert.Equal(10, match.Players[1].Health);
    }

    private static MatchEngine CreateEngine()
    {
        var unit = new UnitDefinition(
            new UnitId("fighter"),
            "Fighter",
            tier: 1,
            baseAttack: 2,
            baseHealth: 2);
        var units = new UnitCatalog([unit]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 20)]);
        var leaders = new LeaderCatalog(
        [
            new LeaderDefinition(new LeaderId("vital"), "Vital", healthModifier: 5, startingArmor: 0),
            new LeaderDefinition(new LeaderId("armored"), "Armored", healthModifier: 0, startingArmor: 3),
        ]);
        var matchRules = new MatchRules(2, 8, startingHealth: 10);
        var preparationRules = new PreparationRules(
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
        var combatRules = new CombatRules(
            StartingSidePolicy.LargerFieldThenRandom,
            PostCombatDamagePolicy.WinnerTierPlusSurvivorTiers);

        return new MatchEngine(
            matchRules,
            preparationRules,
            combatRules,
            pool,
            new MinimumRandomSource(),
            units,
            new BehaviorCatalog([]),
            leaders);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
