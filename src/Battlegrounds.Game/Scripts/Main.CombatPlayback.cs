using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private const string CombatLungeDirectionMeta = "presentation_lunge_direction";

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
    private HBoxContainer? _combatLeftUnits;
    private HBoxContainer? _combatRightUnits;
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

        var margin = new MarginContainer { Name = "CombatMargin" };
        _combatOverlay.AddChild(margin);

        var root = new VBoxContainer
        {
            Name = "CombatRoot",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        margin.AddChild(root);

        _combatTitle = new Label
        {
            Text = Text("ui.combatPlayback", ("combat", Term("combat"))),
            ThemeTypeVariation = "TitleLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        root.AddChild(_combatTitle);

        _combatProgress = new Label
        {
            ThemeTypeVariation = "CaptionLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        root.AddChild(_combatProgress);

        var battlefield = new VBoxContainer
        {
            Name = "CombatBattlefield",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(battlefield);

        var topSide = new VBoxContainer
        {
            Name = "CombatTopBoard",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _combatRightHeader = new Label
        {
            ThemeTypeVariation = "HeadingLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        topSide.AddChild(_combatRightHeader);
        var topCenter = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _combatRightUnits = new HBoxContainer { Name = "CombatTopUnits" };
        topCenter.AddChild(_combatRightUnits);
        topSide.AddChild(topCenter);
        battlefield.AddChild(topSide);

        _combatEvent = new Label
        {
            Name = "CombatEvent",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = "BodyLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        battlefield.AddChild(_combatEvent);

        var bottomSide = new VBoxContainer
        {
            Name = "CombatBottomBoard",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var bottomCenter = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _combatLeftUnits = new HBoxContainer { Name = "CombatBottomUnits" };
        bottomCenter.AddChild(_combatLeftUnits);
        bottomSide.AddChild(bottomCenter);
        _combatLeftHeader = new Label
        {
            ThemeTypeVariation = "HeadingLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        bottomSide.AddChild(_combatLeftHeader);
        battlefield.AddChild(bottomSide);

        var controls = new HBoxContainer { Name = "CombatControls" };
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

        RenderCombatUnits(_combatRightUnits!, playback.RightPlayerId, playback.RightUnits, isTopSide: true);
        RenderCombatUnits(_combatLeftUnits!, playback.LeftPlayerId, playback.LeftUnits, isTopSide: false);

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
        HBoxContainer container,
        PlayerId playerId,
        IReadOnlyList<CombatPlaybackUnitState> units,
        bool isTopSide)
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
            var attack = unit.Attack ?? 0;
            var health = Math.Max(0, unit.Health);
            var details = string.IsNullOrWhiteSpace(unit.Status) ? null : unit.Status;
            var card = CreateCombatUnitToken(unitId, unit.Name, attack, health, tier, details, isTopSide);
            container.AddChild(card);
            QueueCombatCue(card, unitId, CueForUnit(unit.InstanceId));
        }

        RenderTransientDeathCue(container, playerId, isTopSide);
    }

    private PresentationCardButton CreateCombatUnitToken(
        UnitId? unitId,
        string title,
        int attack,
        int health,
        int tier,
        string? details,
        bool isTopSide)
    {
        PresentationCardButton card;
        if (unitId is UnitId knownUnitId)
        {
            card = CreatePresentationCard(
                ModPresentationEntityKind.Unit,
                knownUnitId.Value,
                ModPresentationAssetSlots.Art,
                title,
                Term("unit"),
                string.Empty);
        }
        else
        {
            card = new PresentationCardButton();
            card.Configure(title, Term("unit"), string.Empty, description: null, texture: null);
        }

        card.SetMeta("presentation_skip_footprint", true);
        card.SetMeta(CombatLungeDirectionMeta, isTopSide ? Vector2.Down : Vector2.Up);
        card.ConfigureToken(
            tier: null,
            attack,
            health,
            inspectDetails: details,
            inspectTier: tier > 0 ? tier : null);

        var tokenWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Card.MinimumWidth(ModThemeMetricKeys.Card.BoardRole),
            32.0f,
            512.0f);
        var tokenHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardHeight(ModThemeMetricKeys.Row.Field),
            32.0f,
            768.0f);
        card.CustomMinimumSize = new Vector2(tokenWidth, tokenHeight);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.ButtonMask = (MouseButtonMask)0;
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

    private void RenderTransientDeathCue(HBoxContainer container, PlayerId playerId, bool isTopSide)
    {
        if (_combatPlayback?.CurrentEvent is not CombatUnitDiedTimelineEvent died || died.PlayerId != playerId)
            return;

        var unitId = ResolveCombatUnitId(died.UnitInstanceId);
        var title = unitId is UnitId knownUnitId
            ? UnitName(knownUnitId)
            : Text("ui.combatUnitFallback", ("unit", Term("unit")), ("instance", died.UnitInstanceId.Value));
        var tier = ResolveCombatUnitTier(unitId);
        var card = CreateCombatUnitToken(unitId, title, 0, 0, tier, details: null, isTopSide);
        container.AddChild(card);
        var targetIndex = Math.Clamp(died.Position, 0, Math.Max(0, container.GetChildCount() - 1));
        container.MoveChild(card, targetIndex);
        QueueCombatCue(card, unitId, CombatVisualCue.Death);
    }

    private void QueueCombatCue(PresentationCardButton card, UnitId? unitId, CombatVisualCue cue)
    {
        if (cue == CombatVisualCue.None) return;
        Callable.From(() =>
        {
            if (card.IsInsideTree())
                ApplyCombatCue(card, unitId, cue);
        }).CallDeferred();
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
