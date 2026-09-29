using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Playables;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    [Export] public string Locale { get; set; } = string.Empty;

    private ModPresentationText? _presentationText;

    private void InitializePresentation(ModPackage mod)
    {
        ArgumentNullException.ThrowIfNull(mod);
        var requestedLocale = string.IsNullOrWhiteSpace(Locale) ? TranslationServer.GetLocale() : Locale;
        _presentationText = mod.Presentation.Resolve(requestedLocale);
        InitializeTheme();
    }

    private string Text(string key) => _presentationText?.Get(key) ?? key;

    private string Term(string key) => _presentationText?.Term(key) ?? key;

    private string Text(string key, params (string Name, object? Value)[] values) =>
        _presentationText?.Format(key, values) ?? key;

    private string EntityName(ModPresentationEntityKind kind, string id) =>
        _presentationText?.EntityName(kind, id) ?? id;

    private string? EntityDescription(ModPresentationEntityKind kind, string id)
    {
        if (_presentationText is null)
            return null;
        return _presentationText.TryEntityDescription(kind, id, out var value) ? value : null;
    }

    private string LeaderName(LeaderId id) => EntityName(ModPresentationEntityKind.Leader, id.Value);
    private string UnitName(UnitId id) => EntityName(ModPresentationEntityKind.Unit, id.Value);
    private string ActionName(ActionId id) => EntityName(ModPresentationEntityKind.Action, id.Value);
    private string PowerName(PowerId id) => EntityName(ModPresentationEntityKind.Power, id.Value);

    private string? LeaderDescription(LeaderId id) => EntityDescription(ModPresentationEntityKind.Leader, id.Value);
    private string? UnitDescription(UnitId id) => EntityDescription(ModPresentationEntityKind.Unit, id.Value);
    private string? ActionDescription(ActionId id) => EntityDescription(ModPresentationEntityKind.Action, id.Value);
    private string? PowerDescription(PowerId id) => EntityDescription(ModPresentationEntityKind.Power, id.Value);

    private string PlayableName(PlayableKind kind, string id) => kind switch
    {
        PlayableKind.Unit => EntityName(ModPresentationEntityKind.Unit, id),
        PlayableKind.Action => EntityName(ModPresentationEntityKind.Action, id),
        _ => id,
    };

    private string? PlayableDescription(PlayableKind kind, string id) => kind switch
    {
        PlayableKind.Unit => EntityDescription(ModPresentationEntityKind.Unit, id),
        PlayableKind.Action => EntityDescription(ModPresentationEntityKind.Action, id),
        _ => null,
    };
}
