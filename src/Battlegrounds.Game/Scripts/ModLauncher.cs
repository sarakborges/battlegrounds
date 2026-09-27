using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class ModLauncher : Control
{
    private const string LauncherRevision = "launcher-r6";

    [Export] public string ModsRoot { get; set; } = "res://../../mods";
    [Export] public string GameplayScenePath { get; set; } = "res://Scenes/Main.tscn";
    [Export] public int Seed { get; set; } = 20260927;
    [Export] public int ParticipantCount { get; set; } = 4;
    [Export] public string Locale { get; set; } = string.Empty;

    private readonly List<(Button Button, ModDiscoveryEntry Entry)> _candidateButtons = new();
    private Label _status = null!;
    private VBoxContainer _modButtons = null!;
    private RichTextLabel _diagnostics = null!;
    private Button _refreshButton = null!;
    private bool _autoStartAttempted;

    public override async void _Ready()
    {
        _status = GetNode<Label>("%Status");
        _modButtons = GetNode<VBoxContainer>("%ModButtons");
        _diagnostics = GetNode<RichTextLabel>("%Diagnostics");
        _refreshButton = GetNode<Button>("%RefreshButton");
        _refreshButton.Pressed += DiscoverMods;

        _status.Text = $"Discovering mods... [{LauncherRevision}]";
        GD.Print($"[ModLauncher] C# launcher started ({LauncherRevision}).");

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        DiscoverMods();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouse ||
            mouse.ButtonIndex != MouseButton.Left ||
            !mouse.Pressed)
        {
            return;
        }

        GD.Print($"[ModLauncher] Raw left click at {mouse.Position} ({LauncherRevision}).");
        _status.Text = $"Raw click at {mouse.Position}. [{LauncherRevision}]";

        foreach (var candidate in _candidateButtons)
        {
            if (!GodotObject.IsInstanceValid(candidate.Button) || !candidate.Button.Visible)
                continue;

            if (!candidate.Button.GetGlobalRect().HasPoint(mouse.Position))
                continue;

            GD.Print($"[ModLauncher] Raw click matched '{candidate.Entry.DisplayName}'.");
            _status.Text = $"Raw click matched {candidate.Entry.DisplayName}. [{LauncherRevision}]";
            GetViewport().SetInputAsHandled();
            SelectCandidate(candidate.Entry);
            return;
        }
    }

    private async void DiscoverMods()
    {
        _candidateButtons.Clear();
        ClearChildren(_modButtons);
        _diagnostics.Text = string.Empty;
        _status.Text = $"Discovering mods... [{LauncherRevision}]";

        try
        {
            var modsRoot = ResolveModsRoot();
            GD.Print($"[ModLauncher] Discovering mods in '{modsRoot}'.");

            var entries = new ModDiscovery().Discover(modsRoot);
            _status.Text = entries.Count == 0
                ? $"No mod packages found in {ModsRoot}. [{LauncherRevision}]"
                : $"Choose a mod package ({entries.Count} found). [{LauncherRevision}]";

            foreach (var entry in entries)
                AddCandidate(entry);

            GD.Print($"[ModLauncher] Discovery complete: {entries.Count} package(s).");

            if (_autoStartAttempted)
                return;

            var autoStart = entries.FirstOrDefault(entry =>
                                entry.IsValid && entry.DirectoryName.Equals("warbands", StringComparison.OrdinalIgnoreCase))
                            ?? entries.FirstOrDefault(entry => entry.IsValid);
            if (autoStart is null)
                return;

            _autoStartAttempted = true;
            _status.Text = $"Auto-starting {autoStart.DisplayName} in 1 second... [{LauncherRevision}]";
            GD.Print($"[ModLauncher] Diagnostic auto-start armed for '{autoStart.DisplayName}'.");

            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            if (!IsInsideTree())
                return;

            _status.Text = $"Auto-starting {autoStart.DisplayName} now... [{LauncherRevision}]";
            GD.Print($"[ModLauncher] Diagnostic auto-start firing for '{autoStart.DisplayName}'.");
            StartGame(autoStart);
        }
        catch (Exception exception)
        {
            _status.Text = $"Mod discovery failed. [{LauncherRevision}]";
            _diagnostics.Text = exception.ToString();
            GD.PushError($"[ModLauncher] Mod discovery failed: {exception}");
        }
    }

    private string ResolveModsRoot()
    {
        if (ModsRoot.StartsWith("res://", StringComparison.Ordinal))
        {
            var projectRoot = ProjectSettings.GlobalizePath("res://");
            var relative = ModsRoot["res://".Length..]
                .Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, relative));
        }

        if (ModsRoot.StartsWith("user://", StringComparison.Ordinal))
            return Path.GetFullPath(ProjectSettings.GlobalizePath(ModsRoot));

        return Path.GetFullPath(ModsRoot);
    }

    private void AddCandidate(ModDiscoveryEntry entry)
    {
        var id = string.IsNullOrWhiteSpace(entry.Summary.Id) ? entry.DirectoryName : entry.Summary.Id;
        var state = entry.IsValid ? "Ready" : $"Invalid · {entry.Validation.ErrorCount} error(s)";
        var button = new Button
        {
            Text = $"{entry.DisplayName}  ·  {id}  ·  {state}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.All,
            TooltipText = entry.IsValid
                ? "Start a local single-player match with this mod."
                : "Inspect validation diagnostics for this mod.",
        };

        button.ButtonDown += () =>
        {
            _status.Text = $"ButtonDown {entry.DisplayName}. [{LauncherRevision}]";
            GD.Print($"[ModLauncher] ButtonDown for '{entry.DisplayName}' ({entry.DirectoryName}).");
        };
        button.Pressed += () => SelectCandidate(entry);
        _modButtons.AddChild(button);
        _candidateButtons.Add((button, entry));
    }

    private void SelectCandidate(ModDiscoveryEntry entry)
    {
        _status.Text = $"Selected {entry.DisplayName}. [{LauncherRevision}]";
        GD.Print($"[ModLauncher] Selected '{entry.DisplayName}' ({entry.DirectoryName}), valid={entry.IsValid}.");

        if (!entry.IsValid)
        {
            ShowDiagnostics(entry);
            return;
        }

        StartGame(entry);
    }

    private void ShowDiagnostics(ModDiscoveryEntry entry)
    {
        var lines = entry.Validation.Issues.Select(issue =>
            $"[{issue.Code}] {issue.File} {issue.Path}\n{issue.Message}");
        _diagnostics.Text = $"{entry.DisplayName}\n{entry.DirectoryName}\n\n{string.Join("\n\n", lines)}";
    }

    private void StartGame(ModDiscoveryEntry entry)
    {
        _status.Text = $"Starting {entry.DisplayName}... [{LauncherRevision}]";
        _diagnostics.Text = string.Empty;

        try
        {
            var packedScene = ResourceLoader.Load<PackedScene>(GameplayScenePath)
                ?? throw new InvalidOperationException($"Gameplay scene '{GameplayScenePath}' could not be loaded.");
            var game = packedScene.Instantiate<Main>();

            game.ModPath = CombineGodotPath(ModsRoot, entry.DirectoryName);
            game.Seed = Seed;
            game.ParticipantCount = ParticipantCount;
            game.Locale = Locale;

            GD.Print($"[ModLauncher] Starting '{entry.DisplayName}' from '{game.ModPath}'.");

            var tree = GetTree();
            tree.Root.AddChild(game);
            tree.CurrentScene = game;
            QueueFree();
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not start {entry.DisplayName}. [{LauncherRevision}]";
            _diagnostics.Text = exception.ToString();
            GD.PushError($"[ModLauncher] Could not start {entry.DisplayName}: {exception}");
        }
    }

    private static string CombineGodotPath(string root, string child) =>
        $"{root.TrimEnd('/', '\\')}/{child}";

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
            child.QueueFree();
    }
}
