using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Choices;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
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
        ApplyStaticPresentationText();
        AppendLog($"Presentation locale: {_presentationText.Locale}.");
    }

    private string Text(string key) =>
        _presentationText?.Get(key) ?? key;

    private string Term(string key) =>
        _presentationText?.Term(key) ?? key;

    private string Text(string key, params (string Name, object? Value)[] values) =>
        _presentationText?.Format(key, values) ?? key;

    private string EntityName(ModPresentationEntityKind kind, string id) =>
        _presentationText?.EntityName(kind, id) ?? id;

    private string LeaderName(LeaderId id) => EntityName(ModPresentationEntityKind.Leader, id.Value);
    private string PowerName(PowerId id) => EntityName(ModPresentationEntityKind.Power, id.Value);
    private string UnitName(UnitId id) => EntityName(ModPresentationEntityKind.Unit, id.Value);
    private string ActionName(ActionId id) => EntityName(ModPresentationEntityKind.Action, id.Value);
    private string CombineName(UnitCombineId id) => EntityName(ModPresentationEntityKind.Combine, id.Value);

    private string PlayableName(PlayableKind kind, string id) => kind switch
    {
        PlayableKind.Unit => EntityName(ModPresentationEntityKind.Unit, id),
        PlayableKind.Action => EntityName(ModPresentationEntityKind.Action, id),
        _ => id,
    };

    private string PhaseText(MatchPhase phase) => phase switch
    {
        MatchPhase.Preparation => Term("preparation"),
        MatchPhase.Combat => Term("combat"),
        MatchPhase.Setup => "Setup",
        MatchPhase.Finished => "Finished",
        _ => phase.ToString(),
    };

    private string PlayableKindText(PlayableKind kind) => kind switch
    {
        PlayableKind.Unit => Term("unit"),
        PlayableKind.Action => Term("action"),
        _ => kind.ToString(),
    };

    private string ChoiceKindText(PendingChoiceKind kind) => kind switch
    {
        PendingChoiceKind.Unit => Term("unit"),
        PendingChoiceKind.Action => Term("action"),
        _ => kind.ToString(),
    };

    private void ApplyStaticPresentationText()
    {
        if (_presentationText is null) return;

        GetNode<Label>("Margin/Root/PreparationPanel/Columns/OfferColumn/Title").Text = Term("offer");
        GetNode<Label>("Margin/Root/PreparationPanel/Columns/ReserveColumn/Title").Text = Term("reserve");
        GetNode<Label>("Margin/Root/PreparationPanel/Columns/FieldColumn/Title").Text = Term("field");
        GetNode<Label>("Margin/Root/LogTitle").Text = Text("ui.sessionLog");

        _confirmInteractionButton.Text = Text("ui.confirm");
        _cancelInteractionButton.Text = Text("ui.cancel");
        _refreshButton.Text = Text("ui.refresh");
        _upgradeButton.Text = Text("ui.upgradeTier", ("tier", Term("tier")));
        _powerButton.Text = Text("ui.usePower", ("power", Term("power")));
        _combineButton.Text = Text("ui.combineUnits", ("units", Term("units")));
        _endPreparationButton.Text = Text("ui.endPreparation", ("preparation", Term("preparation")));
    }
}
