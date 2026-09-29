using System.Text.Json;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Choices;
using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private static readonly JsonSerializerOptions WebJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private Control? _webUiHost;
    private bool _webUiInitializationAttempted;
    private bool _webUiReady;

    public override void _Process(double delta)
    {
        if (_webUiInitializationAttempted)
            return;

        _webUiInitializationAttempted = true;
        InitializeWebUi();
    }

    private void InitializeWebUi()
    {
        try
        {
            var scene = GD.Load<PackedScene>("res://WebUI/WebUiHost.tscn");
            if (scene is null)
            {
                AppendLog("Web UI host scene was not found; keeping the Godot UI.");
                return;
            }

            _webUiHost = scene.Instantiate<Control>();
            _webUiHost.Connect("web_ready", Callable.From(OnWebUiReady));
            _webUiHost.Connect("web_message", Callable.From<string>(OnWebUiMessage));
            _webUiHost.Connect("web_unavailable", Callable.From<string>(OnWebUiUnavailable));
            AddChild(_webUiHost);
            MoveChild(_webUiHost, GetChildCount() - 1);
        }
        catch (Exception exception)
        {
            AppendLog($"Web UI initialization failed: {exception.Message}. Keeping the Godot UI.");
        }
    }

    private void OnWebUiReady()
    {
        _webUiReady = true;
        AppendLog("Web UI connected through Godot CEF.");
        PushWebUiState();
    }

    private void OnWebUiUnavailable(string reason)
    {
        _webUiReady = false;
        AppendLog($"Web UI unavailable: {reason}. Keeping the Godot UI.");
    }

    private void OnWebUiMessage(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
                throw new InvalidOperationException("Web UI message is missing 'type'.");

            var type = typeElement.GetString() ?? string.Empty;
            switch (type)
            {
                case "request-state":
                    break;
                case "select-leader":
                    SelectLeader(new LeaderId(RequiredString(root, "leaderId")));
                    break;
                case "acquire":
                    ExecuteHuman(player => new AcquirePlayableCommand(player.Id, RequiredInt(root, "slot")));
                    break;
                case "deploy":
                    TryDeployUnit(RequiredInt(root, "slot"));
                    Render();
                    break;
                case "play-action":
                    TryPlayAction(RequiredInt(root, "slot"));
                    Render();
                    break;
                case "release":
                    ExecuteHuman(player => new ReleaseUnitCommand(player.Id, RequiredInt(root, "slot")));
                    break;
                case "refresh":
                    ExecuteHuman(player => new RefreshOfferCommand(player.Id));
                    break;
                case "upgrade":
                    ExecuteHuman(player => new UpgradeTierCommand(player.Id));
                    break;
                case "toggle-freeze":
                    ToggleFreeze();
                    Render();
                    break;
                case "use-power":
                    TryUsePower();
                    Render();
                    break;
                case "end-preparation":
                    ExecuteHuman(player => new EndPreparationCommand(player.Id));
                    break;
                case "resolve-choice":
                    ResolveWebChoice(RequiredInt(root, "optionIndex"));
                    break;
                case "select-target":
                    SubmitSelectedTarget(new UnitInstanceId(RequiredLong(root, "unitInstanceId")));
                    break;
                case "begin-combine":
                    BeginCombineSelection();
                    break;
                case "select-combine-recipe":
                    SelectCombineRecipe(new UnitCombineId(RequiredString(root, "combineId")));
                    break;
                case "toggle-combine-unit":
                    ToggleWebCombineUnit(new UnitInstanceId(RequiredLong(root, "unitInstanceId")));
                    break;
                case "confirm-interaction":
                    ConfirmInteraction();
                    break;
                case "cancel-interaction":
                    CancelInteraction();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported Web UI message type '{type}'.");
            }
        }
        catch (Exception exception)
        {
            AppendLog($"Web UI command failed: {exception.Message}");
            SendWebUi("error", new { message = exception.Message });
        }
        finally
        {
            PushWebUiState();
        }
    }

    private void ResolveWebChoice(int optionIndex)
    {
        if (!TryGetHuman(out var human) || human.PendingChoice is null)
            throw new InvalidOperationException("There is no pending choice to resolve.");

        ResolveChoice(human.PendingChoice, optionIndex);
    }

    private void ToggleWebCombineUnit(UnitInstanceId unitInstanceId)
    {
        if (!TryGetHuman(out var human) || _session is null ||
            _interaction.Kind != PresentationInteractionKind.CombineComponents ||
            _interaction.CombineId is not UnitCombineId combineId)
        {
            throw new InvalidOperationException("No combine component selection is active.");
        }

        var unit = human.Reserve.Concat(human.Field).FirstOrDefault(value => value.Id == unitInstanceId)
            ?? throw new InvalidOperationException($"Unit instance '{unitInstanceId}' is not owned by the human player.");
        var definition = _session.Mod.Combines.GetRequired(combineId);
        ToggleCombineUnit(unit, definition);
    }

    private void PushWebUiState()
    {
        if (!_webUiReady || _webUiHost is null)
            return;

        SendWebUi("state", BuildWebUiState());
    }

    private void SendWebUi(string type, object payload)
    {
        if (!_webUiReady || _webUiHost is null)
            return;

        var json = JsonSerializer.Serialize(new { type, payload }, WebJsonOptions);
        _webUiHost.Call("send_message", json);
    }

    private object BuildWebUiState()
    {
        if (_session is null)
        {
            return new
            {
                status = "loading",
                mod = (object?)null,
                theme = BuildWebThemeState(),
            };
        }

        if (!_session.HasStarted)
        {
            var leaders = _session.LeaderSelection.GetOffer(_session.HumanPlayerId)
                .Select(id =>
                {
                    var definition = _session.Mod.Leaders.GetRequired(id);
                    return new
                    {
                        id = id.Value,
                        name = LeaderName(id),
                        healthModifier = definition.HealthModifier,
                        armor = definition.StartingArmor,
                    };
                })
                .ToArray();

            return new
            {
                status = "leader-selection",
                mod = new { id = _session.Mod.Id, name = _session.Mod.Name },
                seed = Seed,
                labels = BuildWebLabels(),
                leaders,
                theme = BuildWebThemeState(),
            };
        }

        var match = _session.Match ?? throw new InvalidOperationException("Started session has no match.");
        if (!match.TryGetPlayer(_session.HumanPlayerId, out var human))
            throw new InvalidOperationException("Human player is missing from the active match.");

        var canAct = match.Phase == MatchPhase.Preparation &&
                     !human.IsEliminated &&
                     !human.IsReadyForCombat &&
                     _session.CurrentPreparationPlayerId == human.Id;

        var offer = human.PlayableOffer.Select(entry => new
        {
            slot = entry.Slot,
            kind = entry.Kind.ToString().ToLowerInvariant(),
            id = entry.Id,
            name = PlayableName(entry.Kind, entry.Id),
            tier = entry.Tier,
            cost = human.GetAcquireCost(entry, _session.Mod.PreparationRules),
            frozen = entry.IsFrozen,
        }).ToArray();

        var reserve = human.PlayableReserve.Select(entry => new
        {
            slot = entry.Slot,
            kind = entry.Kind.ToString().ToLowerInvariant(),
            id = entry.DefinitionId,
            name = PlayableName(entry.Kind, entry.DefinitionId),
            unitInstanceId = entry.Unit?.Id.Value,
            tier = entry.Unit?.Definition.Tier,
            attack = entry.Unit?.Attack,
            health = entry.Unit?.Health,
            selectedForCombine = entry.Unit is not null && _interaction.IsSelected(entry.Unit.Id),
        }).ToArray();

        var field = human.Field.Select((unit, slot) => new
        {
            slot,
            id = unit.Definition.Id.Value,
            unitInstanceId = unit.Id.Value,
            name = UnitName(unit.Definition.Id),
            tier = unit.Definition.Tier,
            attack = unit.Attack,
            health = unit.Health,
            selectedForCombine = _interaction.IsSelected(unit.Id),
        }).ToArray();

        var players = match.Players.OrderBy(player => player.Id.Value).Select(player => new
        {
            id = player.Id.Value,
            human = player.Id == _session.HumanPlayerId,
            health = player.Health,
            armor = player.Leader?.Armor ?? 0,
            tier = player.Tier,
            eliminated = player.IsEliminated,
            ready = player.IsReadyForCombat,
            leader = player.Leader is null ? null : LeaderName(player.Leader.Definition.Id),
        }).ToArray();

        return new
        {
            status = match.Phase == MatchPhase.Finished ? "finished" : "match",
            mod = new { id = _session.Mod.Id, name = _session.Mod.Name },
            seed = Seed,
            phase = match.Phase.ToString().ToLowerInvariant(),
            round = match.Round,
            canAct,
            currentPreparationPlayerId = _session.CurrentPreparationPlayerId?.Value,
            labels = BuildWebLabels(),
            human = new
            {
                id = human.Id.Value,
                health = human.Health,
                armor = human.Leader?.Armor ?? 0,
                resource = human.Resource,
                tier = human.Tier,
                upgradeCost = human.UpgradeCost,
                offerFrozen = human.IsOfferFrozen,
                ready = human.IsReadyForCombat,
                eliminated = human.IsEliminated,
                power = human.Leader?.CurrentPowerId?.Value,
            },
            players,
            offer,
            reserve,
            field,
            pendingChoice = BuildWebPendingChoice(human.PendingChoice),
            interaction = BuildWebInteraction(match, human),
            theme = BuildWebThemeState(),
        };
    }

    private object? BuildWebPendingChoice(PendingChoice? choice)
    {
        if (choice is null)
            return null;

        return choice switch
        {
            PendingUnitChoice unitChoice => new
            {
                kind = "unit",
                options = unitChoice.Options.Select((option, index) => new
                {
                    index,
                    id = option.Id.Value,
                    name = UnitName(option.Id),
                    tier = option.Tier,
                    attack = option.Attack,
                    health = option.Health,
                }).ToArray(),
            },
            PendingActionChoice actionChoice => new
            {
                kind = "action",
                options = actionChoice.Options.Select((option, index) => new
                {
                    index,
                    id = option.Id.Value,
                    name = ActionName(option.Id),
                    tier = option.Tier,
                    cost = option.Cost,
                }).ToArray(),
            },
            _ => new { kind = choice.Kind.ToString().ToLowerInvariant(), options = Array.Empty<object>() },
        };
    }

    private object? BuildWebInteraction(MatchState match, PlayerState human)
    {
        if (!_interaction.IsActive)
            return null;

        if (_interaction.Kind == PresentationInteractionKind.CombineRecipe)
        {
            return new
            {
                kind = "combine-recipe",
                recipes = GetAvailableCombines(human).Select(definition => new
                {
                    id = definition.Id.Value,
                    name = CombineName(definition.Id),
                    requiredCopies = definition.RequiredCopies,
                    source = UnitName(definition.SourceUnitId),
                    result = UnitName(definition.ResultUnitId),
                }).ToArray(),
            };
        }

        if (_interaction.Kind == PresentationInteractionKind.CombineComponents &&
            _interaction.CombineId is UnitCombineId combineId)
        {
            var definition = _session!.Mod.Combines.GetRequired(combineId);
            return new
            {
                kind = "combine-components",
                combineId = combineId.Value,
                name = CombineName(combineId),
                requiredCopies = definition.RequiredCopies,
                selected = _interaction.SelectedUnits.Count,
            };
        }

        var candidates = GetWebTargetCandidates(match, human);
        return new
        {
            kind = "target",
            source = _interaction.Kind.ToString().ToLowerInvariant(),
            zone = _interaction.TargetZone.ToString().ToLowerInvariant(),
            candidates,
        };
    }

    private object[] GetWebTargetCandidates(MatchState match, PlayerState human)
    {
        IEnumerable<(PlayerId OwnerId, UnitInstance Unit)> candidates;
        if (_interaction.TargetZone == EffectTargetZone.Reserve)
        {
            UnitInstanceId? excluded = null;
            if (_interaction.Kind == PresentationInteractionKind.DeployTarget &&
                _interaction.UnitReserveSlot is int reserveSlot && reserveSlot >= 0 && reserveSlot < human.Reserve.Count)
            {
                excluded = human.Reserve[reserveSlot].Id;
            }

            candidates = human.Reserve
                .Where(unit => unit.Id != excluded)
                .Select(unit => (human.Id, unit));
        }
        else
        {
            candidates = match.Players
                .OrderBy(player => player.Id.Value)
                .SelectMany(player => player.Field.Select(unit => (player.Id, unit)));
        }

        return candidates.Select(candidate => (object)new
        {
            ownerId = candidate.OwnerId.Value,
            unitInstanceId = candidate.Unit.Id.Value,
            id = candidate.Unit.Definition.Id.Value,
            name = UnitName(candidate.Unit.Definition.Id),
            attack = candidate.Unit.Attack,
            health = candidate.Unit.Health,
        }).ToArray();
    }

    private object BuildWebLabels() => new
    {
        unit = Term("unit"),
        units = Term("units"),
        action = Term("action"),
        actions = Term("actions"),
        leader = Term("leader"),
        power = Term("power"),
        health = Term("health"),
        armor = Term("armor"),
        resource = Term("resource"),
        offer = Term("offer"),
        tier = Term("tier"),
        reserve = Term("reserve"),
        field = Term("field"),
        round = Term("round"),
        preparation = Term("preparation"),
        combat = Term("combat"),
        refresh = Text("ui.refresh"),
        upgrade = Text("ui.upgradeTier", ("tier", Term("tier"))),
        freeze = Text("ui.freezeOffer", ("offer", Term("offer"))),
        unfreeze = Text("ui.unfreezeOffer", ("offer", Term("offer"))),
        usePower = Text("ui.usePower", ("power", Term("power"))),
        combine = Text("ui.combineUnits", ("units", Term("units"))),
        endPreparation = Text("ui.endPreparation", ("preparation", Term("preparation"))),
        acquire = Term("acquire"),
        release = Term("release"),
        deploy = Text("ui.deploy"),
        play = Text("ui.play"),
        chooseLeader = Text("ui.chooseLeader", ("leader", Term("leader"))),
        confirm = Text("ui.confirm"),
        cancel = Text("ui.cancel"),
    };

    private object? BuildWebThemeState()
    {
        if (_modTheme is null)
            return null;

        return new
        {
            version = _modTheme.Version,
            colors = _modTheme.Colors,
            fonts = _modTheme.Fonts,
            fontSizes = _modTheme.FontSizes,
            spacing = _modTheme.Spacing,
            radii = _modTheme.Radii,
            metrics = _modTheme.Metrics,
            components = _modTheme.Components,
            screens = _modTheme.Screens,
        };
    }

    private static string RequiredString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Web UI message requires string '{property}'.");
        return value.GetString()!;
    }

    private static int RequiredInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || !value.TryGetInt32(out var result))
            throw new InvalidOperationException($"Web UI message requires integer '{property}'.");
        return result;
    }

    private static long RequiredLong(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || !value.TryGetInt64(out var result))
            throw new InvalidOperationException($"Web UI message requires integer '{property}'.");
        return result;
    }
}
