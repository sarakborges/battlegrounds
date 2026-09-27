using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Players;
using Godot;

namespace Battlegrounds.Game;

public partial class Main : Control
{
    [Export] public string ModPath { get; set; } = "res://../../mods/example";
    [Export] public int Seed { get; set; } = 20260927;
    [Export] public int ParticipantCount { get; set; } = 4;

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
    private Button _refreshButton = null!;
    private Button _upgradeButton = null!;
    private Button _freezeButton = null!;
    private Button _powerButton = null!;
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
        _refreshButton = GetNode<Button>("%RefreshButton");
        _upgradeButton = GetNode<Button>("%UpgradeButton");
        _freezeButton = GetNode<Button>("%FreezeButton");
        _powerButton = GetNode<Button>("%PowerButton");
        _endPreparationButton = GetNode<Button>("%EndPreparationButton");
        _log = GetNode<RichTextLabel>("%Log");
    }

    private void BindStaticActions()
    {
        _refreshButton.Pressed += () => ExecuteHuman(player => new RefreshOfferCommand(player.Id));
        _upgradeButton.Pressed += () => ExecuteHuman(player => new UpgradeTierCommand(player.Id));
        _freezeButton.Pressed += ToggleFreeze;
        _powerButton.Pressed += () => ExecuteHuman(player => new UsePowerCommand(player.Id));
        _endPreparationButton.Pressed += () => ExecuteHuman(player => new EndPreparationCommand(player.Id));
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
        if (_session is null)
        {
            return;
        }

        if (!_session.HasStarted)
        {
            RenderLeaderSelection();
            return;
        }

        RenderMatch();
    }

    private void RenderLeaderSelection()
    {
        if (_session is null)
        {
            return;
        }

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
        if (_session is null)
        {
            return;
        }

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
        if (_session?.Match is not MatchState match)
        {
            return;
        }

        _leaderPanel.Visible = false;
        _preparationPanel.Visible = true;
        _status.Text = $"{_session.Mod.Name} • Round {match.Round} • {match.Phase}";
        _matchSummary.Text = BuildMatchSummary(match);

        if (!match.TryGetPlayer(_session.HumanPlayerId, out var human))
        {
            throw new InvalidOperationException("Human player is missing from the active match.");
        }

        _humanSummary.Text = BuildHumanSummary(human);
        RenderOffer(human);
        RenderReserve(human);
        RenderField(human);
        UpdateActionButtons(human, match.Phase);
    }

    private string BuildMatchSummary(MatchState match)
    {
        if (_session is null)
        {
            return string.Empty;
        }

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
        return $"Resource {player.Resource}  •  Tier {player.Tier}  •  Upgrade {upgrade}  •  " +
               $"Offer {(player.IsOfferFrozen ? "Frozen" : "Open")}  •  " +
               $"Reserve {player.PlayableReserveCount}  •  Field {player.Field.Count}";
    }

    private void RenderOffer(PlayerState human)
    {
        if (_session is null)
        {
            return;
        }

        ClearChildren(_offerButtons);
        if (human.PlayableOffer.Count == 0)
        {
            AddMutedLabel(_offerButtons, "No offer entries.");
            return;
        }

        foreach (var entry in human.PlayableOffer)
        {
            var cost = entry.Cost ?? _session.Mod.PreparationRules.AcquireCost;
            var button = new Button
            {
                Text = $"Acquire {entry.Name}  •  {entry.Kind}  •  T{entry.Tier}  •  Cost {cost}",
                Disabled = human.IsReadyForCombat,
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

        foreach (var entry in human.PlayableReserve)
        {
            var slot = entry.Slot;
            var verb = entry.Kind == PlayableKind.Unit ? "Deploy" : "Play";
            var button = new Button
            {
                Text = $"{verb} {entry.Name}  •  {entry.Kind}",
                Disabled = human.IsReadyForCombat,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.Pressed += entry.Kind == PlayableKind.Unit
                ? () => ExecuteHuman(player => new DeployUnitCommand(player.Id, slot))
                : () => ExecuteHuman(player => new PlayActionCommand(player.Id, slot));
            _reserveButtons.AddChild(button);
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

        for (var fieldSlot = 0; fieldSlot < human.Field.Count; fieldSlot++)
        {
            var unit = human.Field[fieldSlot];
            var capturedSlot = fieldSlot;
            var button = new Button
            {
                Text = $"Release {unit.Definition.Name}  •  {unit.Attack}/{unit.Health}",
                Disabled = human.IsReadyForCombat,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            button.Pressed += () => ExecuteHuman(player => new ReleaseUnitCommand(player.Id, capturedSlot));
            _fieldButtons.AddChild(button);
        }
    }

    private void UpdateActionButtons(PlayerState human, MatchPhase phase)
    {
        var canAct = phase == MatchPhase.Preparation && !human.IsEliminated && !human.IsReadyForCombat;
        _refreshButton.Disabled = !canAct;
        _upgradeButton.Disabled = !canAct;
        _freezeButton.Disabled = !canAct;
        _powerButton.Disabled = !canAct || human.Leader?.CurrentPowerId is null;
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

    private void ExecuteHuman(Func<PlayerState, IPreparationCommand> commandFactory)
    {
        if (_session?.Match is not MatchState match || !match.TryGetPlayer(_session.HumanPlayerId, out var human))
        {
            return;
        }

        try
        {
            var command = commandFactory(human);
            var result = _session.ExecuteHumanPreparation(command);
            if (!result.Succeeded)
            {
                AppendLog($"{command.GetType().Name} rejected: {result.FailureCode}.");
                Render();
                return;
            }

            AppendLog($"{command.GetType().Name} accepted.");
            AdvanceAutomation(prepareFollowingRound: true);
            Render();
        }
        catch (Exception exception)
        {
            AppendLog($"Command failed: {exception.Message}");
            Render();
        }
    }

    private void AdvanceAutomation(bool prepareFollowingRound)
    {
        if (_session?.Match is null)
        {
            return;
        }

        var result = _session.AdvanceAutomated();
        if (result.AiPreparationsCompleted > 0)
        {
            AppendLog($"AI completed {result.AiPreparationsCompleted} Preparation turn(s).");
        }

        if (result.CombatRound is null)
        {
            return;
        }

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
            {
                AppendLog($"AI prepared for round {prep.Round}.");
            }
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
        {
            child.QueueFree();
        }
    }

    private static void AddMutedLabel(Node parent, string text)
    {
        parent.AddChild(new Label { Text = text });
    }
}
