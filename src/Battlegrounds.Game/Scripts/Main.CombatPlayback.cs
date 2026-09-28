using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private long _observedCombatSequence;
    private double _combatPlaybackAccumulator;
    private CombatPlaybackState? _combatPlayback;
    private readonly Dictionary<UnitInstanceId, UnitId> _combatUnitDefinitions = [];
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

    private enum CombatVisualCue
    {
        None,
        Trigger,
        Attacker,
        Target,
        Summon,
        StatsChanged,
        Damage,
        Destroyed,
        Death,
        Revive,
        Behavior,
    }

    public override void _Process(double delta)
    {
        ObserveLatestCombat();
        if (_combatPlayback is null || _combatOverlay?.Visible != true) return;

        _combatPlaybackAccumulator += delta;
        var interval = ResolvePresentationMetric(
            ModThemeMetricKeys.Motion.CombatPlaybackStepSeconds,
            0.05f,
            10.0f);
        if (_combatPlaybackAccumulator < interval) return;

        _combatPlaybackAccumulator = 0;
        AdvanceCombatPlayback();
    }

    private void ObserveLatestCombat()
    {
        if (_session?.LastCombat is not SessionCombatRecord record || record.Sequence <= _observedCombatSequence)
            return;

        _observedCombatSequence = record.Sequence;
        var text = _presentationText ?? _session.Mod.Presentation.Resolve(null);
        var playback = CombatPlaybackState.TryCreate(record, _session.HumanPlayerId, text);
        if (playback is null) return;

        _combatPlayback = playback;
        BuildCombatUnitDefinitionIndex(playback);
        _combatPlaybackAccumulator = 0;
        EnsureCombatPlaybackUi();
        RefreshCombatThemeState();
        _combatOverlay!.Visible = true;
        ApplyScreenTheme(ModThemeScreenRoles.Combat);
        RenderCombatPlayback();
        AppendLog($"Playing resolved {Term("combat")} {Term("round")} {record.Round} from immutable session data.");
    }

    private void BuildCombatUnitDefinitionIndex(CombatPlaybackState playback)
    {
        _combatUnitDefinitions.Clear();
        foreach (var snapshot in playback.Record.StartingUnits)
            _combatUnitDefinitions[snapshot.InstanceId] = snapshot.UnitId;

        foreach (var timelineEvent in playback.Settlement.CombatResult.Timeline)
        {
            switch (timelineEvent)
            {
                case CombatUnitSummonedTimelineEvent summoned:
                    _combatUnitDefinitions[summoned.Unit.InstanceId] = summoned.Unit.UnitId;
                    break;
                case CombatUnitRevivedTimelineEvent revived:
                    _combatUnitDefinitions[revived.Unit.InstanceId] = revived.Unit.UnitId;
                    break;
            }
        }
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
        _combatOverlay.AddChild(margin);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        margin.AddChild(root);

        _combatTitle = new Label
        {
            Text = Text("ui.combatPlayback", ("combat", Term("combat"))),
            ThemeTypeVariation = "TitleLabel",
        };
        root.AddChild(_combatTitle);

        _combatProgress = new Label
        {
            ThemeTypeVariation = "CaptionLabel",
        };
        root.AddChild(_combatProgress);

        var boards = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(boards);

        var left = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _combatLeftHeader = new Label
        {
            ThemeTypeVariation = "HeadingLabel",
        };
        _combatLeftUnits = new VBoxContainer();
        left.AddChild(_combatLeftHeader);
        left.AddChild(_combatLeftUnits);
        boards.AddChild(left);

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _combatRightHeader = new Label
        {
            ThemeTypeVariation = "HeadingLabel",
        };
        _combatRightUnits = new VBoxContainer();
        right.AddChild(_combatRightHeader);
        right.AddChild(_combatRightUnits);
        boards.AddChild(right);

        _combatEvent = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = "BodyLabel",
        };
        root.AddChild(_combatEvent);

        var controls = new HBoxContainer();
        _combatNextButton = new Button
        {
            Text = Text("ui.next"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ThemeTypeVariation = "PrimaryButton",
        };
        _combatSkipButton = new Button { Text = Text("ui.skipSettlement") };
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
        _combatTitle!.Text = Text(
            "ui.combatResolvedTitle",
            ("round", Term("round")),
            ("roundValue", playback.Record.Round),
            ("combat", Term("combat")));
        _combatProgress!.Text = playback.SettlementVisible
            ? Text("ui.combatSettlementProgress", ("events", timeline.Count))
            : shownEvent == 0
                ? Text("ui.combatInitialBoards", ("events", timeline.Count))
                : Text("ui.combatEventProgress", ("current", shownEvent), ("events", timeline.Count));
        _combatLeftHeader!.Text = FormatCombatSide(playback.LeftPlayerId, archived: false);
        _combatRightHeader!.Text = FormatCombatSide(
            playback.RightPlayerId,
            archived: playback.Settlement.RightPlayerId is null);
        _combatEvent!.Text = playback.EventText;

        RenderCombatUnits(_combatLeftUnits!, playback.LeftPlayerId, playback.LeftUnits);
        RenderCombatUnits(_combatRightUnits!, playback.RightPlayerId, playback.RightUnits);

        _combatNextButton!.Text = playback.SettlementVisible ? Text("ui.continue") : Text("ui.next");
        _combatSkipButton!.Text = Text("ui.skipSettlement");
        _combatSkipButton.Visible = !playback.SettlementVisible;
    }

    private string FormatCombatSide(PlayerId playerId, bool archived)
    {
        var key = _session is not null && playerId == _session.HumanPlayerId
            ? "ui.sideYou"
            : archived
                ? "ui.sideArchived"
                : "ui.sideAi";
        return Text(key, ("player", playerId.Value));
    }

    private void RenderCombatUnits(
        VBoxContainer container,
        PlayerId playerId,
        IReadOnlyList<CombatPlaybackUnitState> units)
    {
        ClearChildren(container);
        if (units.Count == 0 && !IsCurrentDeathFor(playerId))
        {
            AddMutedLabel(container, Text("ui.emptyField", ("field", Term("field"))));
            return;
        }

        foreach (var unit in units)
        {
            var unitId = ResolveCombatUnitId(unit.InstanceId);
            var tier = ResolveCombatUnitTier(unitId);
            var stats = unit.IsAlive
                ? UnitCardStats(tier, unit.Attack ?? 0, unit.Health)
                : "—";
            var subtitle = string.IsNullOrEmpty(unit.Status)
                ? Term("unit")
                : $"{Term("unit")} • {unit.Status}";
            var card = CreateCombatUnitCard(unitId, unit.Name, subtitle, stats);
            ApplyCombatCue(card, unitId, CueForUnit(unit.InstanceId));
            container.AddChild(card);
        }

        RenderTransientDeathCue(container, playerId);
    }

    private PresentationCardButton CreateCombatUnitCard(
        UnitId? unitId,
        string title,
        string subtitle,
        string stats)
    {
        PresentationCardButton card;
        if (unitId is UnitId knownUnitId)
        {
            card = CreatePresentationCard(
                ModPresentationEntityKind.Unit,
                knownUnitId.Value,
                ModPresentationAssetSlots.Art,
                title,
                subtitle,
                stats);
        }
        else
        {
            card = new PresentationCardButton();
            card.Configure(title, subtitle, stats, description: null, texture: null);
        }

        card.MouseFilter = Control.MouseFilterEnum.Ignore;
        card.FocusMode = Control.FocusModeEnum.None;
        return card;
    }

    private UnitId? ResolveCombatUnitId(UnitInstanceId instanceId) =>
        _combatUnitDefinitions.TryGetValue(instanceId, out var unitId) ? unitId : null;

    private int ResolveCombatUnitTier(UnitId? unitId)
    {
        if (_session is null || unitId is not UnitId knownUnitId) return 0;
        return _session.Mod.Units.GetRequired(knownUnitId).Tier;
    }

    private CombatVisualCue CueForUnit(UnitInstanceId instanceId)
    {
        if (_combatPlayback?.CurrentEvent is not CombatTimelineEvent current) return CombatVisualCue.None;
        return current switch
        {
            CombatTriggerTimelineEvent trigger when trigger.SourceUnitInstanceId == instanceId => CombatVisualCue.Trigger,
            CombatAttackStartedTimelineEvent attack when attack.AttackerInstanceId == instanceId => CombatVisualCue.Attacker,
            CombatAttackStartedTimelineEvent attack when attack.TargetInstanceId == instanceId => CombatVisualCue.Target,
            CombatUnitSummonedTimelineEvent summon when summon.Unit.InstanceId == instanceId => CombatVisualCue.Summon,
            CombatUnitStatsChangedTimelineEvent stats when stats.UnitInstanceId == instanceId => CombatVisualCue.StatsChanged,
            CombatUnitDamagedTimelineEvent damage when damage.UnitInstanceId == instanceId => CombatVisualCue.Damage,
            CombatUnitDestroyedTimelineEvent destroyed when destroyed.UnitInstanceId == instanceId => CombatVisualCue.Destroyed,
            CombatUnitRevivedTimelineEvent revived when revived.Unit.InstanceId == instanceId => CombatVisualCue.Revive,
            CombatBehaviorChangedTimelineEvent behavior when behavior.UnitInstanceId == instanceId => CombatVisualCue.Behavior,
            _ => CombatVisualCue.None,
        };
    }

    private bool IsCurrentDeathFor(PlayerId playerId) =>
        _combatPlayback?.CurrentEvent is CombatUnitDiedTimelineEvent died && died.PlayerId == playerId;

    private void RenderTransientDeathCue(VBoxContainer container, PlayerId playerId)
    {
        if (_combatPlayback?.CurrentEvent is not CombatUnitDiedTimelineEvent died || died.PlayerId != playerId)
            return;

        var unitId = ResolveCombatUnitId(died.UnitInstanceId);
        var title = unitId is UnitId knownUnitId
            ? UnitName(knownUnitId)
            : Text("ui.combatUnitFallback", ("unit", Term("unit")), ("instance", died.UnitInstanceId.Value));
        var card = CreateCombatUnitCard(unitId, title, Term("unit"), "—");
        container.AddChild(card);
        var targetIndex = Math.Clamp(died.Position, 0, Math.Max(0, container.GetChildCount() - 1));
        container.MoveChild(card, targetIndex);
        ApplyCombatCue(card, unitId, CombatVisualCue.Death);
    }

    private void ApplyCombatCue(PresentationCardButton card, UnitId? unitId, CombatVisualCue cue)
    {
        if (cue == CombatVisualCue.None) return;

        var (role, fallbackAnimation) = cue switch
        {
            CombatVisualCue.Trigger => (ModPresentationCueRoles.CombatTrigger, ModPresentationAnimation.Pulse),
            CombatVisualCue.Attacker => (ModPresentationCueRoles.CombatAttack, ModPresentationAnimation.Lunge),
            CombatVisualCue.Target => (ModPresentationCueRoles.CombatTarget, ModPresentationAnimation.Shake),
            CombatVisualCue.Summon => (ModPresentationCueRoles.CombatSummon, ModPresentationAnimation.Pop),
            CombatVisualCue.StatsChanged => (ModPresentationCueRoles.CombatStats, ModPresentationAnimation.Pulse),
            CombatVisualCue.Damage => (ModPresentationCueRoles.CombatDamage, ModPresentationAnimation.Shake),
            CombatVisualCue.Destroyed => (ModPresentationCueRoles.CombatDestroy, ModPresentationAnimation.Shake),
            CombatVisualCue.Death => (ModPresentationCueRoles.CombatDeath, ModPresentationAnimation.Fade),
            CombatVisualCue.Revive => (ModPresentationCueRoles.CombatRevive, ModPresentationAnimation.Pop),
            CombatVisualCue.Behavior => (ModPresentationCueRoles.CombatBehavior, ModPresentationAnimation.Pulse),
            _ => throw new ArgumentOutOfRangeException(nameof(cue), cue, null),
        };

        PlayPresentationCue(
            card,
            ModPresentationEntityKind.Unit,
            unitId?.Value ?? "__unknown-combat-unit",
            role,
            fallbackAnimation);
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
            AppendLog($"Finished {Term("combat")} playback for {Term("round")} {_combatPlayback.Record.Round}.");
        _combatPlayback = null;
        _combatUnitDefinitions.Clear();
        _combatPlaybackAccumulator = 0;
        if (_combatOverlay is not null) _combatOverlay.Visible = false;
        ApplyScreenTheme(ModThemeScreenRoles.Preparation);
        Render();
    }
}
