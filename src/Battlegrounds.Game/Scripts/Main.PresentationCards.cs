using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Playables;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private ModPresentationTextureStore? _presentationTextures;

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
        return card;
    }

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
}
