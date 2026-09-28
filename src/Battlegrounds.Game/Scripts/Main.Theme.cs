using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private ModThemeCatalog? _modTheme;
    private ModThemeBuilder? _themeBuilder;
    private TextureRect? _themeBackgroundImage;

    private void InitializeTheme()
    {
        var modDirectory = ProjectSettings.GlobalizePath(ModPath);
        var loader = new ModThemeLoader();
        var engineTheme = loader.LoadEngineDefault();
        var modTheme = loader.Load(modDirectory);
        _modTheme = ModThemeCatalog.Layer(engineTheme, modTheme);

        _themeBuilder = new ModThemeBuilder(modDirectory, _modTheme);
        Theme = _themeBuilder.Build();
        _endPreparationButton.ThemeTypeVariation = "PrimaryButton";

        ApplyScreenTheme(ModThemeScreenRoles.Preparation);
        ApplySemanticTypography();
        ApplySemanticLayout();
        AppendLog(modTheme.IsEmpty
            ? $"Presentation theme v{_modTheme.Version} loaded from engine defaults."
            : $"Presentation theme v{_modTheme.Version} loaded from engine defaults + selected mod overrides.");
    }

    private void ApplyScreenTheme(string role)
    {
        if (_modTheme is null || _themeBuilder is null || !_modTheme.Screens.TryGetValue(role, out var screen)) return;

        var background = GetNode<ColorRect>("Background");
        if (_modTheme.TryResolveColor(screen.BackgroundColor, out var color))
            background.Color = Color.FromHtml(color);

        if (string.IsNullOrWhiteSpace(screen.BackgroundAsset))
        {
            if (_themeBackgroundImage is not null) _themeBackgroundImage.Visible = false;
            return;
        }

        var texture = _themeBuilder.LoadImage(screen.BackgroundAsset);
        if (texture is null) return;

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
        ApplyTextRole("Margin/Shell/CenterStage/PreparationPanel/TavernControls/TierBadge/HudTierValue", "TierValueLabel");
        ApplyTextRole("Margin/Shell/CenterStage/PreparationPanel/HeroDock/HealthBadge/HudHealthValue", "HealthValueLabel");
        ApplyTextRole("Margin/Shell/CenterStage/PreparationPanel/HeroDock/HudArmorBadge/HudArmorValue", "ArmorValueLabel");
        ApplyTextRole("Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroCore/HudHeroName", "HeroNameLabel");
        ApplyTextRole("Margin/Shell/CenterStage/PreparationPanel/HeroDock/ResourceBadge/HudResourceValue", "ResourceValueLabel");
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
