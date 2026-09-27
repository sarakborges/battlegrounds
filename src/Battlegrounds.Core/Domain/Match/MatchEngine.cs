using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Match;

public readonly record struct CombatPairing
{
    public PlayerId LeftPlayerId { get; }
    public PlayerId? RightPlayerId { get; }
    public bool UsesEliminatedOpponent => RightPlayerId is null;

    public CombatPairing(PlayerId leftPlayerId, PlayerId rightPlayerId)
    {
        if (leftPlayerId == rightPlayerId) throw new ArgumentException("A player cannot be paired against itself.", nameof(rightPlayerId));
        LeftPlayerId = leftPlayerId;
        RightPlayerId = rightPlayerId;
    }

    private CombatPairing(PlayerId playerId)
    {
        LeftPlayerId = playerId;
        RightPlayerId = null;
    }

    public static CombatPairing VersusEliminatedOpponent(PlayerId playerId) => new(playerId);
}

public sealed record CombatSettlement(
    PlayerId LeftPlayerId,
    PlayerId? RightPlayerId,
    PlayerId? EliminatedOpponentSourcePlayerId,
    CombatResult CombatResult,
    PlayerId? WinnerPlayerId,
    bool EliminatedOpponentWon,
    PlayerId? DamagedPlayerId,
    int PlayerDamage,
    int ArmorAbsorbed,
    int? DamagedPlayerArmorAfter,
    int? DamagedPlayerHealthAfter)
{
    public bool UsesEliminatedOpponent => EliminatedOpponentSourcePlayerId is not null;
}

public sealed class CombatRoundResult
{
    private readonly ReadOnlyCollection<CombatSettlement> _settlements;
    public IReadOnlyList<CombatSettlement> Settlements => _settlements;
    public bool MatchFinished { get; }
    public PlayerId? WinnerPlayerId { get; }

    internal CombatRoundResult(IEnumerable<CombatSettlement> settlements, bool matchFinished, PlayerId? winnerPlayerId)
    {
        _settlements = Array.AsReadOnly(settlements.ToArray());
        MatchFinished = matchFinished;
        WinnerPlayerId = winnerPlayerId;
    }
}

public sealed class MatchEngine
{
    private readonly MatchRules _matchRules;
    private readonly CombatRules _combatRules;
    private readonly IRandomSource _randomSource;
    private readonly PreparationEngine _preparationEngine;
    private readonly CombatEngine _combatEngine;
    private readonly LeaderCatalog? _leaderCatalog;

    public MatchEngine(
        MatchRules matchRules,
        PreparationRules preparationRules,
        CombatRules combatRules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog = null,
        BehaviorCatalog? behaviorCatalog = null,
        LeaderCatalog? leaderCatalog = null,
        PowerCatalog? powerCatalog = null,
        ActionCatalog? actionCatalog = null)
    {
        _matchRules = matchRules ?? throw new ArgumentNullException(nameof(matchRules));
        ArgumentNullException.ThrowIfNull(preparationRules);
        _combatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
        ArgumentNullException.ThrowIfNull(unitPool);
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _leaderCatalog = leaderCatalog;

        _preparationEngine = new PreparationEngine(
            preparationRules,
            unitPool,
            randomSource,
            unitCatalog,
            behaviorCatalog,
            powerCatalog,
            actionCatalog);

        _combatEngine = unitCatalog is not null && behaviorCatalog is not null
            ? new CombatEngine(preparationRules.FieldCapacity, unitCatalog, behaviorCatalog, powerCatalog)
            : new CombatEngine();
    }

    public MatchState CreateMatch(IEnumerable<PlayerId> playerIds)
    {
        if (_leaderCatalog is not null)
            throw new InvalidOperationException("This match engine has a leader catalog; create the match with PlayerSetup values so every player selects a leader.");
        return MatchState.Create(playerIds, _matchRules);
    }

    public MatchState CreateMatch(IEnumerable<PlayerSetup> playerSetups)
    {
        if (_leaderCatalog is null) throw new InvalidOperationException("This match engine has no leader catalog.");
        return MatchState.Create(playerSetups, _matchRules, _leaderCatalog);
    }

    public void BeginMatch(MatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (match.Phase != MatchPhase.Setup) throw new InvalidOperationException($"Cannot begin match from {match.Phase}.");
        _preparationEngine.BeginPreparation(match);
    }

    public PreparationCommandResult ExecutePreparation(MatchState match, IPreparationCommand command) =>
        _preparationEngine.Execute(match, command);

    public CombatRoundResult ResolveCombatRound(MatchState match, IEnumerable<CombatPairing> pairings)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(pairings);
        if (match.Phase != MatchPhase.Combat) throw new InvalidOperationException($"Cannot resolve combat round from {match.Phase}.");

        var activePlayers = match.Players.Where(player => !player.IsEliminated).ToArray();
        var healthBeforeCombat = activePlayers.ToDictionary(player => player.Id, player => player.Health);
        var eliminatedOpponent = match.LatestEliminatedOpponent;
        var materializedPairings = pairings.ToArray();
        ValidatePairings(activePlayers.Select(player => player.Id).ToArray(), materializedPairings, eliminatedOpponent);

        var resourceAdjustments = new Dictionary<PlayerId, int>();
        var powerChanges = new Dictionary<PlayerId, PowerId>();
        var settlements = new List<CombatSettlement>(materializedPairings.Length);
        var newlyEliminated = new List<PlayerId>();

        foreach (var pairing in materializedPairings)
        {
            if (pairing.UsesEliminatedOpponent)
            {
                var player = GetActivePlayer(match, pairing.LeftPlayerId);
                var snapshot = eliminatedOpponent ?? throw new InvalidOperationException("No eliminated-opponent snapshot is available for this round.");
                var input = new CombatInput(
                    CombatParticipant.FromField(player.Id, player.Field, player.Leader?.CurrentPowerId, player.EffectHistory.Snapshot()),
                    snapshot.Participant);
                var combatResult = _combatEngine.Resolve(input, _combatRules, _randomSource);
                AccumulateResourceDeltas(combatResult, resourceAdjustments, [player.Id]);
                AccumulatePowerChanges(combatResult, powerChanges, [player.Id]);
                ApplyHistoryDeltas(match, combatResult, [player.Id]);
                settlements.Add(SettleAgainstEliminatedOpponent(player, snapshot, input, combatResult));
                if (player.IsEliminated) newlyEliminated.Add(player.Id);
                continue;
            }

            var rightPlayerId = pairing.RightPlayerId!.Value;
            var left = GetActivePlayer(match, pairing.LeftPlayerId);
            var right = GetActivePlayer(match, rightPlayerId);
            var liveInput = new CombatInput(
                CombatParticipant.FromField(left.Id, left.Field, left.Leader?.CurrentPowerId, left.EffectHistory.Snapshot()),
                CombatParticipant.FromField(right.Id, right.Field, right.Leader?.CurrentPowerId, right.EffectHistory.Snapshot()));
            var liveResult = _combatEngine.Resolve(liveInput, _combatRules, _randomSource);
            AccumulateResourceDeltas(liveResult, resourceAdjustments, [left.Id, right.Id]);
            AccumulatePowerChanges(liveResult, powerChanges, [left.Id, right.Id]);
            ApplyHistoryDeltas(match, liveResult, [left.Id, right.Id]);
            settlements.Add(SettleLiveCombat(left, right, liveInput, liveResult));
            if (left.IsEliminated) newlyEliminated.Add(left.Id);
            if (right.IsEliminated) newlyEliminated.Add(right.Id);
        }

        ApplyPowerChanges(match, powerChanges);
        match.RecordEliminations(newlyEliminated, healthBeforeCombat);
        if (match.ActivePlayerCount <= 1)
        {
            match.Finish();
            match.MarkChanged();
            return new CombatRoundResult(settlements, true, match.WinnerPlayerId);
        }

        _preparationEngine.BeginPreparation(match, resourceAdjustments);
        return new CombatRoundResult(settlements, false, null);
    }

    private CombatSettlement SettleLiveCombat(PlayerState left, PlayerState right, CombatInput input, CombatResult result)
    {
        if (result.IsDraw) return NoDamageSettlement(left.Id, right.Id, null, result, false, null);
        var winnerId = result.WinnerPlayerId!.Value;
        var winner = winnerId == left.Id ? left : winnerId == right.Id ? right : throw new InvalidOperationException("Combat winner is not one of the paired players.");
        var loser = winner.Id == left.Id ? right : left;
        var damage = CalculatePostCombatDamage(winner.Tier, winner.Id, input, result);
        var applied = loser.TakeDamage(damage);
        return new CombatSettlement(left.Id, right.Id, null, result, winner.Id, false, loser.Id, damage, applied.ArmorAbsorbed, applied.ArmorAfter, applied.HealthAfter);
    }

    private CombatSettlement SettleAgainstEliminatedOpponent(PlayerState player, EliminatedOpponentSnapshot opponent, CombatInput input, CombatResult result)
    {
        if (result.IsDraw) return NoDamageSettlement(player.Id, null, opponent.SourcePlayerId, result, false, null);
        var winnerId = result.WinnerPlayerId!.Value;
        if (winnerId == player.Id) return NoDamageSettlement(player.Id, null, opponent.SourcePlayerId, result, false, player.Id);
        if (winnerId != opponent.SourcePlayerId) throw new InvalidOperationException("Combat winner is not part of the eliminated-opponent combat.");
        var damage = CalculatePostCombatDamage(opponent.Tier, opponent.SourcePlayerId, input, result);
        var applied = player.TakeDamage(damage);
        return new CombatSettlement(player.Id, null, opponent.SourcePlayerId, result, null, true, player.Id, damage, applied.ArmorAbsorbed, applied.ArmorAfter, applied.HealthAfter);
    }

    private static CombatSettlement NoDamageSettlement(
        PlayerId leftPlayerId,
        PlayerId? rightPlayerId,
        PlayerId? eliminatedOpponentSourcePlayerId,
        CombatResult result,
        bool eliminatedOpponentWon,
        PlayerId? winnerPlayerId) =>
        new(leftPlayerId, rightPlayerId, eliminatedOpponentSourcePlayerId, result, winnerPlayerId, eliminatedOpponentWon, null, 0, 0, null, null);

    private int CalculatePostCombatDamage(int winnerTier, PlayerId winnerParticipantId, CombatInput input, CombatResult result) =>
        _combatRules.PostCombatDamagePolicy switch
        {
            PostCombatDamagePolicy.WinnerTierPlusSurvivorTiers => winnerTier + SumSurvivorTiers(winnerParticipantId, input, result),
            _ => throw new ArgumentOutOfRangeException(nameof(_combatRules.PostCombatDamagePolicy), _combatRules.PostCombatDamagePolicy, "Unsupported post-combat damage policy."),
        };

    private static int SumSurvivorTiers(PlayerId winnerId, CombatInput input, CombatResult result)
    {
        var participant = winnerId == input.Left.PlayerId ? input.Left : winnerId == input.Right.PlayerId ? input.Right : throw new InvalidOperationException("Combat winner is not in the combat input.");
        var survivors = winnerId == input.Left.PlayerId ? result.LeftSurvivors : result.RightSurvivors;
        var startingTiers = participant.Units.ToDictionary(unit => unit.InstanceId, unit => unit.Tier);
        return survivors.Sum(survivor => startingTiers.TryGetValue(survivor.InstanceId, out var tier) ? tier : survivor.Tier);
    }

    private static void AccumulateResourceDeltas(CombatResult result, Dictionary<PlayerId, int> adjustments, IReadOnlyCollection<PlayerId> allowedPlayers)
    {
        var allowed = allowedPlayers.ToHashSet();
        foreach (var delta in result.ResourceDeltas)
            if (allowed.Contains(delta.Key)) adjustments[delta.Key] = adjustments.GetValueOrDefault(delta.Key) + delta.Value;
    }

    private static void AccumulatePowerChanges(CombatResult result, Dictionary<PlayerId, PowerId> changes, IReadOnlyCollection<PlayerId> allowedPlayers)
    {
        var allowed = allowedPlayers.ToHashSet();
        foreach (var change in result.PowerChanges)
            if (allowed.Contains(change.Key)) changes[change.Key] = change.Value;
    }

    private static void ApplyHistoryDeltas(MatchState match, CombatResult result, IReadOnlyCollection<PlayerId> allowedPlayers)
    {
        var allowed = allowedPlayers.ToHashSet();
        foreach (var delta in result.HistoryDeltas)
        {
            if (!allowed.Contains(delta.Key)) continue;
            if (!match.TryGetPlayer(delta.Key, out var player)) throw new InvalidOperationException($"Cannot settle effect history for player '{delta.Key}'.");
            player.EffectHistory.ApplyCombatDelta(delta.Value);
        }
    }

    private static void ApplyPowerChanges(MatchState match, IReadOnlyDictionary<PlayerId, PowerId> changes)
    {
        foreach (var change in changes)
        {
            if (!match.TryGetPlayer(change.Key, out var player) || player.Leader is null)
                throw new InvalidOperationException($"Cannot settle power change for player '{change.Key}'.");
            player.Leader.SetPower(change.Value);
        }
    }

    private static PlayerState GetActivePlayer(MatchState match, PlayerId playerId)
    {
        if (!match.TryGetPlayer(playerId, out var player)) throw new ArgumentException($"Unknown player '{playerId}'.", nameof(playerId));
        if (player.IsEliminated) throw new ArgumentException($"Player '{playerId}' is eliminated.", nameof(playerId));
        return player;
    }

    private static void ValidatePairings(IReadOnlyList<PlayerId> activePlayerIds, IReadOnlyList<CombatPairing> pairings, EliminatedOpponentSnapshot? eliminatedOpponent)
    {
        var requiresEliminatedOpponent = activePlayerIds.Count % 2 != 0;
        var eliminatedOpponentPairingCount = pairings.Count(pairing => pairing.UsesEliminatedOpponent);
        if (requiresEliminatedOpponent && eliminatedOpponent is null)
            throw new InvalidOperationException("An odd active-player count requires a snapshot from a previously eliminated player.");
        var expectedEliminatedOpponentPairings = requiresEliminatedOpponent ? 1 : 0;
        if (eliminatedOpponentPairingCount != expectedEliminatedOpponentPairings)
            throw new ArgumentException(requiresEliminatedOpponent
                ? "Odd-player combat requires exactly one eliminated-opponent pairing."
                : "Eliminated-opponent pairing is only valid when the active-player count is odd.", nameof(pairings));
        var expectedPairingCount = (activePlayerIds.Count / 2) + expectedEliminatedOpponentPairings;
        if (pairings.Count != expectedPairingCount) throw new ArgumentException("Combat pairings must cover every active player exactly once.", nameof(pairings));

        var active = activePlayerIds.ToHashSet();
        var paired = new HashSet<PlayerId>();
        foreach (var pairing in pairings)
        {
            if (!active.Contains(pairing.LeftPlayerId)) throw new ArgumentException("Combat pairings may only contain active match players.", nameof(pairings));
            if (!paired.Add(pairing.LeftPlayerId)) throw new ArgumentException("An active player cannot appear in more than one combat pairing.", nameof(pairings));
            if (pairing.UsesEliminatedOpponent) continue;
            var rightPlayerId = pairing.RightPlayerId!.Value;
            if (!active.Contains(rightPlayerId)) throw new ArgumentException("Combat pairings may only contain active match players.", nameof(pairings));
            if (!paired.Add(rightPlayerId)) throw new ArgumentException("An active player cannot appear in more than one combat pairing.", nameof(pairings));
        }
        if (paired.Count != active.Count) throw new ArgumentException("Combat pairings must cover every active player exactly once.", nameof(pairings));
    }
}
