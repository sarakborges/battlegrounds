using Battlegrounds.AI;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Taxonomy;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.AI.Tests;

public sealed class PreparationAiStrategyTests
{
    [Fact]
    public void PlayPreparation_SamePersonalityChoosesDifferentUnitForDifferentStrategy()
    {
        var balancedChoice = PlayStrategy(PreparationAiStrategy.Balanced);
        var betaChoice = PlayStrategy(PreparationAiStrategy.PreferType(new UnitTypeId("beta")));

        Assert.Equal(new UnitId("alpha-bruiser"), balancedChoice);
        Assert.Equal(new UnitId("beta-scout"), betaChoice);
    }

    [Fact]
    public void Strategy_TypeAndTagBonusesAreIndependentAndAdditive()
    {
        var type = new UnitTypeDefinition(new UnitTypeId("organic"), "Organic");
        var tag = new TagDefinition(new TagId("scaler"), "Scaler");
        var unit = new UnitDefinition(
            new UnitId("growth"),
            "Growth",
            1,
            1,
            1,
            types: [type],
            tags: [tag]);
        var strategy = new PreparationAiStrategy(
            "organic-scaler",
            preferredTypeIds: [type.Id],
            preferredTagIds: [tag.Id],
            typeMatchBonus: 100,
            tagMatchBonus: 40);

        Assert.Equal(140, strategy.GetUnitPreferenceBonus(unit));
    }

    private static UnitId PlayStrategy(PreparationAiStrategy strategy)
    {
        var alpha = new UnitTypeDefinition(new UnitTypeId("alpha"), "Alpha");
        var beta = new UnitTypeDefinition(new UnitTypeId("beta"), "Beta");
        var alphaBruiser = new UnitDefinition(
            new UnitId("alpha-bruiser"),
            "Alpha Bruiser",
            1,
            10,
            10,
            types: [alpha]);
        var betaScout = new UnitDefinition(
            new UnitId("beta-scout"),
            "Beta Scout",
            1,
            1,
            1,
            types: [beta]);
        var units = new UnitCatalog([alphaBruiser, betaScout]);
        var rules = new PreparationRules(
            startingResource: 3,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 1,
            reserveCapacity: 10,
            maximumTier: 1,
            offerSizesByTier: [2],
            initialUpgradeCostsByTier: []);
        var random = new MinimumRandomSource();
        var pool = new UnitPool(
            units,
            [new UnitPoolEntry(alphaBruiser.Id, 1), new UnitPoolEntry(betaScout.Id, 1)]);
        var engine = new MatchEngine(
            new MatchRules(2, 2),
            rules,
            new CombatRules(StartingSidePolicy.Random),
            pool,
            random,
            unitCatalog: units);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);
        var agent = new PreparationAiAgent(rules, random);

        agent.PlayPreparation(
            engine,
            match,
            new PlayerId(0),
            personality: PreparationAiPersonality.Tempo,
            strategy: strategy);

        return Assert.Single(match.Players[0].Field).Definition.Id;
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
