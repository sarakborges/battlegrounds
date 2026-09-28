using Battlegrounds.AI;
using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Application.Tests;

public sealed class SinglePlayerSessionTests
{
    [Fact]
    public void Create_AutoSelectsAiLeaderAndWaitsForHumanSelection()
    {
        var mod = LoadExampleMod();
        var human = new PlayerId(0);
        var ai = new PlayerId(1);

        var session = SinglePlayerSession.Create(mod, human, [ai], seed: 17);

        Assert.False(session.HasStarted);
        Assert.Null(session.Match);
        Assert.Equal(1, session.LeaderSelection.SelectedCount);
        Assert.True(session.LeaderSelection.TryGetSelection(ai, out _));
        Assert.False(session.LeaderSelection.TryGetSelection(human, out _));
        Assert.True(session.AiPersonalities.ContainsKey(ai));

        var humanLeader = session.LeaderSelection.GetOffer(human)[0];
        var result = session.SelectHumanLeader(humanLeader);

        Assert.True(result.Succeeded);
        Assert.True(session.HasStarted);
        Assert.NotNull(session.Match);
        Assert.Equal(MatchPhase.Preparation, session.Match!.Phase);
        Assert.Equal(1, session.Match.Round);
        Assert.True(session.LeaderSelection.IsComplete);
        Assert.Equal(2, session.PreparationInitiative.Count);
        Assert.Contains(human, session.PreparationInitiative);
        Assert.Contains(ai, session.PreparationInitiative);
        Assert.Equal(session.PreparationInitiative[0], session.CurrentPreparationPlayerId);
    }

    [Fact]
    public void Create_AssignsAiPersonalityDeterministicallyFromSessionRng()
    {
        var minimum = CreateStartedSession(new MinimumRandomSource());
        var maximum = CreateStartedSession(new MaximumRandomSource());
        var minimumAi = Assert.Single(minimum.AiPlayerIds);
        var maximumAi = Assert.Single(maximum.AiPlayerIds);
        Assert.Equal(PreparationAiPersonality.Tempo, minimum.AiPersonalities[minimumAi]);
        Assert.Equal(PreparationAiPersonality.Roller, maximum.AiPersonalities[maximumAi]);
    }

    [Fact]
    public void ExecuteHumanPreparation_RejectsCommandsForAiPlayers()
    {
        var session = CreateStartedSession(new MaximumRandomSource());
        var ai = Assert.Single(session.AiPlayerIds);
        Assert.Throws<ArgumentException>(() => session.ExecuteHumanPreparation(new EndPreparationCommand(ai)));
    }

    [Fact]
    public void AdvanceAutomated_WhenHumanHasInitiative_WaitsWithoutRunningAi()
    {
        var session = CreateStartedSession(new MaximumRandomSource());
        var human = session.HumanPlayerId;
        var ai = Assert.Single(session.AiPlayerIds);
        Assert.Equal(new[] { human, ai }, session.PreparationInitiative);
        var advance = session.AdvanceAutomated();
        Assert.Equal(0, advance.AiPreparationsCompleted);
        Assert.Null(advance.CombatRound);
        Assert.Empty(advance.Pairings);
        Assert.Equal(MatchPhase.Preparation, advance.Phase);
        Assert.Equal(human, session.CurrentPreparationPlayerId);
        Assert.False(session.Match!.Players.Single(player => player.Id == ai).IsReadyForCombat);
    }

    [Fact]
    public void AdvanceAutomated_WhenAiHasInitiative_CompletesAiThenStopsAtHuman()
    {
        var session = CreateStartedSession(new MinimumRandomSource());
        var human = session.HumanPlayerId;
        var ai = Assert.Single(session.AiPlayerIds);
        Assert.Equal(new[] { ai, human }, session.PreparationInitiative);
        var advance = session.AdvanceAutomated();
        Assert.Equal(1, advance.AiPreparationsCompleted);
        Assert.Null(advance.CombatRound);
        Assert.Empty(advance.Pairings);
        Assert.Equal(MatchPhase.Preparation, advance.Phase);
        Assert.Equal(human, session.CurrentPreparationPlayerId);
        Assert.False(session.Match!.Players.Single(player => player.Id == human).IsReadyForCombat);
        Assert.True(session.Match.Players.Single(player => player.Id == ai).IsReadyForCombat);
    }

    [Fact]
    public void ExecuteHumanPreparation_RejectsHumanBeforeItsInitiativeTurn()
    {
        var session = CreateStartedSession(new MinimumRandomSource());
        var human = session.HumanPlayerId;
        var ai = Assert.Single(session.AiPlayerIds);
        Assert.Equal(ai, session.CurrentPreparationPlayerId);
        Assert.Throws<InvalidOperationException>(() => session.ExecuteHumanPreparation(new EndPreparationCommand(human)));
        session.AdvanceAutomated();
        Assert.Equal(human, session.CurrentPreparationPlayerId);
        var humanEnd = session.ExecuteHumanPreparation(new EndPreparationCommand(human));
        Assert.True(humanEnd.Succeeded);
        Assert.Equal(MatchPhase.Combat, session.Match!.Phase);
    }

    [Fact]
    public void AdvanceAutomated_AfterHumanEnds_RunsRemainingAiAndResolvesOneCombatRound()
    {
        var session = CreateStartedSession(new MaximumRandomSource());
        var human = session.HumanPlayerId;
        var ai = Assert.Single(session.AiPlayerIds);
        Assert.Equal(new[] { human, ai }, session.PreparationInitiative);
        var humanEnd = session.ExecuteHumanPreparation(new EndPreparationCommand(human));
        Assert.True(humanEnd.Succeeded);
        Assert.Equal(MatchPhase.Preparation, session.Match!.Phase);
        Assert.Equal(ai, session.CurrentPreparationPlayerId);
        var advance = session.AdvanceAutomated();
        Assert.Equal(1, advance.AiPreparationsCompleted);
        Assert.NotNull(advance.CombatRound);
        Assert.Single(advance.Pairings);
        Assert.Single(session.Match.CombatPairingHistory);
        Assert.Equal(2, session.Match.Round);
        Assert.Equal(MatchPhase.Preparation, session.Match.Phase);
        Assert.Equal(MatchPhase.Preparation, advance.Phase);
        Assert.Equal(2, session.PreparationInitiative.Count);
        Assert.Equal(session.PreparationInitiative[0], session.CurrentPreparationPlayerId);
    }

    [Fact]
    public void Create_AcceptsOneHumanWithTwoAiPlayers()
    {
        var mod = LoadExampleMod();
        var session = SinglePlayerSession.Create(
            mod,
            new PlayerId(0),
            [new PlayerId(1), new PlayerId(2)],
            seed: 7);

        Assert.Equal(2, session.AiPlayerIds.Count);
        Assert.Equal(2, session.LeaderSelection.SelectedCount);
        Assert.False(session.HasStarted);
    }

    private static SinglePlayerSession CreateStartedSession(IRandomSource? randomSource = null)
    {
        var mod = LoadExampleMod();
        var session = SinglePlayerSession.Create(
            mod,
            new PlayerId(0),
            [new PlayerId(1)],
            randomSource ?? new SeededRandomSource(12345));
        var humanLeader = session.LeaderSelection.GetOffer(session.HumanPlayerId)[0];
        var result = session.SelectHumanLeader(humanLeader);
        Assert.True(result.Succeeded);
        return session;
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }

    private sealed class MaximumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => maxExclusive - 1;
    }

    private static ModPackage LoadExampleMod() => new ModLoader().Load(FindExampleModDirectory());

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
