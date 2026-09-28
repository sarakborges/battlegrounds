using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _automaticCombineSubmissionInProgress;
    private string? _automaticCombineFailureKey;
    private ModInteractionSettings? _interactionSettings;

    private ModInteractionSettings InteractionSettings =>
        _interactionSettings ??= new ModInteractionSettingsLoader().Load(ProjectSettings.GlobalizePath(ModPath));

    private bool UsesAutomaticCombines =>
        InteractionSettings.CombineMode == CombineInteractionMode.Automatic;

    private void UpdateAutomaticCombines()
    {
        UpdateCombineButtonVisibility();
        TrySubmitAutomaticCombine();
    }

    private void UpdateCombineButtonVisibility()
    {
        if (_combineButton is null)
            return;

        if (UsesAutomaticCombines || _session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
        {
            _combineButton.Visible = false;
            return;
        }

        _combineButton.Visible = match.TryGetPlayer(_session.HumanPlayerId, out var human) &&
                                 !human.IsEliminated &&
                                 !human.IsReadyForCombat &&
                                 human.PendingChoice is null &&
                                 GetAvailableCombines(human).Count > 0;
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
            if (result.HasValue && result.Value.Succeeded)
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
        $"{match.Round}:{human.PlayableReserveCount}:{human.Field.Count}:{definition.Id.Value}:" +
        string.Join(",", instanceIds.Select(id => id.Value));
}