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
        _modTheme = new ModThemeLoader().Load(modDirectory);
        if (_modTheme.IsEmpty) return;

        _themeBuilder = new ModThemeBuilder(modDirectory, _modTheme);
        Theme = _themeBuilder.Build();
        _endPreparationButton.ThemeTypeVariation = "PrimaryButton";

        ApplyScreenTheme(ModThemeScreenRoles.Preparation);
        ApplySemanticTypography();
        AppendLog($"Presentation theme v{_modTheme.Version} loaded from the selected mod.");
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
        if (_modTheme is null) return;

        ApplyFontSize(GetNode<Label>("Margin/Root/Title"), "title");
        ApplyFontSize(_status, "heading");
        ApplyFontSize(_leaderPrompt, "heading");
        ApplyFontSize(_humanSummary, "body");
        ApplyFontSize(_interactionPrompt, "body");
        ApplyFontSize(GetNode<Label>("Margin/Root/PreparationPanel/Columns/OfferColumn/Title"), "heading");
        ApplyFontSize(GetNode<Label>("Margin/Root/PreparationPanel/Columns/ReserveColumn/Title"), "heading");
        ApplyFontSize(GetNode<Label>("Margin/Root/PreparationPanel/Columns/FieldColumn/Title"), "heading");
    }

    private void ApplyFontSize(Control control, string token)
    {
        if (_modTheme is null || !_modTheme.FontSizes.TryGetValue(token, out var size)) return;
        control.AddThemeFontSizeOverride("font_size", size);
    }
}
