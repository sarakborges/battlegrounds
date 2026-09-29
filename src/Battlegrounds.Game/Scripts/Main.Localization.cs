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

    private string LeaderName(LeaderId id) => EntityName(ModPresentationEntityKind.Leader, id.Value);
    private string UnitName(UnitId id) => EntityName(ModPresentationEntityKind.Unit, id.Value);
    private string ActionName(ActionId id) => EntityName(ModPresentationEntityKind.Action, id.Value);
    private string CombineName(UnitCombineId id) => EntityName(ModPresentationEntityKind.Combine, id.Value);

    private string PlayableName(PlayableKind kind, string id) => kind switch
    {
        PlayableKind.Unit => EntityName(ModPresentationEntityKind.Unit, id),
        PlayableKind.Action => EntityName(ModPresentationEntityKind.Action, id),
        _ => id,
    };
}
