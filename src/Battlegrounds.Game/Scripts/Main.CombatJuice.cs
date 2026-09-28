using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private readonly Dictionary<UnitInstanceId, PresentationCardButton> _combatCardsByInstance = [];
    private PanelContainer? _combatSettlementBanner;
    private Label? _combatSettlementTitle;
    private Label? _combatSettlementDamage;
    private PlayerId? _combatPendingReflowPlayerId;
    private int _combatPendingReflowPosition = -1;
    private long _combatSettlementAnimatedSequence = -1;
    private long _combatJuiceEventSequence = -1;

    private void EnsureCombatJuiceUi(VBoxContainer root)
    {
        if (_combatSettlementBanner is not null)
            return;

        var center = new CenterContainer
        {
            Name = "CombatSettlementCenter",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(center);

        _combatSettlementBanner = new PanelContainer
        {
            Name = "CombatSettlementBanner",
            ThemeTypeVariation = "InteractionSurface",
            Visible = false,
        };
        var width = ResolvePresentationMetric(
            ModThemeMetricKeys.Card.InspectWidth,
            180.0f,
            1024.0f);
        _combatSettlementBanner.CustomMinimumSize = new Vector2(width, 0);
        center.AddChild(_combatSettlementBanner);

        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", Mathf.RoundToInt(ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.Combat.ContentGap,
            0.0f,
            256.0f)));
        _combatSettlementBanner.AddChild(column);

        _combatSettlementTitle = new Label
        {
            ThemeTypeVariation = "TitleLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        column.AddChild(_combatSettlementTitle);

        _combatSettlementDamage = new Label
        {
            ThemeTypeVariation = "BodyLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        column.AddChild(_combatSettlementDamage);
    }

    private void RegisterCombatCard(UnitInstanceId instanceId, PresentationCardButton card)
    {
        _combatCardsByInstance[instanceId] = card;
    }

    private void RenderCombatJuice(CombatPlaybackState playback)
    {
        if (playback.SettlementVisible)
        {
            RenderCombatSettlementJuice(playback);
            return;
        }

        if (_combatSettlementBanner is not null)
            _combatSettlementBanner.Visible = false;

        if (playback.CurrentEvent is null || playback.CurrentEvent.Sequence == _combatJuiceEventSequence)
            return;

        _combatJuiceEventSequence = playback.CurrentEvent.Sequence;
        switch (playback.CurrentEvent)
        {
            case CombatUnitDamagedTimelineEvent damaged:
                SpawnCombatDamageFeedback(damaged);
                break;
            case CombatUnitDiedTimelineEvent died:
                RememberCombatReflow(died.PlayerId, died.Position);
                break;
        }
    }

    private void SpawnCombatDamageFeedback(CombatUnitDamagedTimelineEvent damaged)
    {
        if (!_combatCardsByInstance.TryGetValue(damaged.UnitInstanceId, out var card) || !card.IsInsideTree())
            return;

        card.ZIndex = Math.Max(card.ZIndex, 24);
        SpawnCombatImpactFlash(card);
        SpawnCombatFloatingValue(card, $"-{damaged.Amount}", "HealthValueLabel");
    }

    private void SpawnCombatImpactFlash(Control target)
    {
        var flashColor = ResolveCombatThemeColor("dangerActive", "#E36A59");
        var flash = new ColorRect
        {
            Name = "CombatImpactFlash",
            Color = new Color(flashColor, 0.42f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 40,
        };
        target.AddChild(flash);
        flash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var stepDuration = ResolvePresentationMetric(
            ModThemeMetricKeys.Motion.CombatPlaybackStepSeconds,
            0.05f,
            10.0f);
        var duration = Math.Clamp(stepDuration * 0.32, 0.08, 0.35);
        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "modulate:a", 0.0f, duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => flash.QueueFree()));
    }

    private void SpawnCombatFloatingValue(Control target, string text, string themeVariation)
    {
        var popup = new Label
        {
            Name = "CombatFloatingValue",
            Text = text,
            ThemeTypeVariation = themeVariation,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 60,
        };
        target.AddChild(popup);
        popup.AnchorLeft = 0.5f;
        popup.AnchorTop = 0.48f;
        popup.AnchorRight = 0.5f;
        popup.AnchorBottom = 0.48f;
        popup.OffsetLeft = -48;
        popup.OffsetRight = 48;
        popup.OffsetTop = -22;
        popup.OffsetBottom = 22;

        var riseDistance = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardHeight(ModThemeMetricKeys.Row.Field),
            32.0f,
            768.0f) * 0.34f;
        var stepDuration = ResolvePresentationMetric(
            ModThemeMetricKeys.Motion.CombatPlaybackStepSeconds,
            0.05f,
            10.0f);
        var duration = Math.Clamp(stepDuration * 0.8, 0.2, 0.9);
        var targetPosition = popup.Position + Vector2.Up * riseDistance;

        popup.Scale = new Vector2(1.18f, 1.18f);
        popup.PivotOffset = popup.Size * 0.5f;
        var tween = popup.CreateTween();
        tween.SetParallel();
        tween.TweenProperty(popup, "position", targetPosition, duration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(popup, "scale", Vector2.One, duration * 0.45)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(popup, "modulate:a", 0.0f, duration)
            .SetDelay(duration * 0.38);
        tween.Chain().TweenCallback(Callable.From(() => popup.QueueFree()));
    }

    private void RememberCombatReflow(PlayerId playerId, int position)
    {
        if (_combatPendingReflowPlayerId is PlayerId pending && pending == playerId)
            _combatPendingReflowPosition = Math.Min(_combatPendingReflowPosition, position);
        else
        {
            _combatPendingReflowPlayerId = playerId;
            _combatPendingReflowPosition = position;
        }
    }

    private void QueueCombatReflowIfNeeded(HBoxContainer container, PlayerId playerId)
    {
        if (_combatPendingReflowPlayerId is not PlayerId pending || pending != playerId)
            return;
        if (_combatPlayback?.CurrentEvent is CombatUnitDiedTimelineEvent)
            return;

        var firstShiftedIndex = Math.Max(0, _combatPendingReflowPosition);
        _combatPendingReflowPlayerId = null;
        _combatPendingReflowPosition = -1;

        Callable.From(() =>
        {
            if (!container.IsInsideTree())
                return;

            var cards = container.GetChildren()
                .OfType<PresentationCardButton>()
                .Where(card => !card.IsQueuedForDeletion())
                .ToArray();
            if (cards.Length == 0)
                return;

            var nudge = ResolvePresentationMetric(
                ModThemeMetricKeys.Card.MinimumWidth(ModThemeMetricKeys.Card.BoardRole),
                32.0f,
                512.0f) * 0.2f;
            var duration = ResolvePresentationMetric(
                ModThemeMetricKeys.Motion.Cue.PopDurationSeconds,
                0.05f,
                5.0f);

            for (var index = firstShiftedIndex; index < cards.Length; index++)
            {
                var card = cards[index];
                var settledPosition = card.Position;
                card.Position = settledPosition + Vector2.Right * nudge;
                card.CreateTween()
                    .TweenProperty(card, "position", settledPosition, duration)
                    .SetTrans(Tween.TransitionType.Back)
                    .SetEase(Tween.EaseType.Out);
            }
        }).CallDeferred();
    }

    private void RenderCombatSettlementJuice(CombatPlaybackState playback)
    {
        if (_combatSettlementBanner is null || _combatSettlementTitle is null || _combatSettlementDamage is null)
            return;

        _combatSettlementBanner.Visible = true;
        _combatSettlementTitle.Text = CombatOutcomeTitle(playback);
        _combatSettlementDamage.Text = CombatSettlementDamageText(playback);

        if (_combatSettlementAnimatedSequence == playback.Record.Sequence)
            return;
        _combatSettlementAnimatedSequence = playback.Record.Sequence;

        Callable.From(() =>
        {
            if (_combatSettlementBanner?.IsInsideTree() != true)
                return;

            var scale = ResolvePresentationMetric(
                ModThemeMetricKeys.Motion.Cue.PopScale,
                0.1f,
                3.0f);
            var duration = ResolvePresentationMetric(
                ModThemeMetricKeys.Motion.Cue.PopDurationSeconds,
                0.05f,
                5.0f);
            _combatSettlementBanner.PivotOffset = _combatSettlementBanner.Size * 0.5f;
            _combatSettlementBanner.Scale = new Vector2(scale, scale);
            _combatSettlementBanner.Modulate = new Color(1, 1, 1, 0.2f);
            var tween = _combatSettlementBanner.CreateTween();
            tween.SetParallel();
            tween.TweenProperty(_combatSettlementBanner, "scale", Vector2.One, duration)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_combatSettlementBanner, "modulate", Colors.White, duration);
        }).CallDeferred();

        if (playback.Settlement.DamagedPlayerId is not PlayerId damagedPlayer || playback.Settlement.PlayerDamage <= 0)
            return;

        var damagedHeader = damagedPlayer == playback.LeftPlayerId
            ? _combatLeftHeader
            : damagedPlayer == playback.RightPlayerId
                ? _combatRightHeader
                : null;
        if (damagedHeader is null)
            return;

        SpawnCombatFloatingValue(damagedHeader, $"-{playback.Settlement.PlayerDamage}", "HealthValueLabel");
        damagedHeader.PivotOffset = damagedHeader.Size * 0.5f;
        damagedHeader.Scale = new Vector2(1.15f, 1.15f);
        damagedHeader.CreateTween()
            .TweenProperty(damagedHeader, "scale", Vector2.One, ResolvePresentationMetric(
                ModThemeMetricKeys.Motion.Cue.PulseDurationSeconds,
                0.05f,
                5.0f))
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }

    private string CombatOutcomeTitle(CombatPlaybackState playback)
    {
        if (_session is null)
            return Text("ui.combatDrawTitle");

        PlayerId? winner = playback.Settlement.EliminatedOpponentWon
            ? playback.Settlement.EliminatedOpponentSourcePlayerId
            : playback.Settlement.WinnerPlayerId;
        if (winner is null)
            return Text("ui.combatDrawTitle");
        return winner == _session.HumanPlayerId
            ? Text("ui.combatVictory")
            : Text("ui.combatDefeat");
    }

    private string CombatSettlementDamageText(CombatPlaybackState playback)
    {
        if (playback.Settlement.DamagedPlayerId is not PlayerId damaged)
            return Text("ui.combatNoHeroDamage");

        return Text(
            "ui.combatHeroDamage",
            ("player", damaged.Value),
            ("damage", playback.Settlement.PlayerDamage),
            ("health", Term("health")),
            ("absorbed", playback.Settlement.ArmorAbsorbed),
            ("armor", Term("armor")));
    }

    private Color ResolveCombatThemeColor(string token, string fallbackHtml)
    {
        if (_modTheme?.TryResolveColor(token, out var resolved) == true)
            return Color.FromHtml(resolved);
        return Color.FromHtml(fallbackHtml);
    }

    private void ResetCombatJuiceState()
    {
        _combatCardsByInstance.Clear();
        _combatPendingReflowPlayerId = null;
        _combatPendingReflowPosition = -1;
        _combatJuiceEventSequence = -1;
        _combatSettlementAnimatedSequence = -1;
        if (_combatSettlementBanner is not null)
            _combatSettlementBanner.Visible = false;
    }
}
