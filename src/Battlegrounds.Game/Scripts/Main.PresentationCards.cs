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
        if (selected)
            card.SetSelected(true);
        card.Pressed += () => PlayPresentationCue(
            card,
            entityKind,
            entityId,
            ModPresentationCueRoles.UiSelect,
            ModPresentationAnimation.Pulse,
            0.18);
        return card;
    }

    private void PlayPresentationCue(
        Control target,
        ModPresentationEntityKind entityKind,
        string entityId,
        string role,
        ModPresentationAnimation fallbackAnimation = ModPresentationAnimation.Pulse,
        double fallbackDurationSeconds = 0.18) =>
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
        _presentationCues ??= new ModPresentationCuePlayer(this, ProjectSettings.GlobalizePath(ModPath));
}
