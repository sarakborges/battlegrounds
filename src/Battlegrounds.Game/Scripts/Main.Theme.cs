using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private ModThemeCatalog? _engineTheme;
    private ModThemeCatalog? _modTheme;
    private ModThemeBuilder? _themeBuilder;
    private TextureRect? _themeBackgroundImage;

    public override void _EnterTree()
    {
        Callable.From(ApplyEngineThemeIfUnresolved).CallDeferred();
    }

    private void InitializeTheme()
    {
        var modDirectory = ProjectSettings.GlobalizePath(ModPath);
        var loader = new ModThemeLoader();
        _engineTheme ??= loader.LoadEngineDefault();
        var modTheme = loader.Load(modDirectory);
        _modTheme = ModThemeCatalog.Layer(_engineTheme, modTheme);

        ApplyResolvedTheme(modDirectory);
        AppendLog(modTheme.IsEmpty
            ? $"Presentation theme v{_modTheme.Version} loaded from engine defaults."
            : $"Presentation theme v{_modTheme.Version} loaded from engine defaults + selected mod overrides.");
    }

    private void ApplyEngineThemeIfUnresolved()
    {
        if (_modTheme is not null)
            return;

        _engineTheme ??= new ModThemeLoader().LoadEngineDefault();
        _modTheme = _engineTheme;
        ApplyResolvedTheme(ProjectSettings.GlobalizePath("res://"));
        AppendLog($"Presentation theme v{_modTheme.Version} loaded from engine defaults after mod presentation initialization failed.");
    }

    private void ApplyResolvedTheme(string assetRoot)
    {
        if (_modTheme is null)
            throw new InvalidOperationException("Cannot apply an unresolved presentation theme.");

        _themeBuilder = new ModThemeBuilder(assetRoot, _modTheme);
        Theme = _themeBuilder.Build();
        _endPreparationButton.ThemeTypeVariation = "PrimaryButton";

        ApplyScreenTheme(ModThemeScreenRoles.Preparation);
        ApplySemanticTypography();
        ApplySemanticLayout();
    }

    private void ApplyScreenTheme(string role)
    {
        if (_modTheme is null || _themeBuilder is null || !_modTheme.Screens.TryGetValue(role, out var screen)) return;

        var background = GetNode<ColorRect>("Background");
        if (_modTheme.TryResolveColor(screen.BackgroundColor, out var color))
            background.Color = Color.FromHtml(color);

        Texture2D? texture = null;
        var gameplayScreen = string.Equals(role, ModThemeScreenRoles.Preparation, StringComparison.Ordinal) ||
                             string.Equals(role, ModThemeScreenRoles.Combat, StringComparison.Ordinal);
        if (gameplayScreen)
            PresentationTextures.TryGetBoardImage(out texture);

        if (texture is null && !string.IsNullOrWhiteSpace(screen.BackgroundAsset))
            texture = _themeBuilder.LoadImage(screen.BackgroundAsset);

        if (texture is null)
        {
            if (_themeBackgroundImage is not null) _themeBackgroundImage.Visible = false;
            return;
        }

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
            MoveChild(_themeBackgroundImage, background.GetIndex() + 1);
            _themeBackgroundImage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        _themeBackgroundImage.Texture = texture;
        _themeBackgroundImage.Visible = true;
    }

    private void ApplySemanticTypography()
    {
        ApplyTextRole(_status, "HeadingLabel");
        ApplyTextRole(_leaderPrompt, "HeadingLabel");
        ApplyTextRole(_interactionPrompt, "BodyLabel");
        ApplyTextRole("%HudTierValue", "TierValueLabel");
        ApplyTextRole("%HudHealthValue", "HealthValueLabel");
        ApplyTextRole("%HudArmorValue", "ArmorValueLabel");
        ApplyTextRole("%HudHeroName", "HeroNameLabel");
        ApplyTextRole("%HudResourceValue", "ResourceValueLabel");
    }

    private void ApplyTextRole(string path, string variation)
    {
        var label = GetNodeOrNull<Label>(path);
        if (label is null)
        {
            GD.PushWarning($"Theme typography target '{path}' was not found; skipping it.");
            return;
        }

        ApplyTextRole(label, variation);
    }

    private static void ApplyTextRole(Label label, string variation)
    {
        label.RemoveThemeFontSizeOverride("font_size");
        label.ThemeTypeVariation = variation;
    }
}
