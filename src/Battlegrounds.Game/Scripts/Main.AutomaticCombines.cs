using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _automaticCombineSubmissionInProgress;
    private string? _automaticCombineFailureKey;

    /// <summary>
    /// Warbands presents combines as an automatic game rule instead of a manual player action.
    /// The Core command remains explicit: this presentation policy deterministically selects the
    /// exact instances and submits the same CombineUnitsCommand used by manual mods and AI.
    ///
    /// Other mods remain manual by default, so their existing Combine button and selection flow
    /// continue to work unchanged.
    /// </summary>
    private bool UsesAutomaticCombines =>
        _session is not null &&
        string.Equals(_session.Mod.Id, "warbands", StringComparison.Ordinal);

    public override void _Process(double delta)
    {
        UpdateCombineButtonVisibility();
        TrySubmitAutomaticCombine();
    }

    private void UpdateCombineButtonVisibility()
    {
        if (_combineButton is null)
            return;

        _combineButton.Visible = !UsesAutomaticCombines;
    }

    private void TrySubmitAutomaticCombine()
    {
        if (_automaticCombineSubmissionInProgress || !UsesAutomaticCombines || _session?.Match is not MatchState match)
            return;
        if (match.Phase != MatchPhase.Preparation || _session.CurrentPreparationPlayerId != _session.HumanPlayerId)
            return;
        if (!match.TryGetPlayer(_session.HumanPlayerId, out var human))
            return;
        if (human.IsEliminated || human.IsReadyForCombat || human.PendingChoice is not null || _interaction.IsActive)
            return;

        var plan = FindAutomaticCombine(human);
        if (plan is null)
        {
            _automaticCombineFailureKey = null;
            return;
        }

        var failureKey = BuildAutomaticCombineFailureKey(match, human, plan.Value.Definition, plan.Value.InstanceIds);
        if (string.Equals(_automaticCombineFailureKey, failureKey, StringComparison.Ordinal))
            return;

        _automaticCombineSubmissionInProgress = true;
        try
        {
            var command = new CombineUnitsCommand(human.Id, plan.Value.Definition.Id, plan.Value.InstanceIds);
            var result = SubmitHumanCommand(command, logFailure: false);
            if (result is { Succeeded: true })
            {
                _automaticCombineFailureKey = null;
                AppendLog(
                    $"Auto-combined {plan.Value.Definition.RequiredCopies}x {UnitName(plan.Value.Definition.SourceUnitId)} " +
                    $"into {UnitName(plan.Value.Definition.ResultUnitId)}.");
                Render();
                return;
            }

            _automaticCombineFailureKey = failureKey;
            if (result.HasValue)
                AppendLog($"Automatic combine rejected: {result.Value.FailureCode}.");
        }
        finally
        {
            _automaticCombineSubmissionInProgress = false;
        }
    }

    private (UnitCombineDefinition Definition, UnitInstanceId[] InstanceIds)? FindAutomaticCombine(PlayerState human)
    {
        if (_session is null)
            return null;

        foreach (var definition in _session.Mod.Combines.All.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            var reserveCandidates = human.Reserve
                .Where(unit => unit.Definition.Id == definition.SourceUnitId)
                .OrderBy(unit => unit.Id.Value)
                .ToArray();
            var fieldCandidates = human.Field
                .Where(unit => unit.Definition.Id == definition.SourceUnitId)
                .OrderBy(unit => unit.Id.Value)
                .ToArray();

            if (reserveCandidates.Length + fieldCandidates.Length < definition.RequiredCopies)
                continue;

            // Prefer Reserve copies so the resulting generated unit has room whenever possible.
            // Within each zone, stable instance-id ordering keeps replay/debug behavior deterministic.
            var selected = reserveCandidates
                .Concat(fieldCandidates)
                .Take(definition.RequiredCopies)
                .ToArray();
            var selectedReserveCount = Math.Min(reserveCandidates.Length, definition.RequiredCopies);
            var postCombineReserveCount = human.PlayableReserveCount - selectedReserveCount + 1;
            if (postCombineReserveCount > _session.Mod.PreparationRules.ReserveCapacity)
                continue;

            return (definition, selected.Select(unit => unit.Id).ToArray());
        }

        return null;
    }

    private static string BuildAutomaticCombineFailureKey(
        MatchState match,
        PlayerState human,
        UnitCombineDefinition definition,
        IReadOnlyList<UnitInstanceId> instanceIds) =>
        string.Join(
            ':',
            match.Round,
            human.PlayableReserveCount,
            human.Field.Count,
            definition.Id.Value,
            string.Join(',', instanceIds.Select(id => id.Value)));
}
