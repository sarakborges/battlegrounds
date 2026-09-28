using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

internal sealed class ModThemeBuilder
{
    private readonly string _modDirectory;
    private readonly ModThemeCatalog _source;
    private readonly Dictionary<string, FontFile> _fonts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);

    public ModThemeBuilder(string modDirectory, ModThemeCatalog source)
    {
        if (string.IsNullOrWhiteSpace(modDirectory)) throw new ArgumentException("Mod directory cannot be empty.", nameof(modDirectory));
        _modDirectory = Path.GetFullPath(modDirectory);
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public Theme Build()
    {
        var theme = new Theme();
        ApplyDefaults(theme);
        ApplyTypographyVariations(theme);
        ApplySemanticLabelVariations(theme);

        theme.SetTypeVariation("PrimaryButton", "Button");
        theme.SetTypeVariation("CardButton", "Button");

        ApplyComponent(theme, ModThemeComponentRoles.Button, "Button");
        ApplyComponent(theme, ModThemeComponentRoles.ButtonPrimary, "PrimaryButton", ModThemeComponentRoles.Button);
        ApplyComponent(theme, ModThemeComponentRoles.Card, "CardButton", ModThemeComponentRoles.Button);
        ApplyComponent(theme, ModThemeComponentRoles.Input, "LineEdit");
        ApplyComponent(theme, ModThemeComponentRoles.Panel, "PanelContainer");

        ApplyPanelVariation(theme, ModThemePanelRoles.OpponentRail, "OpponentRailSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.TavernControls, "TavernControlsSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.Tavern, "TavernSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.Board, "BoardSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.Reserve, "ReserveSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.HeroDock, "HeroDockSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.TurnRail, "TurnRailSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.HeroPortrait, "HeroPortraitFrame");
        ApplyPanelVariation(theme, ModThemePanelRoles.OpponentEntry, "OpponentEntry");
        ApplyPanelVariation(theme, ModThemePanelRoles.OpponentEntrySelf, "OpponentEntrySelf");
        ApplyPanelVariation(theme, ModThemePanelRoles.OpponentEntryEliminated, "OpponentEntryEliminated");
        ApplyPanelVariation(theme, ModThemePanelRoles.OpponentPortrait, "OpponentPortraitFrame");
        ApplyPanelVariation(theme, ModThemePanelRoles.TierBadge, "TierBadge");
        ApplyPanelVariation(theme, ModThemePanelRoles.HealthBadge, "HealthBadge");
        ApplyPanelVariation(theme, ModThemePanelRoles.ArmorBadge, "ArmorBadge");
        ApplyPanelVariation(theme, ModThemePanelRoles.ResourceBadge, "ResourceBadge");
        ApplyPanelVariation(theme, ModThemePanelRoles.Interaction, "InteractionSurface");
        ApplyPanelVariation(theme, ModThemePanelRoles.Shopkeeper, "ShopkeeperDropTarget");
        ApplyPanelVariation(theme, ModThemePanelRoles.CombatOverlay, "CombatOverlay");
        return theme;
    }

    public Texture2D? LoadImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        if (_textures.TryGetValue(relativePath, out var cached)) return cached;

        var fullPath = ResolvePath(relativePath);
        var image = Image.LoadFromFile(fullPath);
        if (image.IsEmpty()) return null;
        var texture = ImageTexture.CreateFromImage(image);
        _textures.Add(relativePath, texture);
        return texture;
    }

    private void ApplyDefaults(Theme theme)
    {
        if (_source.Fonts.TryGetValue("body", out var bodyFont))
            theme.DefaultFont = LoadFont(bodyFont.RelativePath);
        if (_source.FontSizes.TryGetValue("body", out var bodySize))
            theme.DefaultFontSize = bodySize;

        if (TryColor("text", out var text))
        {
            theme.SetColor("font_color", "Label", text);
            theme.SetColor("default_color", "RichTextLabel", text);
        }
        if (TryColor("textMuted", out var muted))
            theme.SetColor("font_uneditable_color", "LineEdit", muted);
    }

    private void ApplyTypographyVariations(Theme theme)
    {
        ApplyLabelVariation(theme, "TitleLabel", "title", preferDisplayFont: true, colorToken: "text");
        ApplyLabelVariation(theme, "HeadingLabel", "heading", preferDisplayFont: true, colorToken: "text");
        ApplyLabelVariation(theme, "BodyLabel", "body", preferDisplayFont: false, colorToken: "text");
        ApplyLabelVariation(theme, "CaptionLabel", "caption", preferDisplayFont: false, colorToken: "textMuted");
    }

    private void ApplySemanticLabelVariations(Theme theme)
    {
        ApplySemanticLabelVariation(theme, ModThemeLabelRoles.HeroName, "HeroNameLabel", "heading", preferDisplayFont: true, "text");
        ApplySemanticLabelVariation(theme, ModThemeLabelRoles.HealthValue, "HealthValueLabel", "heading", preferDisplayFont: true, "text");
        ApplySemanticLabelVariation(theme, ModThemeLabelRoles.ArmorValue, "ArmorValueLabel", "heading", preferDisplayFont: true, "text");
        ApplySemanticLabelVariation(theme, ModThemeLabelRoles.TierValue, "TierValueLabel", "heading", preferDisplayFont: true, "text");
        ApplySemanticLabelVariation(theme, ModThemeLabelRoles.ResourceValue, "ResourceValueLabel", "heading", preferDisplayFont: true, "text");
    }

    private void ApplySemanticLabelVariation(
        Theme theme,
        string role,
        string variation,
        string sizeToken,
        bool preferDisplayFont,
        string colorToken)
    {
        ApplyLabelVariation(theme, variation, sizeToken, preferDisplayFont, colorToken);
        ApplyComponent(theme, role, variation);
    }

    private void ApplyLabelVariation(Theme theme, string variation, string sizeToken, bool preferDisplayFont, string colorToken)
    {
        theme.SetTypeVariation(variation, "Label");
        var fontKey = preferDisplayFont && _source.Fonts.ContainsKey("display") ? "display" : "body";
        if (_source.Fonts.TryGetValue(fontKey, out var font)) theme.SetFont("font", variation, LoadFont(font.RelativePath));
        if (_source.FontSizes.TryGetValue(sizeToken, out var size)) theme.SetFontSize("font_size", variation, size);
        if (TryColor(colorToken, out var color)) theme.SetColor("font_color", variation, color);
    }

    private void ApplyPanelVariation(Theme theme, string role, string variation)
    {
        theme.SetTypeVariation(variation, "PanelContainer");
        ApplyComponent(theme, role, variation, ModThemeComponentRoles.Panel);
    }

    private void ApplyComponent(Theme theme, string role, string godotType, string? inheritedRole = null)
    {
        var inherited = inheritedRole is not null && _source.Components.TryGetValue(inheritedRole, out var inheritedStyle)
            ? inheritedStyle
            : null;
        var own = _source.Components.TryGetValue(role, out var ownStyle) ? ownStyle : null;
        if (inherited is null && own is null) return;

        var normal = Resolve(inherited, own, "normal");
        ApplyFont(theme, godotType, normal);
        ApplyTextColor(theme, godotType, "font_color", normal);
        ApplyStyleBox(theme, godotType, "normal", normal);

        foreach (var state in new[] { "hover", "pressed", "disabled", "focus" })
        {
            var resolved = Resolve(inherited, own, state);
            var colorName = state switch
            {
                "hover" => "font_hover_color",
                "pressed" => "font_pressed_color",
                "disabled" => "font_disabled_color",
                _ => null,
            };
            if (colorName is not null) ApplyTextColor(theme, godotType, colorName, resolved);
            ApplyStyleBox(theme, godotType, state, resolved);
        }
    }

    private void ApplyFont(Theme theme, string godotType, ResolvedStyle style)
    {
        if (style.Font is not null && _source.Fonts.TryGetValue(style.Font, out var font))
            theme.SetFont("font", godotType, LoadFont(font.RelativePath));
        if (style.FontSize is not null && _source.FontSizes.TryGetValue(style.FontSize, out var size))
            theme.SetFontSize("font_size", godotType, size);
    }

    private void ApplyTextColor(Theme theme, string godotType, string property, ResolvedStyle style)
    {
        if (!ResolveColor(style.TextColor, out var color)) return;
        theme.SetColor(property, godotType, WithOpacity(color, style.Opacity));
    }

    private void ApplyStyleBox(Theme theme, string godotType, string state, ResolvedStyle style)
    {
        if (style.BackgroundAsset is null && style.BackgroundColor is null && style.BorderColor is null &&
            style.Radius is null && style.Padding is null)
            return;

        StyleBox box;
        if (style.BackgroundAsset is not null)
        {
            var texture = LoadImage(style.BackgroundAsset);
            if (texture is null) return;
            var textured = new StyleBoxTexture { Texture = texture };
            if (style.Slice is not null)
            {
                textured.TextureMarginLeft = style.Slice.Left;
                textured.TextureMarginTop = style.Slice.Top;
                textured.TextureMarginRight = style.Slice.Right;
                textured.TextureMarginBottom = style.Slice.Bottom;
            }
            if (ResolveColor(style.BackgroundColor, out var tint))
                textured.ModulateColor = WithOpacity(tint, style.Opacity);
            else if (style.Opacity is not null)
                textured.ModulateColor = new Color(1, 1, 1, (float)style.Opacity.Value);
            box = textured;
        }
        else
        {
            var flat = new StyleBoxFlat();
            if (ResolveColor(style.BackgroundColor, out var background))
                flat.BgColor = WithOpacity(background, style.Opacity);
            else
                flat.BgColor = new Color(0, 0, 0, 0);
            if (ResolveColor(style.BorderColor, out var border)) flat.BorderColor = border;
            if (style.BorderWidth is not null)
            {
                flat.BorderWidthLeft = style.BorderWidth.Value;
                flat.BorderWidthTop = style.BorderWidth.Value;
                flat.BorderWidthRight = style.BorderWidth.Value;
                flat.BorderWidthBottom = style.BorderWidth.Value;
            }
            if (style.Radius is not null && _source.Radii.TryGetValue(style.Radius, out var radius))
            {
                flat.CornerRadiusTopLeft = radius;
                flat.CornerRadiusTopRight = radius;
                flat.CornerRadiusBottomLeft = radius;
                flat.CornerRadiusBottomRight = radius;
            }
            box = flat;
        }

        if (style.Padding is not null)
        {
            if (_source.Spacing.TryGetValue(style.Padding.Horizontal, out var horizontal))
            {
                box.ContentMarginLeft = horizontal;
                box.ContentMarginRight = horizontal;
            }
            if (_source.Spacing.TryGetValue(style.Padding.Vertical, out var vertical))
            {
                box.ContentMarginTop = vertical;
                box.ContentMarginBottom = vertical;
            }
        }

        theme.SetStylebox(state, godotType, box);
    }

    private FontFile LoadFont(string relativePath)
    {
        if (_fonts.TryGetValue(relativePath, out var cached)) return cached;
        var font = new FontFile();
        font.LoadDynamicFont(ResolvePath(relativePath));
        if (font.Data.IsEmpty()) throw new InvalidDataException($"Theme font '{relativePath}' could not be loaded.");
        _fonts.Add(relativePath, font);
        return font;
    }

    private string ResolvePath(string relativePath) => Path.Combine(
        _modDirectory,
        relativePath.Replace('/', Path.DirectorySeparatorChar));

    private bool TryColor(string token, out Color color) => ResolveColor(token, out color);

    private bool ResolveColor(string? value, out Color color)
    {
        color = default;
        if (!_source.TryResolveColor(value, out var resolved)) return false;
        color = Color.FromHtml(resolved);
        return true;
    }

    private static Color WithOpacity(Color color, double? opacity) =>
        opacity is null ? color : new Color(color, color.A * (float)opacity.Value);

    private static ResolvedStyle Resolve(ModThemeStyle? inherited, ModThemeStyle? own, string state)
    {
        var inheritedNormal = TryState(inherited, "normal");
        var ownNormal = TryState(own, "normal");
        var inheritedState = state == "normal" ? null : TryState(inherited, state);
        var ownState = state == "normal" ? null : TryState(own, state);
        var layers = new[] { inherited, inheritedNormal, own, ownNormal, inheritedState, ownState };

        return new ResolvedStyle(
            LastString(layers, item => item.Font),
            LastString(layers, item => item.FontSize),
            LastString(layers, item => item.TextColor),
            LastString(layers, item => item.BackgroundColor),
            LastString(layers, item => item.BorderColor),
            LastValue(layers, item => item.BorderWidth),
            LastString(layers, item => item.Radius),
            LastReference(layers, item => item.Padding),
            LastString(layers, item => item.BackgroundAsset),
            LastReference(layers, item => item.Slice),
            LastValue(layers, item => item.Opacity));
    }

    private static ModThemeStyle? TryState(ModThemeStyle? style, string state) =>
        style is not null && style.States.TryGetValue(state, out var value) ? value : null;

    private static string? LastString(IEnumerable<ModThemeStyle?> layers, Func<ModThemeStyle, string?> selector)
    {
        string? result = null;
        foreach (var layer in layers)
        {
            if (layer is null) continue;
            result = selector(layer) ?? result;
        }
        return result;
    }

    private static T? LastReference<T>(IEnumerable<ModThemeStyle?> layers, Func<ModThemeStyle, T?> selector)
        where T : class
    {
        T? result = null;
        foreach (var layer in layers)
        {
            if (layer is null) continue;
            result = selector(layer) ?? result;
        }
        return result;
    }

    private static T? LastValue<T>(IEnumerable<ModThemeStyle?> layers, Func<ModThemeStyle, T?> selector)
        where T : struct
    {
        T? result = null;
        foreach (var layer in layers)
        {
            if (layer is null) continue;
            result = selector(layer) ?? result;
        }
        return result;
    }

    private sealed record ResolvedStyle(
        string? Font,
        string? FontSize,
        string? TextColor,
        string? BackgroundColor,
        string? BorderColor,
        int? BorderWidth,
        string? Radius,
        ModThemePadding? Padding,
        string? BackgroundAsset,
        ModThemeSlice? Slice,
        double? Opacity);
}
