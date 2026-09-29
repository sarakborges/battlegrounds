using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Choices;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Players;
using Godot;

namespace Battlegrounds.Game;

public partial class Main : Control
{
    [Export] public string ModPath { get; set; } = "res://../../mods/example";
    [Export] public int Seed { get; set; } = 20260927;
    [Export] public int ParticipantCount { get; set; } = 4;

    private readonly PresentationInteractionState _interaction = new();
    private SinglePlayerSession? _session;

    public override void _Ready()
    {
        Bootstrap();
        EnsureWebUiInitialized();
    }

    private void Bootstrap()
    {
        try
        {
            var modDirectory = ProjectSettings.GlobalizePath(ModPath);
            var mod = new ModLoader().Load(modDirectory);
            ValidateParticipantCount(mod);
            InitializePresentation(mod);

            var humanPlayerId = new PlayerId(0);
            var aiPlayerIds = Enumerable.Range(1, ParticipantCount - 1)
                .Select(value => new PlayerId(value))
                .ToArray();
            _session = SinglePlayerSession.Create(mod, humanPlayerId, aiPlayerIds, Seed);

            AppendLog($"Loaded mod '{mod.Name}' ({mod.Id}) with seed {Seed}.");
            AppendLog($"Created local session with 1 human and {aiPlayerIds.Length} AI opponents.");
        }
        catch (Exception exception)
        {
            GD.PushError($"Gameplay bootstrap failed: {exception}");
        }
    }

    private void ValidateParticipantCount(ModPackage mod)
    {
        if (ParticipantCount < mod.MatchRules.MinimumPlayers || ParticipantCount > mod.MatchRules.MaximumPlayers)
        {
            throw new InvalidOperationException(
                $"ParticipantCount must be between {mod.MatchRules.MinimumPlayers} and {mod.MatchRules.MaximumPlayers} for this mod.");
        }
    }

    private void SelectLeader(LeaderId leaderId)
    {
        if (_session is null) return;

        var result = _session.SelectHumanLeader(leaderId);
        if (!result.Succeeded)
        {
            AppendLog($"{Term("leader")} selection rejected: {result.FailureCode}.");
            return;
        }

        AppendLog($"Selected {Term("leader")} '{LeaderName(leaderId)}'.");
        AdvanceAutomation(prepareFollowingRound: false);
        Render();
    }

    private void ResolveChoice(PendingChoice choice, int optionIndex)
    {
        if (_session is null) return;

        IPreparationCommand command = choice switch
        {
            PendingUnitChoice => new ResolveUnitChoiceCommand(_session.HumanPlayerId, choice.Id, optionIndex),
            PendingActionChoice => new ResolveActionChoiceCommand(_session.HumanPlayerId, choice.Id, optionIndex),
            _ => throw new InvalidOperationException($"Unsupported pending choice type '{choice.GetType().Name}'."),
        };

        SubmitHumanCommand(command);
        Render();
    }

    private void ToggleFreeze()
    {
        ExecuteHuman(player =>
            player.IsOfferFrozen
                ? new UnfreezeOfferCommand(player.Id)
                : new FreezeOfferCommand(player.Id));
    }

    private void TryDeployUnit(int reserveSlot)
    {
        if (!TryGetHuman(out var human)) return;

        var command = new DeployUnitCommand(human.Id, reserveSlot);
        var result = SubmitHumanCommand(command, logFailure: false);
        if (result.HasValue && !result.Value.Succeeded &&
            result.Value.FailureCode == PreparationFailureCode.InvalidDeployTarget)
        {
            var targetZone = GetSelectedTargetZone(
                human.Reserve[reserveSlot].Definition.Triggers
                    .Where(trigger => trigger.Event == NativeTriggerKeys.OnPlay)
                    .SelectMany(trigger => trigger.Effects));
            _interaction.BeginDeployTarget(reserveSlot, targetZone);
        }
        else if (result.HasValue && !result.Value.Succeeded)
        {
            AppendLog($"{command.GetType().Name} rejected: {result.Value.FailureCode}.");
        }
    }

    private void TryPlayAction(int reserveSlot)
    {
        if (!TryGetHuman(out var human)) return;

        var command = new PlayActionCommand(human.Id, reserveSlot);
        var result = SubmitHumanCommand(command, logFailure: false);
        if (result.HasValue && !result.Value.Succeeded &&
            result.Value.FailureCode == PreparationFailureCode.InvalidActionTarget)
        {
            var action = human.PlayableReserve.Single(entry => entry.Slot == reserveSlot).Action
                ?? throw new InvalidOperationException("Targeted reserve entry is not an Action.");
            _interaction.BeginActionTarget(reserveSlot, GetSelectedTargetZone(action.Definition.Effects));
        }
        else if (result.HasValue && !result.Value.Succeeded)
        {
            AppendLog($"{command.GetType().Name} rejected: {result.Value.FailureCode}.");
        }
    }

    private void TryUsePower()
    {
        if (!TryGetHuman(out var human)) return;

        var command = new UsePowerCommand(human.Id);
        var result = SubmitHumanCommand(command, logFailure: false);
        if (result.HasValue && !result.Value.Succeeded &&
            result.Value.FailureCode == PreparationFailureCode.InvalidPowerTarget)
        {
            var powerId = human.Leader?.CurrentPowerId
                ?? throw new InvalidOperationException("Targeted power is unavailable.");
            var power = _session?.Mod.Powers.GetRequired(powerId)
                ?? throw new InvalidOperationException("Targeted power definition is unavailable.");
            var effects = power.FindTrigger(NativeTriggerKeys.OnActivate)?.Effects ?? [];
            _interaction.BeginPowerTarget(GetSelectedTargetZone(effects));
        }
        else if (result.HasValue && !result.Value.Succeeded)
        {
            AppendLog($"{command.GetType().Name} rejected: {result.Value.FailureCode}.");
        }
    }

    private static EffectTargetZone GetSelectedTargetZone(IEnumerable<EffectDefinition> effects)
    {
        var zones = effects
            .Select(GetEffectTargetSelector)
            .Where(selector => selector?.Scope == EffectTargetScope.Selected)
            .Select(selector => selector!.Zone)
            .Distinct()
            .ToArray();
        return zones.Length == 1 ? zones[0] : EffectTargetZone.Field;
    }

    private static EffectTargetSelector? GetEffectTargetSelector(EffectDefinition effect) => effect switch
    {
        ModifyStatsEffectDefinition value => value.Target,
        DealDamageEffectDefinition value => value.Target,
        DestroyUnitEffectDefinition value => value.Target,
        TriggerEventEffectDefinition value => value.Target,
        AddBehaviorEffectDefinition value => value.Target,
        RemoveBehaviorEffectDefinition value => value.Target,
        TransformUnitEffectDefinition value => value.Target,
        CopyUnitToReserveEffectDefinition value => value.Target,
        ReturnUnitToReserveEffectDefinition value => value.Target,
        ApplyUnitModifierEffectDefinition value => value.Target,
        RemoveUnitModifierEffectDefinition value => value.Target,
        _ => null,
    };

    private void SubmitSelectedTarget(UnitInstanceId unitId)
    {
        if (!TryGetHuman(out var human)) return;

        IPreparationCommand command = _interaction.Kind switch
        {
            PresentationInteractionKind.DeployTarget when _interaction.UnitReserveSlot is int unitReserveSlot =>
                new DeployUnitCommand(human.Id, unitReserveSlot, unitId),
            PresentationInteractionKind.ActionTarget when _interaction.ActionReserveSlot is int reserveSlot =>
                new PlayActionCommand(human.Id, reserveSlot, unitId),
            PresentationInteractionKind.PowerTarget => new UsePowerCommand(human.Id, unitId),
            _ => throw new InvalidOperationException("No target-based interaction is active."),
        };

        var result = SubmitHumanCommand(command, logFailure: false);
        if (result.HasValue && result.Value.Succeeded)
            _interaction.Reset();
        else if (result.HasValue)
            AppendLog($"Selected target rejected: {result.Value.FailureCode}.");

        Render();
    }

    private void CancelInteraction()
    {
        _interaction.Reset();
        Render();
    }

    private void ExecuteHuman(Func<PlayerState, IPreparationCommand> commandFactory)
    {
        if (!TryGetHuman(out var human)) return;
        SubmitHumanCommand(commandFactory(human));
        Render();
    }

    private PreparationCommandResult? SubmitHumanCommand(IPreparationCommand command, bool logFailure = true)
    {
        if (_session?.Match is null) return null;

        try
        {
            var result = _session.ExecuteHumanPreparation(command);
            if (!result.Succeeded)
            {
                if (logFailure)
                    AppendLog($"{command.GetType().Name} rejected: {result.FailureCode}.");
                return result;
            }

            AdvanceAutomation(prepareFollowingRound: true);
            return result;
        }
        catch (Exception exception)
        {
            AppendLog($"Command failed: {exception.Message}");
            return null;
        }
    }

    private bool TryGetHuman(out PlayerState human)
    {
        human = null!;
        return _session?.Match is MatchState match && match.TryGetPlayer(_session.HumanPlayerId, out human);
    }

    private void AdvanceAutomation(bool prepareFollowingRound)
    {
        if (_session?.Match is null) return;

        var result = _session.AdvanceAutomated();
        if (result.CombatRound is null) return;

        if (result.CombatRound.MatchFinished)
            return;

        if (prepareFollowingRound && result.Phase == MatchPhase.Preparation)
            _session.AdvanceAutomated();
    }

    private void Render() => PushWebUiState();

    private static void AppendLog(string message) => GD.Print($"[Battlegrounds] {message}");
}
