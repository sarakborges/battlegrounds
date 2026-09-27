using Battlegrounds.AI;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.AI.Tests;

public sealed class PreparationAiCommandObserverTests
{
    [Fact]
    public void PlayPreparation_ObservesEveryAcceptedCommandWithoutChangingCommandCounts()
    {
        var unit = new UnitDefinition(new UnitId("worker"), "Worker", 1, 2, 2);
        var units = new UnitCatalog([unit]);
        var rules = new PreparationRules(
            startingResource: 3,
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
        var observer = new RecordingObserver();
        var agent = new PreparationAiAgent(rules, random);

        var result = agent.PlayPreparation(
            engine,
            match,
            new PlayerId(0),
            observer: observer);

        Assert.Equal(result.CommandsExecuted, observer.BeforeCount);
        Assert.Equal(result.CommandsExecuted, observer.AcceptedCount);
        Assert.Equal(result.CommandCounts.Total, observer.AcceptedCount);
        Assert.Contains(nameof(AcquirePlayableCommand), observer.BeforeCommandTypes);
        Assert.Contains(nameof(AcquirePlayableCommand), observer.AcceptedCommandTypes);
        Assert.Equal(observer.BeforeCommandTypes, observer.AcceptedCommandTypes);
    }

    private sealed class RecordingObserver : IPreparationAiCommandObserver
    {
        public int BeforeCount { get; private set; }
        public int AcceptedCount { get; private set; }
        public List<string> BeforeCommandTypes { get; } = [];
        public List<string> AcceptedCommandTypes { get; } = [];

        public void BeforeCommand(PlayerState player, IPreparationCommand command)
        {
            BeforeCount++;
            BeforeCommandTypes.Add(command.GetType().Name);
        }

        public void AfterAcceptedCommand(PlayerState player, IPreparationCommand command)
        {
            AcceptedCount++;
            AcceptedCommandTypes.Add(command.GetType().Name);
        }
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
