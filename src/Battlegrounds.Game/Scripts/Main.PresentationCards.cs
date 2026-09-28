using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Units;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private ModPresentationTextureStore? _presentationTextures;
    private ModPresentationCuePlayer? _presentationCues;

    private PresentationCardButton CreatePresentationCard(
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot,
        string title,
        string subtitle,
        string stats,
        bool disabled = false,
        bool selected = false)
    {
        Texture2D? texture = null;
        PresentationTextures.TryGetImage(entityKind, entityId, slot, out texture);

        string? description = null;
        if (_presentationText is not null &&
            _presentationText.TryEntityDescription(entityKind, entityId, out var localizedDescription))
        {
            description = localizedDescription;
        }

        var card = new PresentationCardButton
        {
            Disabled = disabled,
        };
        card.Configure(title, subtitle, stats, description, texture);
        card.ConfigureIdentity(entityKind, entityId);
        ApplyPresentationCardLayout(card);
        if (selected)
            card.SetSelected(true);
        card.Pressed += () => PlayPresentationCue(
            card,
            entityKind,
            entityId,
            ModPresentationCueRoles.UiSelect,
            ModPresentationAnimation.Pulse,
            ResolvePresentationMetric(ModThemeMetricKeys.Motion.UiSelectDurationSeconds, 0.05f, 5.0f));
        return card;
    }

    internal void ConfigureCompactTokenForParent(
        PresentationCardButton card,
        ModPresentationEntityKind entityKind,
        string entityId)
    {
        if (_session is null || entityKind != ModPresentationEntityKind.Unit)
            return;

        var parent = card.GetParent();
        var parentName = parent?.Name.ToString();
        if (parentName == "OfferButtons")
        {
            var definition = _session.Mod.Units.GetRequired(new UnitId(entityId));
            card.ConfigureToken(
                definition.Tier,
                definition.BaseAttack,
                definition.BaseHealth,
                BuildUnitInspectDetails(definition));
            return;
        }

        if (parentName != "FieldButtons" ||
            parent is null ||
            _session.Match is not { } match ||
            !match.TryGetPlayer(_session.HumanPlayerId, out var human))
        {
            return;
        }

        var fieldIndex = parent.GetChildren()
            .OfType<PresentationCardButton>()
            .Where(candidate => !candidate.IsQueuedForDeletion())
            .TakeWhile(candidate => candidate != card)
            .Count();
        if (fieldIndex < 0 || fieldIndex >= human.Field.Count)
            return;

        var unit = human.Field[fieldIndex];
        if (!string.Equals(unit.Definition.Id.Value, entityId, StringComparison.Ordinal))
            return;

        card.ConfigureToken(
            null,
            unit.Attack,
            unit.Health,
            BuildUnitInspectDetails(unit.Definition, unit));
    }

    private void PlayPresentationCue(
        Control target,
        ModPresentationEntityKind entityKind,
        string entityId,
        string role,
        ModPresentationAnimation fallbackAnimation = ModPresentationAnimation.Pulse,
        double? fallbackDurationSeconds = null) =>
        PresentationCues.Play(target, entityKind, entityId, role, fallbackAnimation, fallbackDurationSeconds);

    private string UnitCardStats(int tier, int attack, int health) =>
        Text(
            "ui.cardUnitStats",
            ("tier", Term("tier")),
            ("tierValue", tier),
            ("attack", attack),
            ("health", Term("health")),
            ("healthValue", health));

    private string ActionCardStats(int tier, int cost) =>
        Text(
            "ui.cardActionStats",
            ("tier", Term("tier")),
            ("tierValue", tier),
            ("cost", cost));

    private string LeaderCardStats(string healthText, int armor) =>
        Text(
            "ui.cardLeaderStats",
            ("healthText", healthText),
            ("armor", Term("armor")),
            ("armorValue", armor));

    private string OfferCardStats(PlayableKind kind, string id, int tier, int cost)
    {
        if (_session is null) return string.Empty;

        return kind switch
        {
            PlayableKind.Unit => BuildUnitDefinitionStats(_session.Mod.Units.GetRequired(new UnitId(id))),
            PlayableKind.Action => ActionCardStats(tier, cost),
            _ => string.Empty,
        };
    }

    private string ReserveCardStats(PlayableKind kind, string definitionId, UnitInstance? unit)
    {
        if (_session is null) return string.Empty;

        return kind switch
        {
            PlayableKind.Unit when unit is not null => UnitCardStats(unit.Definition.Tier, unit.Attack, unit.Health),
            PlayableKind.Action => BuildActionDefinitionStats(definitionId),
            _ => string.Empty,
        };
    }

    private string BuildUnitDefinitionStats(UnitDefinition definition) =>
        UnitCardStats(definition.Tier, definition.BaseAttack, definition.BaseHealth);

    private string BuildUnitInspectDetails(UnitDefinition definition, UnitInstance? unit = null)
    {
        var lines = new List<string>();

        if (definition.Types.Count > 0)
            lines.Add(string.Join(" • ", definition.Types.Select(type => type.Name)));

        var behaviors = unit is null ? definition.Behaviors : unit.Behaviors;
        if (behaviors.Count > 0)
            lines.Add(string.Join(" · ", behaviors.Select(behavior => behavior.Name)));

        if (definition.Tags.Count > 0)
            lines.Add(string.Join(" · ", definition.Tags.Select(tag => tag.Name)));

        if (unit is not null && unit.Modifiers.Count > 0)
        {
            lines.Add(string.Join(
                "\n",
                unit.Modifiers.Select(modifier =>
                    $"{modifier.Key}: {FormatSigned(modifier.AttackDelta)}/{FormatSigned(modifier.HealthDelta)}")));
        }

        return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string FormatSigned(int value) => value > 0 ? $"+{value}" : value.ToString();

    private string BuildActionDefinitionStats(string id)
    {
        if (_session is null) return string.Empty;
        var definition = _session.Mod.Actions.GetRequired(new ActionId(id));
        return ActionCardStats(definition.Tier, definition.Cost);
    }

    private ModPresentationEntityKind PlayableEntityKind(PlayableKind kind) => kind switch
    {
        PlayableKind.Unit => ModPresentationEntityKind.Unit,
        PlayableKind.Action => ModPresentationEntityKind.Action,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported playable kind."),
    };

    private string PlayableAssetSlot(PlayableKind kind) => kind switch
    {
        PlayableKind.Unit or PlayableKind.Action => ModPresentationAssetSlots.Art,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported playable kind."),
    };

    private ModPresentationTextureStore PresentationTextures =>
        _presentationTextures ??= new ModPresentationTextureStore(ProjectSettings.GlobalizePath(ModPath));

    private ModPresentationCuePlayer PresentationCues =>
        _presentationCues ??= new ModPresentationCuePlayer(
            this,
            ProjectSettings.GlobalizePath(ModPath),
            BuildPresentationMotionProfile());

    private ModPresentationMotionProfile BuildPresentationMotionProfile() => new(
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.PulseScale, 0.01f, 4.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.PulseDurationSeconds, 0.05f, 5.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.ShakeRotationDegrees, 0.0f, 180.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.ShakeDurationSeconds, 0.05f, 5.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.LungeScale, 0.01f, 4.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.LungeDurationSeconds, 0.05f, 5.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.FadeScale, 0.01f, 4.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.FadeOpacity, 0.0f, 1.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.FadeDurationSeconds, 0.05f, 5.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.PopScale, 0.01f, 4.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.PopOpacity, 0.0f, 1.0f),
        ResolvePresentationMetric(ModThemeMetricKeys.Motion.Cue.PopDurationSeconds, 0.05f, 5.0f));
}
