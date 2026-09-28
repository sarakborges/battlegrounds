using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class ModLauncher : Control
{
    private static readonly Color DefaultBackgroundColor = new(0.055f, 0.063f, 0.082f, 1f);

    [Export] public string ModsRoot { get; set; } = "res://../../mods";
    [Export] public string GameplayScenePath { get; set; } = "res://Scenes/Main.tscn";
    [Export] public int Seed { get; set; } = 20260927;
    [Export] public int ParticipantCount { get; set; } = 4;
    [Export] public string Locale { get; set; } = string.Empty;

    private ColorRect _background = null!;
    private Label _status = null!;
    private VBoxContainer _modButtons = null!;
    private RichTextLabel _diagnostics = null!;
    private Button _refreshButton = null!;
    private TextureRect? _themeBackgroundImage;
    private ModThemeBuilder? _themeBuilder;
    private string? _themedDirectoryName;

    public override void _Ready()
    {
        _background = GetNode<ColorRect>("Background");
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
        ResetLauncherTheme();

        try
        {
            var modsRoot = ResolveModsRoot();
            var entries = new ModDiscovery().Discover(modsRoot);
            _status.Text = entries.Count == 0
                ? $"No mod packages found in {ModsRoot}."
                : $"Choose a mod package ({entries.Count} found).";

            foreach (var entry in entries)
                AddCandidate(entry);

            var firstValid = entries.FirstOrDefault(entry => entry.IsValid);
            if (firstValid is not null)
                ApplyCandidateTheme(firstValid);
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
        if (entry.IsValid)
        {
            button.MouseEntered += () => ApplyCandidateTheme(entry);
            button.FocusEntered += () => ApplyCandidateTheme(entry);
        }
        _modButtons.AddChild(button);
    }

    private void ApplyCandidateTheme(ModDiscoveryEntry entry)
    {
        if (!entry.IsValid || string.Equals(_themedDirectoryName, entry.DirectoryName, StringComparison.Ordinal))
            return;

        var modDirectory = Path.Combine(ResolveModsRoot(), entry.DirectoryName);
        var theme = new ModThemeLoader().Load(modDirectory);

        ResetLauncherTheme();
        _themedDirectoryName = entry.DirectoryName;
        if (theme.IsEmpty)
            return;

        _themeBuilder = new ModThemeBuilder(modDirectory, theme);
        Theme = _themeBuilder.Build();
        ApplyLauncherTypography(theme);
        ApplyLauncherScreen(theme);
    }

    private void ApplyLauncherTypography(ModThemeCatalog theme)
    {
        var title = GetNode<Label>("Margin/Root/Title");
        title.ThemeTypeVariation = "TitleLabel";
        if (theme.FontSizes.ContainsKey("title"))
            title.RemoveThemeFontSizeOverride("font_size");

        var subtitle = GetNode<Label>("Margin/Root/Subtitle");
        subtitle.ThemeTypeVariation = "HeadingLabel";
        if (theme.FontSizes.ContainsKey("heading"))
            subtitle.RemoveThemeFontSizeOverride("font_size");

        var diagnosticsTitle = GetNode<Label>("Margin/Root/DiagnosticsTitle");
        diagnosticsTitle.ThemeTypeVariation = "HeadingLabel";
        if (theme.FontSizes.ContainsKey("heading"))
            diagnosticsTitle.RemoveThemeFontSizeOverride("font_size");

        _status.ThemeTypeVariation = "BodyLabel";
        _refreshButton.ThemeTypeVariation = "PrimaryButton";
    }

    private void ApplyLauncherScreen(ModThemeCatalog theme)
    {
        if (!theme.Screens.TryGetValue(ModThemeScreenRoles.Launcher, out var screen))
            return;

        if (theme.TryResolveColor(screen.BackgroundColor, out var color))
            _background.Color = Color.FromHtml(color);

        if (string.IsNullOrWhiteSpace(screen.BackgroundAsset) || _themeBuilder is null)
            return;

        var texture = _themeBuilder.LoadImage(screen.BackgroundAsset);
        if (texture is null)
            return;

        if (_themeBackgroundImage is null)
        {
            _themeBackgroundImage = new TextureRect
            {
                Name = "ThemeBackgroundImage",
                MouseFilter = MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            };
            AddChild(_themeBackgroundImage);
            MoveChild(_themeBackgroundImage, _background.GetIndex() + 1);
            _themeBackgroundImage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        _themeBackgroundImage.Texture = texture;
        _themeBackgroundImage.Visible = true;
    }

    private void ResetLauncherTheme()
    {
        Theme = null;
        _themeBuilder = null;
        _themedDirectoryName = null;
        _background.Color = DefaultBackgroundColor;
        if (_themeBackgroundImage is not null)
            _themeBackgroundImage.Visible = false;

        var title = GetNode<Label>("Margin/Root/Title");
        title.ThemeTypeVariation = StringName.Empty;
        title.AddThemeFontSizeOverride("font_size", 32);

        var subtitle = GetNode<Label>("Margin/Root/Subtitle");
        subtitle.ThemeTypeVariation = StringName.Empty;
        subtitle.AddThemeFontSizeOverride("font_size", 18);

        var diagnosticsTitle = GetNode<Label>("Margin/Root/DiagnosticsTitle");
        diagnosticsTitle.ThemeTypeVariation = StringName.Empty;
        diagnosticsTitle.AddThemeFontSizeOverride("font_size", 18);

        _status.ThemeTypeVariation = StringName.Empty;
        _refreshButton.ThemeTypeVariation = StringName.Empty;
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
