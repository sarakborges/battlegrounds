using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Match;

public readonly record struct CombatPairing(PlayerId LeftPlayerId, PlayerId RightPlayerId);

public sealed record CombatSettlement(
    PlayerId LeftPlayerId,
    PlayerId RightPlayerId,
    CombatResult CombatResult,
    PlayerId? WinnerPlayerId,
    PlayerId? DamagedPlayerId,
    int PlayerDamage,
    int? DamagedPlayerHealthAfter);

public sealed class CombatRoundResult
{
    private readonly ReadOnlyCollection<CombatSettlement> _settlements;

    public IReadOnlyList<CombatSettlement> Settlements => _settlements;
    public bool MatchFinished { get; }
    public PlayerId? WinnerPlayerId { get; }

    internal CombatRoundResult(
        IEnumerable<CombatSettlement> settlements,
        bool matchFinished,
        PlayerId? winnerPlayerId)
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

    public MatchEngine(
        MatchRules matchRules,
        PreparationRules preparationRules,
        CombatRules combatRules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog = null,
        BehaviorCatalog? behaviorCatalog = null)
    {
        _matchRules = matchRules ?? throw new ArgumentNullException(nameof(matchRules));
        ArgumentNullException.ThrowIfNull(preparationRules);
        _combatRules = combatRules ?? throw new ArgumentNullException(nameof(combatRules));
        ArgumentNullException.ThrowIfNull(unitPool);
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));

        _preparationEngine = new PreparationEngine(
            preparationRules,
            unitPool,
            randomSource,
            unitCatalog,
            behaviorCatalog);

        _combatEngine = unitCatalog is not null && behaviorCatalog is not null
            ? new CombatEngine(preparationRules.FieldCapacity, unitCatalog, behaviorCatalog)
            : new CombatEngine();
    }

    public MatchState CreateMatch(IEnumerable<PlayerId> playerIds) =>
        MatchState.Create(playerIds, _matchRules);

    public void BeginMatch(MatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (match.Phase != MatchPhase.Setup)
        {
            throw new InvalidOperationException($"Cannot begin match from {match.Phase}.");
        }

        _preparationEngine.BeginPreparation(match);
    }

    public PreparationCommandResult ExecutePreparation(
        MatchState match,
        IPreparationCommand command) =>
        _preparationEngine.Execute(match, command);

    public CombatRoundResult ResolveCombatRound(
        MatchState match,
        IEnumerable<CombatPairing> pairings)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(pairings);

        if (match.Phase != MatchPhase.Combat)
        {
            throw new InvalidOperationException($"Cannot resolve combat round from {match.Phase}.");
        }

        var activePlayers = match.Players.Where(player => !player.IsEliminated).ToArray();
        var materializedPairings = pairings.ToArray();
        ValidatePairings(activePlayers.Select(player => player.Id).ToArray(), materializedPairings);

        var resourceAdjustments = new Dictionary<PlayerId, int>();
        var settlements = new List<CombatSettlement>(materializedPairings.Length);

        foreach (var pairing in materializedPairings)
        {
            var left = GetActivePlayer(match, pairing.LeftPlayerId);
            var right = GetActivePlayer(match, pairing.RightPlayerId);
            var input = CombatInput.FromFields(left.Id, left.Field, right.Id, right.Field);
            var combatResult = _combatEngine.Resolve(input, _combatRules, _randomSource);

            foreach (var delta in combatResult.ResourceDeltas)
            {
                resourceAdjustments[delta.Key] = resourceAdjustments.GetValueOrDefault(delta.Key) + delta.Value;
            }

            settlements.Add(SettleCombat(left, right, input, combatResult));
        }

        if (match.ActivePlayerCount <= 1)
        {
            match.Finish();
            match.MarkChanged();
            return new CombatRoundResult(settlements, matchFinished: true, match.WinnerPlayerId);
        }

        _preparationEngine.BeginPreparation(match, resourceAdjustments);
        return new CombatRoundResult(settlements, matchFinished: false, winnerPlayerId: null);
    }

    private CombatSettlement SettleCombat(
        PlayerState left,
        PlayerState right,
        CombatInput input,
        CombatResult result)
    {
        if (result.IsDraw)
        {
            return new CombatSettlement(
                left.Id,
                right.Id,
                result,
                WinnerPlayerId: null,
                DamagedPlayerId: null,
                PlayerDamage: 0,
                DamagedPlayerHealthAfter: null);
        }

        var winnerId = result.WinnerPlayerId!.Value;
        var winner = winnerId == left.Id
            ? left
            : winnerId == right.Id
                ? right
                : throw new InvalidOperationException("Combat winner is not one of the paired players.");
        var loser = winner.Id == left.Id ? right : left;
        var damage = CalculatePostCombatDamage(winner, input, result);
        loser.TakeDamage(damage);

        return new CombatSettlement(
            left.Id,
            right.Id,
            result,
            winner.Id,
            loser.Id,
            damage,
            loser.Health);
    }

    private int CalculatePostCombatDamage(
        PlayerState winner,
        CombatInput input,
        CombatResult result)
    {
        return _combatRules.PostCombatDamagePolicy switch
        {
            PostCombatDamagePolicy.WinnerTierPlusSurvivorTiers =>
                winner.Tier + SumSurvivorTiers(winner.Id, input, result),
            _ => throw new ArgumentOutOfRangeException(
                nameof(_combatRules.PostCombatDamagePolicy),
                _combatRules.PostCombatDamagePolicy,
                "Unsupported post-combat damage policy."),
        };
    }

    private static int SumSurvivorTiers(
        PlayerId winnerId,
        CombatInput input,
        CombatResult result)
    {
        var participant = winnerId == input.Left.PlayerId
            ? input.Left
            : winnerId == input.Right.PlayerId
                ? input.Right
                : throw new InvalidOperationException("Combat winner is not in the combat input.");
        var survivors = winnerId == input.Left.PlayerId
            ? result.LeftSurvivors
            : result.RightSurvivors;
        var startingTiers = participant.Units.ToDictionary(unit => unit.InstanceId, unit => unit.Tier);

        return survivors.Sum(survivor =>
            startingTiers.TryGetValue(survivor.InstanceId, out var tier)
                ? tier
                : survivor.Tier);
    }

    private static PlayerState GetActivePlayer(MatchState match, PlayerId playerId)
    {
        if (!match.TryGetPlayer(playerId, out var player))
        {
            throw new ArgumentException($"Unknown player '{playerId}'.", nameof(playerId));
        }

        if (player.IsEliminated)
        {
            throw new ArgumentException($"Player '{playerId}' is eliminated.", nameof(playerId));
        }

        return player;
    }

    private static void ValidatePairings(
        IReadOnlyList<PlayerId> activePlayerIds,
        IReadOnlyList<CombatPairing> pairings)
    {
        if (activePlayerIds.Count % 2 != 0)
        {
            throw new InvalidOperationException(
                "An odd active-player count requires a ghost-opponent assignment, which is not implemented yet.");
        }

        if (pairings.Count * 2 != activePlayerIds.Count)
        {
            throw new ArgumentException("Combat pairings must cover every active player exactly once.", nameof(pairings));
        }

        var active = activePlayerIds.ToHashSet();
        var paired = new HashSet<PlayerId>();
        foreach (var pairing in pairings)
        {
            if (pairing.LeftPlayerId == pairing.RightPlayerId)
            {
                throw new ArgumentException("A player cannot be paired against itself.", nameof(pairings));
            }

            if (!active.Contains(pairing.LeftPlayerId) || !active.Contains(pairing.RightPlayerId))
            {
                throw new ArgumentException("Combat pairings may only contain active match players.", nameof(pairings));
            }

            if (!paired.Add(pairing.LeftPlayerId) || !paired.Add(pairing.RightPlayerId))
            {
                throw new ArgumentException("An active player cannot appear in more than one combat pairing.", nameof(pairings));
            }
        }
    }
}
