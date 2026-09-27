using Battlegrounds.AI;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Application;

public sealed record SessionAdvanceResult(
    int AiPreparationsCompleted,
    IReadOnlyList<CombatPairing> Pairings,
    CombatRoundResult? CombatRound,
    MatchPhase Phase,
    int Round);

/// <summary>
/// Framework-free orchestration for one human player against AI-controlled opponents.
/// Authoritative mutation remains in Core: this type only coordinates existing public boundaries.
/// </summary>
public sealed class SinglePlayerSession
{
    private readonly IRandomSource _randomSource;
    private readonly ICombatPairingPolicy _pairingPolicy;
    private readonly PreparationAiAgent _aiAgent;
    private readonly MatchEngine _matchEngine;
    private readonly PlayerId[] _aiPlayerIds;
    private readonly int _aiMaximumCommands;
    private PlayerId[] _preparationInitiative = [];
    private int _preparationInitiativeRound;
    private long _combatResolutionSequence;

    public ModPackage Mod { get; }
    public PlayerId HumanPlayerId { get; }
    public IReadOnlyList<PlayerId> AiPlayerIds => _aiPlayerIds;
    public IReadOnlyList<PlayerId> PreparationInitiative => _preparationInitiative;
    public LeaderSelectionState LeaderSelection { get; }
    public MatchState? Match { get; private set; }
    public SessionCombatRecord? LastCombat { get; private set; }
    public bool HasStarted => Match is not null;
    public PlayerId? CurrentPreparationPlayerId =>
        Match is { Phase: MatchPhase.Preparation } match && _preparationInitiativeRound == match.Round
            ? FindCurrentPreparationPlayer(match)
            : null;

    private SinglePlayerSession(
        ModPackage mod,
        PlayerId humanPlayerId,
        PlayerId[] aiPlayerIds,
        IRandomSource randomSource,
        ICombatPairingPolicy pairingPolicy,
        int aiMaximumCommands)
    {
        Mod = mod;
        HumanPlayerId = humanPlayerId;
        _aiPlayerIds = aiPlayerIds;
        _randomSource = randomSource;
        _pairingPolicy = pairingPolicy;
        _aiMaximumCommands = aiMaximumCommands;
        _matchEngine = mod.CreateMatchEngine(randomSource);
        _aiAgent = new PreparationAiAgent(
            mod.PreparationRules,
            randomSource,
            mod.Leaders,
            mod.Powers,
            mod.Combines);

        var playerIds = new[] { humanPlayerId }
            .Concat(aiPlayerIds)
            .OrderBy(id => id.Value)
            .ToArray();
        LeaderSelection = mod.CreateLeaderSelection(playerIds, randomSource);

        foreach (var aiPlayerId in aiPlayerIds.OrderBy(id => id.Value))
            _aiAgent.SelectLeader(LeaderSelection, aiPlayerId);
    }

    public static SinglePlayerSession Create(
        ModPackage mod,
        PlayerId humanPlayerId,
        IEnumerable<PlayerId> aiPlayerIds,
        IRandomSource randomSource,
        ICombatPairingPolicy? pairingPolicy = null,
        int aiMaximumCommands = 128)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ArgumentNullException.ThrowIfNull(aiPlayerIds);
        ArgumentNullException.ThrowIfNull(randomSource);
        if (aiMaximumCommands <= 0) throw new ArgumentOutOfRangeException(nameof(aiMaximumCommands));

        var aiPlayers = aiPlayerIds.ToArray();
        if (aiPlayers.Length == 0)
            throw new ArgumentException("A single-player session requires at least one AI opponent.", nameof(aiPlayerIds));
        if (aiPlayers.Contains(humanPlayerId))
            throw new ArgumentException("The human player id cannot also be AI-controlled.", nameof(aiPlayerIds));
        if (aiPlayers.Distinct().Count() != aiPlayers.Length)
            throw new ArgumentException("AI player ids must be unique.", nameof(aiPlayerIds));

        var participantCount = aiPlayers.Length + 1;
        if (participantCount % 2 != 0)
        {
            throw new ArgumentException(
                "The current combat contract requires an even initial participant count because no eliminated-opponent snapshot exists in round one.",
                nameof(aiPlayerIds));
        }

        return new SinglePlayerSession(
            mod,
            humanPlayerId,
            aiPlayers,
            randomSource,
            pairingPolicy ?? new HistoryAwareCombatPairingPolicy(),
            aiMaximumCommands);
    }

    public static SinglePlayerSession Create(
        ModPackage mod,
        PlayerId humanPlayerId,
        IEnumerable<PlayerId> aiPlayerIds,
        int seed,
        ICombatPairingPolicy? pairingPolicy = null,
        int aiMaximumCommands = 128) =>
        Create(
            mod,
            humanPlayerId,
            aiPlayerIds,
            new SeededRandomSource(seed),
            pairingPolicy,
            aiMaximumCommands);

    public LeaderSelectionResult SelectHumanLeader(LeaderId leaderId)
    {
        if (Match is not null)
            throw new InvalidOperationException("Leader selection is already complete and the match has started.");

        var result = LeaderSelection.Select(HumanPlayerId, leaderId);
        if (!result.Succeeded) return result;

        if (!LeaderSelection.IsComplete)
            throw new InvalidOperationException("AI leader selection must be complete before the human selection finishes setup.");

        Match = _matchEngine.CreateMatch(LeaderSelection.GetCompletedPlayerSetups());
        _matchEngine.BeginMatch(Match);
        EnsurePreparationInitiative(Match);
        return result;
    }

    public PreparationCommandResult ExecuteHumanPreparation(IPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var match = GetStartedMatch();
        if (command.PlayerId != HumanPlayerId)
            throw new ArgumentException("The human command boundary cannot submit commands for an AI-controlled player.", nameof(command));

        if (match.Phase == MatchPhase.Preparation)
        {
            EnsurePreparationInitiative(match);
            var currentPlayerId = FindCurrentPreparationPlayer(match)
                ?? throw new InvalidOperationException("Preparation has no active initiative owner.");
            if (currentPlayerId != HumanPlayerId)
            {
                throw new InvalidOperationException(
                    $"Player '{currentPlayerId}' currently owns Preparation initiative; human commands must wait for the human initiative turn.");
            }
        }

        return _matchEngine.ExecutePreparation(match, command);
    }

    /// <summary>
    /// Advances consecutive AI-owned Preparation turns in the current round and stops as soon as initiative
    /// reaches the human. If every active player is ready, resolves exactly one combat round and stops at the
    /// next Preparation or Finished state.
    /// </summary>
    public SessionAdvanceResult AdvanceAutomated()
    {
        var match = GetStartedMatch();
        if (match.Phase == MatchPhase.Finished)
            return CreateAdvanceResult(match, 0, [], null);

        var aiPreparationsCompleted = 0;
        if (match.Phase == MatchPhase.Preparation)
        {
            EnsurePreparationInitiative(match);
            while (match.Phase == MatchPhase.Preparation)
            {
                var currentPlayerId = FindCurrentPreparationPlayer(match)
                    ?? throw new InvalidOperationException("Preparation has no active initiative owner.");
                if (currentPlayerId == HumanPlayerId)
                    break;
                if (!_aiPlayerIds.Contains(currentPlayerId))
                    throw new InvalidOperationException($"Preparation initiative references uncontrolled player '{currentPlayerId}'.");

                _aiAgent.PlayPreparation(_matchEngine, match, currentPlayerId, _aiMaximumCommands);
                aiPreparationsCompleted++;
            }
        }

        if (match.Phase != MatchPhase.Combat)
            return CreateAdvanceResult(match, aiPreparationsCompleted, [], null);

        var pairings = _pairingPolicy.CreatePairings(match, _randomSource);
        var combatRoundNumber = match.Round;
        var startingUnits = CaptureStartingCombatUnits(match, pairings);
        var combatRound = _matchEngine.ResolveCombatRound(match, pairings);
        LastCombat = new SessionCombatRecord(
            ++_combatResolutionSequence,
            combatRoundNumber,
            pairings,
            startingUnits,
            combatRound);

        if (match.Phase == MatchPhase.Preparation)
            EnsurePreparationInitiative(match);

        return CreateAdvanceResult(match, aiPreparationsCompleted, pairings, combatRound);
    }

    private void EnsurePreparationInitiative(MatchState match)
    {
        if (match.Phase != MatchPhase.Preparation)
            return;
        if (_preparationInitiativeRound == match.Round && _preparationInitiative.Length > 0)
            return;

        var initiative = match.Players
            .Where(player => !player.IsEliminated)
            .Select(player => player.Id)
            .ToArray();

        for (var index = initiative.Length - 1; index > 0; index--)
        {
            var swapIndex = _randomSource.NextInt(0, index + 1);
            (initiative[index], initiative[swapIndex]) = (initiative[swapIndex], initiative[index]);
        }

        _preparationInitiative = initiative;
        _preparationInitiativeRound = match.Round;
    }

    private PlayerId? FindCurrentPreparationPlayer(MatchState match)
    {
        foreach (var playerId in _preparationInitiative)
        {
            if (!match.TryGetPlayer(playerId, out var player) || player.IsEliminated || player.IsReadyForCombat)
                continue;
            return playerId;
        }

        return null;
    }

    private SessionCombatUnitSnapshot[] CaptureStartingCombatUnits(
        MatchState match,
        IReadOnlyList<CombatPairing> pairings)
    {
        var units = new List<SessionCombatUnitSnapshot>();
        var livePlayerIds = pairings
            .SelectMany(pairing => pairing.RightPlayerId is PlayerId right
                ? new[] { pairing.LeftPlayerId, right }
                : new[] { pairing.LeftPlayerId })
            .Distinct()
            .ToArray();

        foreach (var playerId in livePlayerIds)
        {
            if (!match.TryGetPlayer(playerId, out var player))
                throw new InvalidOperationException($"Combat player '{playerId}' is missing from the match.");

            units.AddRange(player.Field.Select(unit => new SessionCombatUnitSnapshot(
                player.Id,
                unit.Id,
                unit.Definition.Id,
                unit.Definition.Name,
                unit.Definition.Tier,
                unit.Attack,
                unit.Health)));
        }

        if (pairings.Any(pairing => pairing.UsesEliminatedOpponent))
        {
            var archived = match.LatestEliminatedOpponent
                ?? throw new InvalidOperationException("Eliminated-opponent pairing has no archived opponent snapshot.");
            units.AddRange(archived.Participant.Units.Select(unit => new SessionCombatUnitSnapshot(
                archived.SourcePlayerId,
                unit.InstanceId,
                unit.UnitId,
                unit.Definition?.Name ?? Mod.Units.GetRequired(unit.UnitId).Name,
                unit.Tier,
                unit.Attack,
                unit.Health)));
        }

        return units.ToArray();
    }

    private MatchState GetStartedMatch() =>
        Match ?? throw new InvalidOperationException("The match has not started; select the human Leader first.");

    private static SessionAdvanceResult CreateAdvanceResult(
        MatchState match,
        int aiPreparationsCompleted,
        IReadOnlyList<CombatPairing> pairings,
        CombatRoundResult? combatRound) =>
        new(aiPreparationsCompleted, pairings, combatRound, match.Phase, match.Round);
}