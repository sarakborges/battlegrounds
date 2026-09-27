using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Choices;
using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;
using Godot;

namespace Battlegrounds.Game;

public partial class Main : Control
{
    [Export] public string ModPath { get; set; } = "res://../../mods/example";
    [Export] public int Seed { get; set; } = 20260927;
    [Export] public int ParticipantCount { get; set; } = 4;

    private readonly PresentationInteractionState _interaction = new();
    private SinglePlayerSession? _session;
    private Label _status = null!;
    private Label _matchSummary = null!;
    private Label _humanSummary = null!;
    private Label _leaderPrompt = null!;
    private VBoxContainer _leaderButtons = null!;
    private VBoxContainer _offerButtons = null!;
    private VBoxContainer _reserveButtons = null!;
    private VBoxContainer _fieldButtons = null!;
    private VBoxContainer _leaderPanel = null!;
    private VBoxContainer _preparationPanel = null!;
    private VBoxContainer _interactionPanel = null!;
    private Label _interactionPrompt = null!;
    private VBoxContainer _interactionButtons = null!;
    private Button _confirmInteractionButton = null!;
    private Button _cancelInteractionButton = null!;
    private Button _refreshButton = null!;
    private Button _upgradeButton = null!;
    private Button _freezeButton = null!;
    private Button _powerButton = null!;
    private Button _combineButton = null!;
    private Button _endPreparationButton = null!;
    private RichTextLabel _log = null!;

    public override void _Ready()
    {
        BindNodes();
        BindStaticActions();
        Bootstrap();
    }

    private void BindNodes()
    {
        _status = GetNode<Label>("%Status");
        _matchSummary = GetNode<Label>("%MatchSummary");
        _humanSummary = GetNode<Label>("%HumanSummary");
        _leaderPrompt = GetNode<Label>("%LeaderPrompt");
        _leaderButtons = GetNode<VBoxContainer>("%LeaderButtons");
        _offerButtons = GetNode<VBoxContainer>("%OfferButtons");
        _reserveButtons = GetNode<VBoxContainer>("%ReserveButtons");
        _fieldButtons = GetNode<VBoxContainer>("%FieldButtons");
        _leaderPanel = GetNode<VBoxContainer>("%LeaderPanel");
        _preparationPanel = GetNode<VBoxContainer>("%PreparationPanel");
        _interactionPanel = GetNode<VBoxContainer>("%InteractionPanel");
        _interactionPrompt = GetNode<Label>("%InteractionPrompt");
        _interactionButtons = GetNode<VBoxContainer>("%InteractionButtons");
        _confirmInteractionButton = GetNode<Button>("%ConfirmInteractionButton");
        _cancelInteractionButton = GetNode<Button>("%CancelInteractionButton");
        _refreshButton = GetNode<Button>("%RefreshButton");
        _upgradeButton = GetNode<Button>("%UpgradeButton");
        _freezeButton = GetNode<Button>("%FreezeButton");
        _powerButton = GetNode<Button>("%PowerButton");
        _combineButton = GetNode<Button>("%CombineButton");
        _endPreparationButton = GetNode<Button>("%EndPreparationButton");
        _log = GetNode<RichTextLabel>("%Log");
    }

    private void BindStaticActions()
    {
        _refreshButton.Pressed += () => ExecuteHuman(player => new RefreshOfferCommand(player.Id));
        _upgradeButton.Pressed += () => ExecuteHuman(player => new UpgradeTierCommand(player.Id));
        _freezeButton.Pressed += ToggleFreeze;
        _powerButton.Pressed += TryUsePower;
        _combineButton.Pressed += BeginCombineSelection;
        _endPreparationButton.Pressed += () => ExecuteHuman(player => new EndPreparationCommand(player.Id));
        _confirmInteractionButton.Pressed += ConfirmInteraction;
        _cancelInteractionButton.Pressed += CancelInteraction;
    }

    private void Bootstrap()
    {
        try
        {
            var modDirectory = ProjectSettings.GlobalizePath(ModPath);
            var mod = new ModLoader().Load(modDirectory);
            ValidateParticipantCount(mod);

            var humanPlayerId = new PlayerId(0);
            var aiPlayerIds = Enumerable.Range(1, ParticipantCount - 1).Select(value => new PlayerId(value)).ToArray();
            _session = SinglePlayerSession.Create(mod, humanPlayerId, aiPlayerIds, Seed);

            AppendLog($"Loaded mod '{mod.Name}' ({mod.Id}) with seed {Seed}.");
            AppendLog($"Created local session with 1 human and {aiPlayerIds.Length} AI opponents.");
            Render();
        }
        catch (Exception exception)
        {
            _status.Text = "Bootstrap failed";
            _leaderPrompt.Text = exception.Message;
            _leaderPanel.Visible = true;
            _preparationPanel.Visible = false;
            AppendLog(exception.ToString());
        }
    }

    private void ValidateParticipantCount(ModPackage mod)
    {
        if (ParticipantCount < mod.MatchRules.MinimumPlayers || ParticipantCount > mod.MatchRules.MaximumPlayers)
        {
            throw new InvalidOperationException(
                $"ParticipantCount must be between {mod.MatchRules.MinimumPlayers} and {mod.MatchRules.MaximumPlayers} for this mod.");
        }

        if (ParticipantCount % 2 != 0)
        {
            throw new InvalidOperationException(
                "ParticipantCount must currently be even because round one has no eliminated-opponent snapshot.");
        }
    }

    private void Render()
    {
        if (_session is null) return;

        if (!_session.HasStarted)
        {
            RenderLeaderSelection();
            return;
        }

        RenderMatch();
    }

    private void RenderLeaderSelection()
    {
        if (_session is null) return;

        _leaderPanel.Visible = true;
        _preparationPanel.Visible = false;
        _status.Text = $"{_session.Mod.Name} • Seed {Seed} • Leader selection";
        _leaderPrompt.Text = "Choose your Leader";
        _matchSummary.Text = string.Empty;
        ClearChildren(_leaderButtons);

        foreach (var leaderId in _session.LeaderSelection.GetOffer(_session.HumanPlayerId))
        {
            var definition = _session.Mod.Leaders.GetRequired(leaderId);
            var healthText = definition.HealthModifier switch
            {
                > 0 => $"+{definition.HealthModifier} Health",
                < 0 => $"{definition.HealthModifier} Health",
                _ => "base Health",
            };
            var button = new Button
            {
                Text = $"{definition.Name}  •  {healthText}  •  {definition.StartingArmor} Armor",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.Pressed += () => SelectLeader(leaderId);
            _leaderButtons.AddChild(button);
        }
    }

    private void SelectLeader(LeaderId leaderId)
    {
        if (_session is null) return;

        try
        {
            var result = _session.SelectHumanLeader(leaderId);
            if (!result.Succeeded)
            {
                AppendLog($"Leader selection rejected: {result.FailureCode}.");
                return;
            }

            AppendLog($"Selected Leader '{_session.Mod.Leaders.GetRequired(leaderId).Name}'.");
            AdvanceAutomation(prepareFollowingRound: false);
            Render();
        }
        catch (Exception exception)
        {
            AppendLog($"Leader selection failed: {exception.Message}");
        }
    }

    private void RenderMatch()
    {
        if (_session?.Match is not MatchState match) return;

        _leaderPanel.Visible = false;
        _preparationPanel.Visible = true;
        _status.Text = $"{_session.Mod.Name} • Round {match.Round} • {match.Phase}";
        _matchSummary.Text = BuildMatchSummary(match);

        if (!match.TryGetPlayer(_session.HumanPlayerId, out var human))
            throw new InvalidOperationException("Human player is missing from the active match.");

        if (human.PendingChoice is not null && _interaction.IsActive)
            _interaction.Reset();

        _humanSummary.Text = BuildHumanSummary(human);
        RenderInteraction(human, match);
        RenderOffer(human);
        RenderReserve(human);
        RenderField(human);
        UpdateActionButtons(human, match.Phase);
    }

    private string BuildMatchSummary(MatchState match)
    {
        if (_session is null) return string.Empty;

        var lines = match.Players
            .OrderBy(player => player.Id.Value)
            .Select(player =>
            {
                var actor = player.Id == _session.HumanPlayerId ? "YOU" : "AI";
                var leader = player.Leader?.Definition.Name ?? "No Leader";
                var armor = player.Leader?.Armor ?? 0;
                var state = player.IsEliminated ? "ELIMINATED" : player.IsReadyForCombat ? "READY" : "ACTIVE";
                return $"P{player.Id.Value} [{actor}] {leader}  •  HP {player.Health}  •  Armor {armor}  •  Tier {player.Tier}  •  {state}";
            });

        return string.Join('\n', lines);
    }

    private static string BuildHumanSummary(PlayerState player)
    {
        var upgrade = player.UpgradeCost is null ? "MAX" : player.UpgradeCost.Value.ToString();
        var choice = player.PendingChoice is null ? string.Empty : $"  •  Pending {player.PendingChoice.Kind} choice";
        return $"Resource {player.Resource}  •  Tier {player.Tier}  •  Upgrade {upgrade}  •  " +
               $"Offer {(player.IsOfferFrozen ? "Frozen" : "Open")}  •  " +
               $"Reserve {player.PlayableReserveCount}  •  Field {player.Field.Count}{choice}";
    }

    private void RenderInteraction(PlayerState human, MatchState match)
    {
        ClearChildren(_interactionButtons);
        _confirmInteractionButton.Visible = false;
        _confirmInteractionButton.Disabled = true;
        _cancelInteractionButton.Visible = false;

        if (human.PendingChoice is PendingChoice pendingChoice)
        {
            _interactionPanel.Visible = true;
            _interactionPrompt.Text = $"Resolve pending {pendingChoice.Kind} choice before continuing.";
            RenderPendingChoice(pendingChoice);
            return;
        }

        if (!_interaction.IsActive)
        {
            _interactionPanel.Visible = false;
            return;
        }

        _interactionPanel.Visible = true;
        _cancelInteractionButton.Visible = true;

        switch (_interaction.Kind)
        {
            case PresentationInteractionKind.ActionTarget:
                _interactionPrompt.Text = "Choose a Unit target for the Action. Core will validate the selected target.";
                RenderTargetCandidates(match);
                break;
            case PresentationInteractionKind.PowerTarget:
                _interactionPrompt.Text = "Choose a Unit target for the Power. Core will validate the selected target.";
                RenderTargetCandidates(match);
                break;
            case PresentationInteractionKind.CombineRecipe:
                RenderCombineRecipes(human);
                break;
            case PresentationInteractionKind.CombineComponents:
                RenderCombineComponentsPrompt(human);
                break;
            default:
                _interactionPanel.Visible = false;
                break;
        }
    }

    private void RenderPendingChoice(PendingChoice choice)
    {
        switch (choice)
        {
            case PendingUnitChoice unitChoice:
                for (var index = 0; index < unitChoice.Options.Count; index++)
                {
                    var option = unitChoice.Options[index];
                    var capturedIndex = index;
                    var button = new Button
                    {
                        Text = $"Choose {option.Name}  •  Unit  •  T{option.Tier}  •  {option.BaseAttack}/{option.BaseHealth}",
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    };
                    button.Pressed += () => ResolveChoice(unitChoice, capturedIndex);
                    _interactionButtons.AddChild(button);
                }
                break;
            case PendingActionChoice actionChoice:
                for (var index = 0; index < actionChoice.Options.Count; index++)
                {
                    var option = actionChoice.Options[index];
                    var capturedIndex = index;
                    var button = new Button
                    {
                        Text = $"Choose {option.Name}  •  Action  •  T{option.Tier}  •  Cost {option.Cost}",
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    };
                    button.Pressed += () => ResolveChoice(actionChoice, capturedIndex);
                    _interactionButtons.AddChild(button);
                }
                break;
            default:
                AddMutedLabel(_interactionButtons, "Unsupported pending choice type.");
                break;
        }
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

    private void RenderTargetCandidates(MatchState match)
    {
        var count = 0;
        foreach (var player in match.Players.OrderBy(value => value.Id.Value))
        {
            foreach (var unit in player.Field)
            {
                count++;
                var capturedUnitId = unit.Id;
                var button = new Button
                {
                    Text = $"Target P{player.Id.Value} • {unit.Definition.Name} • {unit.Attack}/{unit.Health}",
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                button.Pressed += () => SubmitSelectedTarget(capturedUnitId);
                _interactionButtons.AddChild(button);
            }
        }

        if (count == 0)
            AddMutedLabel(_interactionButtons, "No Field Units are currently available as target candidates.");
    }

    private void RenderCombineRecipes(PlayerState human)
    {
        if (_session is null) return;

        var available = GetAvailableCombines(human);
        _interactionPrompt.Text = available.Count == 0
            ? "No combine recipe currently has enough owned source copies."
            : "Choose a combine recipe, then explicitly select the component instances to consume.";

        foreach (var definition in available)
        {
            var result = _session.Mod.Units.GetRequired(definition.ResultUnitId);
            var button = new Button
            {
                Text = $"{definition.Name} • {definition.RequiredCopies} copies → {result.Name}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.Pressed += () => SelectCombineRecipe(definition.Id);
            _interactionButtons.AddChild(button);
        }
    }

    private void RenderCombineComponentsPrompt(PlayerState human)
    {
        if (_session is null || _interaction.CombineId is not UnitCombineId combineId) return;

        var definition = _session.Mod.Combines.GetRequired(combineId);
        var selected = _interaction.SelectedUnits.Count;
        _interactionPrompt.Text =
            $"{definition.Name}: select exactly {definition.RequiredCopies} owned '{definition.SourceUnitId.Value}' instances in Reserve/Field. " +
            $"Selected {selected}/{definition.RequiredCopies}.";
        AddMutedLabel(_interactionButtons, "Click eligible Unit rows below to toggle component selection.");
        _confirmInteractionButton.Visible = true;
        _confirmInteractionButton.Text = $"Combine {selected}/{definition.RequiredCopies}";
        _confirmInteractionButton.Disabled = selected != definition.RequiredCopies;
    }

    private IReadOnlyList<UnitCombineDefinition> GetAvailableCombines(PlayerState human)
    {
        if (_session is null) return [];

        return _session.Mod.Combines.All
            .Where(definition => CountOwnedCopies(human, definition.SourceUnitId) >= definition.RequiredCopies)
            .ToArray();
    }

    private static int CountOwnedCopies(PlayerState human, UnitId sourceUnitId) =>
        human.Reserve.Count(unit => unit.Definition.Id == sourceUnitId) +
        human.Field.Count(unit => unit.Definition.Id == sourceUnitId);

    private void RenderOffer(PlayerState human)
    {
        if (_session is null) return;

        ClearChildren(_offerButtons);
        if (human.PlayableOffer.Count == 0)
        {
            AddMutedLabel(_offerButtons, "No offer entries.");
            return;
        }

        var blocked = human.PendingChoice is not null || _interaction.IsActive;
        foreach (var entry in human.PlayableOffer)
        {
            var cost = entry.Cost ?? _session.Mod.PreparationRules.AcquireCost;
            var button = new Button
            {
                Text = $"Acquire {entry.Name}  •  {entry.Kind}  •  T{entry.Tier}  •  Cost {cost}",
                Disabled = human.IsReadyForCombat || blocked,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            var slot = entry.Slot;
            button.Pressed += () => ExecuteHuman(player => new AcquirePlayableCommand(player.Id, slot));
            _offerButtons.AddChild(button);
        }
    }

    private void RenderReserve(PlayerState human)
    {
        ClearChildren(_reserveButtons);
        if (human.PlayableReserve.Count == 0)
        {
            AddMutedLabel(_reserveButtons, "Reserve is empty.");
            return;
        }

        var choiceBlocked = human.PendingChoice is not null;
        UnitCombineDefinition? combine = null;
        if (_session is not null && _interaction.Kind == PresentationInteractionKind.CombineComponents &&
            _interaction.CombineId is UnitCombineId combineId)
        {
            combine = _session.Mod.Combines.GetRequired(combineId);
        }

        foreach (var entry in human.PlayableReserve)
        {
            var slot = entry.Slot;
            if (combine is not null && entry.Kind == PlayableKind.Unit && entry.Unit is UnitInstance unit)
            {
                var eligible = unit.Definition.Id == combine.SourceUnitId;
                var selected = _interaction.IsSelected(unit.Id);
                var button = new Button
                {
                    Text = $"{(selected ? "[x]" : "[ ]")} {unit.Definition.Name} • Reserve • {unit.Attack}/{unit.Health}",
                    Disabled = !eligible,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                button.Pressed += () => ToggleCombineUnit(unit, combine);
                _reserveButtons.AddChild(button);
                continue;
            }

            var verb = entry.Kind == PlayableKind.Unit ? "Deploy" : "Play";
            var buttonNormal = new Button
            {
                Text = $"{verb} {entry.Name}  •  {entry.Kind}",
                Disabled = human.IsReadyForCombat || choiceBlocked || _interaction.IsActive,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            buttonNormal.Pressed += entry.Kind == PlayableKind.Unit
                ? () => ExecuteHuman(player => new DeployUnitCommand(player.Id, slot))
                : () => TryPlayAction(slot);
            _reserveButtons.AddChild(buttonNormal);
        }
    }

    private void RenderField(PlayerState human)
    {
        ClearChildren(_fieldButtons);
        if (human.Field.Count == 0)
        {
            AddMutedLabel(_fieldButtons, "Field is empty.");
            return;
        }

        UnitCombineDefinition? combine = null;
        if (_session is not null && _interaction.Kind == PresentationInteractionKind.CombineComponents &&
            _interaction.CombineId is UnitCombineId combineId)
        {
            combine = _session.Mod.Combines.GetRequired(combineId);
        }

        for (var fieldSlot = 0; fieldSlot < human.Field.Count; fieldSlot++)
        {
            var unit = human.Field[fieldSlot];
            var capturedSlot = fieldSlot;

            if (combine is not null)
            {
                var eligible = unit.Definition.Id == combine.SourceUnitId;
                var selected = _interaction.IsSelected(unit.Id);
                var selectionButton = new Button
                {
                    Text = $"{(selected ? "[x]" : "[ ]")} {unit.Definition.Name} • Field • {unit.Attack}/{unit.Health}",
                    Disabled = !eligible,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                selectionButton.Pressed += () => ToggleCombineUnit(unit, combine);
                _fieldButtons.AddChild(selectionButton);
                continue;
            }

            var button = new Button
            {
                Text = $"Release {unit.Definition.Name}  •  {unit.Attack}/{unit.Health}",
                Disabled = human.IsReadyForCombat || human.PendingChoice is not null || _interaction.IsActive,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.Pressed += () => ExecuteHuman(player => new ReleaseUnitCommand(player.Id, capturedSlot));
            _fieldButtons.AddChild(button);
        }
    }

    private void UpdateActionButtons(PlayerState human, MatchPhase phase)
    {
        var baseCanAct = phase == MatchPhase.Preparation && !human.IsEliminated && !human.IsReadyForCombat;
        var canAct = baseCanAct && human.PendingChoice is null && !_interaction.IsActive;
        _refreshButton.Disabled = !canAct;
        _upgradeButton.Disabled = !canAct;
        _freezeButton.Disabled = !canAct;
        _powerButton.Disabled = !canAct || human.Leader?.CurrentPowerId is null;
        _combineButton.Disabled = !canAct;
        _endPreparationButton.Disabled = !canAct;
        _freezeButton.Text = human.IsOfferFrozen ? "Unfreeze offer" : "Freeze offer";
    }

    private void ToggleFreeze()
    {
        ExecuteHuman(player =>
            player.IsOfferFrozen
                ? new UnfreezeOfferCommand(player.Id)
                : new FreezeOfferCommand(player.Id));
    }

    private void TryPlayAction(int reserveSlot)
    {
        if (!TryGetHuman(out var human)) return;

        var command = new PlayActionCommand(human.Id, reserveSlot);
        var result = SubmitHumanCommand(command, logFailure: false);
        if (result.HasValue && !result.Value.Succeeded &&
            result.Value.FailureCode == PreparationFailureCode.InvalidActionTarget)
        {
            _interaction.BeginActionTarget(reserveSlot);
            AppendLog("Action requires a selected Unit target.");
        }
        else if (result.HasValue && !result.Value.Succeeded)
        {
            AppendLog($"{command.GetType().Name} rejected: {result.Value.FailureCode}.");
        }

        Render();
    }

    private void TryUsePower()
    {
        if (!TryGetHuman(out var human)) return;

        var command = new UsePowerCommand(human.Id);
        var result = SubmitHumanCommand(command, logFailure: false);
        if (result.HasValue && !result.Value.Succeeded &&
            result.Value.FailureCode == PreparationFailureCode.InvalidPowerTarget)
        {
            _interaction.BeginPowerTarget();
            AppendLog("Power requires a selected Unit target.");
        }
        else if (result.HasValue && !result.Value.Succeeded)
        {
            AppendLog($"{command.GetType().Name} rejected: {result.Value.FailureCode}.");
        }

        Render();
    }

    private void SubmitSelectedTarget(UnitInstanceId unitId)
    {
        if (!TryGetHuman(out var human)) return;

        IPreparationCommand command = _interaction.Kind switch
        {
            PresentationInteractionKind.ActionTarget when _interaction.ActionReserveSlot is int reserveSlot =>
                new PlayActionCommand(human.Id, reserveSlot, unitId),
            PresentationInteractionKind.PowerTarget => new UsePowerCommand(human.Id, unitId),
            _ => throw new InvalidOperationException("No target-based interaction is active."),
        };

        var result = SubmitHumanCommand(command, logFailure: false);
        if (result.HasValue && result.Value.Succeeded)
        {
            _interaction.Reset();
        }
        else if (result.HasValue)
        {
            AppendLog($"Selected target rejected: {result.Value.FailureCode}. Choose another target or cancel.");
        }

        Render();
    }

    private void BeginCombineSelection()
    {
        if (!TryGetHuman(out _)) return;
        _interaction.BeginCombineRecipeSelection();
        Render();
    }

    private void SelectCombineRecipe(UnitCombineId combineId)
    {
        _interaction.BeginCombineComponents(combineId);
        Render();
    }

    private void ToggleCombineUnit(UnitInstance unit, UnitCombineDefinition definition)
    {
        if (!_interaction.IsSelected(unit.Id) && _interaction.SelectedUnits.Count >= definition.RequiredCopies)
        {
            AppendLog($"{definition.Name} already has {definition.RequiredCopies} selected components.");
            return;
        }

        _interaction.ToggleUnit(unit.Id);
        Render();
    }

    private void ConfirmInteraction()
    {
        if (_interaction.Kind != PresentationInteractionKind.CombineComponents ||
            _interaction.CombineId is not UnitCombineId combineId ||
            !TryGetHuman(out var human) || _session is null)
        {
            return;
        }

        var definition = _session.Mod.Combines.GetRequired(combineId);
        if (_interaction.SelectedUnits.Count != definition.RequiredCopies) return;

        var command = new CombineUnitsCommand(human.Id, combineId, _interaction.SelectedUnits.ToArray());
        var result = SubmitHumanCommand(command);
        if (result.HasValue && result.Value.Succeeded)
            _interaction.Reset();
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

            AppendLog($"{command.GetType().Name} accepted.");
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
        if (result.AiPreparationsCompleted > 0)
            AppendLog($"AI completed {result.AiPreparationsCompleted} Preparation turn(s).");

        if (result.CombatRound is null) return;

        AppendLog($"Resolved combat round {result.Round - (result.Phase == MatchPhase.Preparation ? 1 : 0)} with {result.Pairings.Count} pairing(s).");
        foreach (var settlement in result.CombatRound.Settlements)
        {
            var opponent = settlement.RightPlayerId is not null
                ? $"P{settlement.RightPlayerId.Value.Value}"
                : $"archived P{settlement.EliminatedOpponentSourcePlayerId?.Value}";
            var winner = settlement.EliminatedOpponentWon
                ? opponent
                : settlement.WinnerPlayerId is null ? "draw" : $"P{settlement.WinnerPlayerId.Value.Value}";
            AppendLog($"P{settlement.LeftPlayerId.Value} vs {opponent}: {winner}; damage {settlement.PlayerDamage}.");
        }

        if (result.CombatRound.MatchFinished)
        {
            AppendLog($"Match finished. Winner: P{result.CombatRound.WinnerPlayerId?.Value}.");
            return;
        }

        if (prepareFollowingRound && result.Phase == MatchPhase.Preparation)
        {
            var prep = _session.AdvanceAutomated();
            if (prep.AiPreparationsCompleted > 0)
                AppendLog($"AI prepared for round {prep.Round}.");
        }
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.Text = string.IsNullOrEmpty(_log.Text) ? line : $"{_log.Text}\n{line}";
        _log.ScrollToLine(Math.Max(0, _log.GetLineCount() - 1));
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
            child.QueueFree();
    }

    private static void AddMutedLabel(Node parent, string text)
    {
        parent.AddChild(new Label { Text = text });
    }
}
