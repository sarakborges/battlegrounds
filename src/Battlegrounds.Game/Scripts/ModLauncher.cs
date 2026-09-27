using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class ModLauncher : Control
{
    [Export] public string ModsRoot { get; set; } = "res://../../mods";
    [Export] public string GameplayScenePath { get; set; } = "res://Scenes/Main.tscn";
    [Export] public int Seed { get; set; } = 20260927;
    [Export] public int ParticipantCount { get; set; } = 4;
    [Export] public string Locale { get; set; } = string.Empty;

    private Label _status = null!;
    private VBoxContainer _modButtons = null!;
    private RichTextLabel _diagnostics = null!;
    private Button _refreshButton = null!;

    public override void _Ready()
    {
        _status = GetNode<Label>("%Status");
        _modButtons = GetNode<VBoxContainer>("%ModButtons");
        _diagnostics = GetNode<RichTextLabel>("%Diagnostics");
        _refreshButton = GetNode<Button>("%RefreshButton");
        _refreshButton.Pressed += DiscoverMods;
        DiscoverMods();
    }

    private void DiscoverMods()
    {
        ClearChildren(_modButtons);
        _diagnostics.Text = string.Empty;

        try
        {
            var modsRoot = ResolveModsRoot();
            var entries = new ModDiscovery().Discover(modsRoot);
            _status.Text = entries.Count == 0
                ? $"No mod packages found in {ModsRoot}."
                : $"Choose a mod package ({entries.Count} found).";

            foreach (var entry in entries)
                AddCandidate(entry);
        }
        catch (Exception exception)
        {
            _status.Text = "Mod discovery failed.";
            _diagnostics.Text = exception.Message;
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
            TooltipText = entry.IsValid
                ? "Start a local single-player match with this mod."
                : "Inspect validation diagnostics for this mod.",
        };
        button.Pressed += () => SelectCandidate(entry);
        _modButtons.AddChild(button);
    }

    private void SelectCandidate(ModDiscoveryEntry entry)
    {
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
        try
        {
            var packedScene = ResourceLoader.Load<PackedScene>(GameplayScenePath)
                ?? throw new InvalidOperationException($"Gameplay scene '{GameplayScenePath}' could not be loaded.");
            var game = packedScene.Instantiate<Main>();
            game.ModPath = CombineGodotPath(ModsRoot, entry.DirectoryName);
            game.Seed = Seed;
            game.ParticipantCount = ParticipantCount;
            game.Locale = Locale;

            var parent = GetParent() ?? throw new InvalidOperationException("Launcher has no scene-tree parent.");
            parent.AddChild(game);
            QueueFree();
        }
        catch (Exception exception)
        {
            _status.Text = $"Could not start {entry.DisplayName}.";
            _diagnostics.Text = exception.ToString();
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
