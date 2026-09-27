using Battlegrounds.Application;
using Battlegrounds.Core.Domain.Ids;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    [Export] public double CombatPlaybackStepSeconds { get; set; } = 0.9;

    private long _observedCombatSequence;
    private double _combatPlaybackAccumulator;
    private CombatPlaybackState? _combatPlayback;
    private PanelContainer? _combatOverlay;
    private Label? _combatTitle;
    private Label? _combatProgress;
    private Label? _combatEvent;
    private Label? _combatLeftHeader;
    private Label? _combatRightHeader;
    private VBoxContainer? _combatLeftUnits;
    private VBoxContainer? _combatRightUnits;
    private Button? _combatNextButton;
    private Button? _combatSkipButton;

    public override void _Process(double delta)
    {
        ObserveLatestCombat();
        if (_combatPlayback is null || _combatOverlay?.Visible != true) return;

        _combatPlaybackAccumulator += delta;
        var interval = Math.Max(0.1, CombatPlaybackStepSeconds);
        if (_combatPlaybackAccumulator < interval) return;

        _combatPlaybackAccumulator = 0;
        AdvanceCombatPlayback();
    }

    private void ObserveLatestCombat()
    {
        if (_session?.LastCombat is not SessionCombatRecord record || record.Sequence <= _observedCombatSequence)
            return;

        _observedCombatSequence = record.Sequence;
        var playback = CombatPlaybackState.TryCreate(record, _session.HumanPlayerId);
        if (playback is null) return;

        _combatPlayback = playback;
        _combatPlaybackAccumulator = 0;
        EnsureCombatPlaybackUi();
        _combatOverlay!.Visible = true;
        RenderCombatPlayback();
        AppendLog($"Playing resolved combat round {record.Round} from immutable session data.");
    }

    private void EnsureCombatPlaybackUi()
    {
        if (_combatOverlay is not null) return;

        _combatOverlay = new PanelContainer
        {
            Name = "CombatPlaybackOverlay",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 100,
            Visible = false,
        };
        AddChild(_combatOverlay);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        _combatOverlay.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        _combatTitle = new Label { Text = "Combat playback" };
        _combatTitle.AddThemeFontSizeOverride("font_size", 26);
        root.AddChild(_combatTitle);

        _combatProgress = new Label();
        root.AddChild(_combatProgress);

        var boards = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        boards.AddThemeConstantOverride("separation", 24);
        root.AddChild(boards);

        var left = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _combatLeftHeader = new Label();
        _combatLeftHeader.AddThemeFontSizeOverride("font_size", 18);
        _combatLeftUnits = new VBoxContainer();
        _combatLeftUnits.AddThemeConstantOverride("separation", 6);
        left.AddChild(_combatLeftHeader);
        left.AddChild(_combatLeftUnits);
        boards.AddChild(left);

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _combatRightHeader = new Label();
        _combatRightHeader.AddThemeFontSizeOverride("font_size", 18);
        _combatRightUnits = new VBoxContainer();
        _combatRightUnits.AddThemeConstantOverride("separation", 6);
        right.AddChild(_combatRightHeader);
        right.AddChild(_combatRightUnits);
        boards.AddChild(right);

        _combatEvent = new Label { Text = "Combat ready." };
        _combatEvent.AddThemeFontSizeOverride("font_size", 17);
        root.AddChild(_combatEvent);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 8);
        _combatNextButton = new Button { Text = "Next", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _combatSkipButton = new Button { Text = "Skip to settlement" };
        _combatNextButton.Pressed += AdvanceCombatPlayback;
        _combatSkipButton.Pressed += SkipCombatPlayback;
        controls.AddChild(_combatNextButton);
        controls.AddChild(_combatSkipButton);
        root.AddChild(controls);
    }

    private void RenderCombatPlayback()
    {
        if (_combatPlayback is null || _combatOverlay is null) return;

        var playback = _combatPlayback;
        var timeline = playback.Settlement.CombatResult.Timeline;
        var shownEvent = playback.CurrentEvent?.Sequence ?? 0;
        _combatTitle!.Text = $"Round {playback.Record.Round} combat • already resolved by Core";
        _combatProgress!.Text = playback.SettlementVisible
            ? $"Settlement • {timeline.Count} timeline event(s)"
            : shownEvent == 0
                ? $"Initial boards • {timeline.Count} event(s) queued"
                : $"Event {shownEvent}/{timeline.Count} • {playback.CurrentEvent!.Kind}";
        _combatLeftHeader!.Text = FormatCombatSide(playback.LeftPlayerId, archived: false);
        _combatRightHeader!.Text = FormatCombatSide(
            playback.RightPlayerId,
            archived: playback.Settlement.RightPlayerId is null);
        _combatEvent!.Text = playback.EventText;

        RenderCombatUnits(_combatLeftUnits!, playback.LeftUnits);
        RenderCombatUnits(_combatRightUnits!, playback.RightUnits);

        _combatNextButton!.Text = playback.SettlementVisible ? "Continue" : "Next event";
        _combatSkipButton!.Visible = !playback.SettlementVisible;
    }

    private string FormatCombatSide(PlayerId playerId, bool archived)
    {
        var actor = _session is not null && playerId == _session.HumanPlayerId ? "YOU" : archived ? "ARCHIVED" : "AI";
        return $"P{playerId.Value} [{actor}]";
    }

    private static void RenderCombatUnits(VBoxContainer container, IReadOnlyList<CombatPlaybackUnitState> units)
    {
        ClearChildren(container);
        if (units.Count == 0)
        {
            AddMutedLabel(container, "Empty field");
            return;
        }

        foreach (var unit in units)
        {
            var stats = unit.IsAlive
                ? $"{(unit.Attack?.ToString() ?? "?")}/{unit.Health}"
                : "DEAD";
            var highlight = string.IsNullOrEmpty(unit.Highlight) ? string.Empty : $"[{unit.Highlight}] ";
            var status = string.IsNullOrEmpty(unit.Status) ? string.Empty : $" • {unit.Status}";
            var label = new Label
            {
                Text = $"{highlight}{unit.Name} • {stats}{status}",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            container.AddChild(label);
        }
    }

    private void AdvanceCombatPlayback()
    {
        if (_combatPlayback is null) return;
        _combatPlaybackAccumulator = 0;
        if (_combatPlayback.Advance())
        {
            FinishCombatPlayback();
            return;
        }

        RenderCombatPlayback();
    }

    private void SkipCombatPlayback()
    {
        if (_combatPlayback is null) return;
        _combatPlaybackAccumulator = 0;
        if (_combatPlayback.SettlementVisible)
        {
            FinishCombatPlayback();
            return;
        }

        _combatPlayback.SkipToSettlement();
        RenderCombatPlayback();
    }

    private void FinishCombatPlayback()
    {
        if (_combatPlayback is not null)
            AppendLog($"Finished combat playback for round {_combatPlayback.Record.Round}.");
        _combatPlayback = null;
        _combatPlaybackAccumulator = 0;
        if (_combatOverlay is not null) _combatOverlay.Visible = false;
        Render();
    }
}
