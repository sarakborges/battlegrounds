using System.Text.Json;
using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class ModSelectionController : Control
{
    [Export] public string ModsRoot { get; set; } = "res://../../mods";
    [Export] public string GameplayScenePath { get; set; } = "res://Scenes/Main.tscn";
    [Export] public int Seed { get; set; }
    [Export] public int ParticipantCount { get; set; } = 4;
    [Export] public string Locale { get; set; } = string.Empty;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private IReadOnlyList<ModDiscoveryEntry> _availableMods = Array.Empty<ModDiscoveryEntry>();
    private string? _discoveryError;
    private ModThemeCatalog _engineTheme = null!;
    private Control? _webUiHost;
    private bool _webUiReady;

    public override void _Ready()
    {
        _engineTheme = new ModThemeLoader().LoadEngineDefault();
        DiscoverMods();
        InitializeWebUi();
    }

    private void DiscoverMods()
    {
        try
        {
            _availableMods = new ModDiscovery().Discover(ResolveModsRoot());
            _discoveryError = null;
        }
        catch (Exception exception)
        {
            _availableMods = Array.Empty<ModDiscoveryEntry>();
            _discoveryError = exception.Message;
        }
    }

    private void InitializeWebUi()
    {
        var scene = GD.Load<PackedScene>("res://webui/WebUiHost.tscn")
            ?? throw new InvalidOperationException("Web UI host scene was not found.");

        _webUiHost = scene.Instantiate<Control>();
        _webUiHost.Connect("web_ready", Callable.From(OnWebUiReady));
        _webUiHost.Connect("web_message", Callable.From<string>(OnWebUiMessage));
        _webUiHost.Connect("web_unavailable", Callable.From<string>(OnWebUiUnavailable));
        AddChild(_webUiHost);
    }

    private void OnWebUiReady()
    {
        _webUiReady = true;
        PushState();
    }

    private void OnWebUiUnavailable(string reason)
    {
        _webUiReady = false;
        GD.PushError($"Mod selection Web UI unavailable: {reason}");
    }

    private void OnWebUiMessage(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            var type = RequiredString(root, "type");

            switch (type)
            {
                case "request-state":
                    PushState();
                    break;
                case "refresh-mods":
                    DiscoverMods();
                    PushState();
                    break;
                case "select-mod":
                    StartGame(RequiredString(root, "directoryName"));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported mod-selection message type '{type}'.");
            }
        }
        catch (Exception exception)
        {
            Send("error", new { message = exception.Message });
        }
    }

    private void PushState() => Send("state", new
    {
        status = "mod-selection",
        discoveryError = _discoveryError,
        mods = _availableMods.Select(entry => new
        {
            directoryName = entry.DirectoryName,
            id = string.IsNullOrWhiteSpace(entry.Summary.Id) ? entry.DirectoryName : entry.Summary.Id,
            name = entry.DisplayName,
            schemaVersion = entry.Summary.SchemaVersion,
            isValid = entry.IsValid,
            errorCount = entry.Validation.ErrorCount,
            issues = entry.Validation.Issues.Select(issue => new
            {
                issue.Code,
                issue.File,
                issue.Path,
                issue.Message,
            }).ToArray(),
        }).ToArray(),
        theme = BuildThemeState(_engineTheme),
    });

    private void StartGame(string directoryName)
    {
        var selected = _availableMods.SingleOrDefault(entry =>
            string.Equals(entry.DirectoryName, directoryName, StringComparison.Ordinal));
        if (selected is null)
            throw new InvalidOperationException($"Unknown mod directory '{directoryName}'. Refresh the mod list and try again.");
        if (!selected.IsValid)
            throw new InvalidOperationException($"Mod '{selected.DisplayName}' is invalid and cannot be started.");

        var packedScene = ResourceLoader.Load<PackedScene>(GameplayScenePath)
            ?? throw new InvalidOperationException($"Gameplay scene '{GameplayScenePath}' could not be loaded.");
        var game = packedScene.Instantiate<Main>();
        game.ModPath = CombinePath(ModsRoot, selected.DirectoryName);
        game.Seed = Seed;
        game.ParticipantCount = ParticipantCount;
        game.Locale = Locale;

        var parent = GetParent() ?? throw new InvalidOperationException("Mod selection has no scene-tree parent.");
        parent.AddChild(game);
        QueueFree();
    }

    private string ResolveModsRoot()
    {
        if (ModsRoot.StartsWith("res://", StringComparison.Ordinal))
        {
            var projectRoot = ProjectSettings.GlobalizePath("res://");
            var relative = ModsRoot["res://".Length..].Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, relative));
        }

        if (ModsRoot.StartsWith("user://", StringComparison.Ordinal))
            return Path.GetFullPath(ProjectSettings.GlobalizePath(ModsRoot));

        return Path.GetFullPath(ModsRoot);
    }

    private void Send(string type, object payload)
    {
        if (!_webUiReady || _webUiHost is null)
            return;

        var json = JsonSerializer.Serialize(new { type, payload }, JsonOptions);
        _webUiHost.Call("send_message", json);
    }

    private static object BuildThemeState(ModThemeCatalog theme) => new
    {
        version = theme.Version,
        colors = theme.Colors,
        fonts = theme.Fonts,
        fontSizes = theme.FontSizes,
        spacing = theme.Spacing,
        radii = theme.Radii,
        metrics = theme.Metrics,
        components = theme.Components,
        screens = theme.Screens,
    };

    private static string RequiredString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Web UI message requires string '{property}'.");
        return value.GetString()!;
    }

    private static string CombinePath(string root, string child) =>
        $"{root.TrimEnd('/', '\\')}/{child}";
}
