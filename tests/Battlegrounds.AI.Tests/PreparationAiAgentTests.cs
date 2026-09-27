using Battlegrounds.AI;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.AI.Tests;

public sealed class PreparationAiAgentTests
{
    [Fact]
    public void SelectLeader_UsesSelectionBoundaryAndMechanicalSurvivability()
    {
        var low = new LeaderDefinition(new LeaderId("low"), "Low", healthModifier: 0, startingArmor: 0);
        var high = new LeaderDefinition(new LeaderId("high"), "High", healthModifier: 2, startingArmor: 3);
        var leaders = new LeaderCatalog([low, high]);
        var selection = LeaderSelectionState.Create(
            [new PlayerId(0), new PlayerId(1)],
            new MatchRules(2, 2),
            new LeaderSelectionRules(2, LeaderOfferPolicy.IndependentPerPlayer),
            leaders,
            new MinimumRandomSource());
        var agent = new PreparationAiAgent(CreateRules(), new MinimumRandomSource(), leaders: leaders);

        var selected = agent.SelectLeader(selection, new PlayerId(0));

        Assert.Equal(high.Id, selected);
        Assert.True(selection.TryGetSelection(new PlayerId(0), out var stored));
        Assert.Equal(high.Id, stored);
    }

    [Fact]
    public void PlayPreparation_CombinesExactOwnedUnitsThroughMatchCommandsAndEndsReady()
    {
        var source = new UnitDefinition(new UnitId("scout"), "Scout", 1, 1, 2);
        var result = new UnitDefinition(new UnitId("scout-prime"), "Scout Prime", 2, 4, 6);
        var combines = new UnitCombineCatalog(
        [
            new UnitCombineDefinition(
                new UnitCombineId("scout-upgrade"),
                "Scout Upgrade",
                source.Id,
                requiredCopies: 3,
                resultUnitId: result.Id)
        ]);
        var units = new UnitCatalog([source, result], combines);
        var rules = new PreparationRules(
            startingResource: 9,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [3, 3],
            initialUpgradeCostsByTier: [5]);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 12)]);
        var random = new MinimumRandomSource();
        var engine = new MatchEngine(
            new MatchRules(2, 2),
            rules,
            new CombatRules(StartingSidePolicy.Random),
            pool,
            random,
            unitCatalog: units);
        var match = engine.CreateMatch([new PlayerId(0), new PlayerId(1)]);
        engine.BeginMatch(match);
        var agent = new PreparationAiAgent(rules, random, combines: combines);

        var aiResult = agent.PlayPreparation(engine, match, new PlayerId(0));

        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        Assert.True(aiResult.PlayerReady);
        Assert.True(player.IsReadyForCombat);
        Assert.Single(player.Field);
        Assert.Equal(result.Id, player.Field[0].Definition.Id);
        Assert.Equal(UnitInstanceOrigin.Generated, player.Field[0].Origin);
        Assert.Equal(9, pool.GetAvailableCopies(source.Id));
    }

    [Fact]
    public void PlayPreparation_ResolvesGeneratedChoiceBeforeContinuing()
    {
        var source = new UnitDefinition(
            new UnitId("oracle"),
            "Oracle",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [new GenerateUnitChoiceEffectDefinition(new UnitDefinitionQuery(excludeSource: true), optionCount: 2)])
            ]);
        var low = new UnitDefinition(new UnitId("low-option"), "Low Option", 1, 1, 1);
        var high = new UnitDefinition(new UnitId("high-option"), "High Option", 2, 2, 2);
        var units = new UnitCatalog([source, low, high]);
        var rules = new PreparationRules(3, 0, 10, 3, 1, 1, 3, 10, 2, [1, 1], [5]);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 4)]);
        var random = new MinimumRandomSource();
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

        agent.PlayPreparation(engine, match, new PlayerId(0));

        var player = match.Players[0];
        Assert.Null(player.PendingChoice);
        Assert.Contains(player.Field, unit => unit.Definition.Id == high.Id);
        Assert.True(player.IsReadyForCombat);
    }

    [Fact]
    public void PlayPreparation_UsesActionAndPowerWithSelectedTargets()
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 1, 1);
        var units = new UnitCatalog([unit]);
        var buffTarget = new EffectTargetSelector(EffectTargetScope.Selected);
        var action = new ActionDefinition(
            new ActionId("training"),
            "Training",
            1,
            2,
            [new ModifyStatsEffectDefinition(buffTarget, 2, 2)]);
        var actions = new ActionCatalog([action]);
        var power = new PowerDefinition(
            new PowerId("boost"),
            "Boost",
            cost: 0,
            maxUsesPerTurn: 1,
            effects: [new ModifyStatsEffectDefinition(buffTarget, 1, 1)]);
        var powers = new PowerCatalog([power]);
        var leader = new LeaderDefinition(new LeaderId("leader"), "Leader", 0, 0, power.Id);
        var leaders = new LeaderCatalog([leader]);
        var rules = new PreparationRules(
            5, 0, 10, 3, 1, 1, 7, 10, 2,
            offerSizesByTier: [2, 2],
            initialUpgradeCostsByTier: [5],
            actionOfferSizesByTier: [1, 1]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 4)]);
        var random = new MinimumRandomSource();
        var engine = new MatchEngine(
            new MatchRules(2, 2),
            rules,
            new CombatRules(StartingSidePolicy.Random),
            pool,
            random,
            unitCatalog: units,
            leaderCatalog: leaders,
            powerCatalog: powers,
            actionCatalog: actions);
        var match = engine.CreateMatch(
        [
            new PlayerSetup(new PlayerId(0), leader.Id),
            new PlayerSetup(new PlayerId(1), leader.Id),
        ]);
        engine.BeginMatch(match);
        var agent = new PreparationAiAgent(rules, random, leaders, powers, units.Combines);

        agent.PlayPreparation(engine, match, new PlayerId(0));

        var player = match.Players[0];
        var fieldUnit = Assert.Single(player.Field);
        Assert.Equal(4, fieldUnit.Attack);
        Assert.Equal(4, fieldUnit.Health);
        Assert.Empty(player.ActionReserve);
        Assert.Equal(1, player.Leader!.GetUsesThisTurn(power.Id));
        Assert.True(player.IsReadyForCombat);
    }

    [Fact]
    public void PlayPreparation_PersonalitiesChooseDifferentEconomicLinesFromSameState()
    {
        var tempo = PlayEconomicPersonality(PreparationAiPersonality.Tempo);
        var greedy = PlayEconomicPersonality(PreparationAiPersonality.Greedy);
        var roller = PlayEconomicPersonality(PreparationAiPersonality.Roller);

        Assert.Equal(1, tempo.Tier);
        Assert.Single(tempo.Field);
        Assert.Equal(1, tempo.Resource);

        Assert.Equal(2, greedy.Tier);
        Assert.Empty(greedy.Field);
        Assert.Equal(0, greedy.Resource);

        Assert.Equal(1, roller.Tier);
        Assert.Empty(roller.Field);
        Assert.Equal(2, roller.Resource);
    }

    private static PlayerState PlayEconomicPersonality(PreparationAiPersonality personality)
    {
        var unit = new UnitDefinition(new UnitId("worker"), "Worker", 1, 2, 2);
        var units = new UnitCatalog([unit]);
        var rules = new PreparationRules(
            startingResource: 5,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [1, 1],
            initialUpgradeCostsByTier: [5]);
        var random = new MinimumRandomSource();
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 8)]);
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

        agent.PlayPreparation(engine, match, new PlayerId(0), personality: personality);

        return match.Players[0];
    }

    private static PreparationRules CreateRules() =>
        new(3, 1, 10, 3, 1, 1, 7, 10, 2, [3, 4], [5]);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
