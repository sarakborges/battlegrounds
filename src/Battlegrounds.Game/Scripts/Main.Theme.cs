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
        ApplyTextRole(GetNode<Label>("Margin/Root/Title"), "TitleLabel");
        ApplyTextRole(_status, "HeadingLabel");
        ApplyTextRole(_leaderPrompt, "HeadingLabel");
        ApplyTextRole(_humanSummary, "BodyLabel");
        ApplyTextRole(_interactionPrompt, "BodyLabel");
        ApplyTextRole(GetNode<Label>("Margin/Root/PreparationPanel/Columns/OfferColumn/Title"), "HeadingLabel");
        ApplyTextRole(GetNode<Label>("Margin/Root/PreparationPanel/Columns/ReserveColumn/Title"), "HeadingLabel");
        ApplyTextRole(GetNode<Label>("Margin/Root/PreparationPanel/Columns/FieldColumn/Title"), "HeadingLabel");
        ApplyTextRole(GetNode<Label>("Margin/Root/LogTitle"), "CaptionLabel");
    }

    private static void ApplyTextRole(Label label, string variation)
    {
        label.RemoveThemeFontSizeOverride("font_size");
        label.ThemeTypeVariation = variation;
    }
}
