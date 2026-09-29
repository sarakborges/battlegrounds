using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private static readonly IReadOnlyDictionary<string, string> PanelRoleByVariation =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OpponentRailSurface"] = ModThemePanelRoles.OpponentRail,
            ["TavernControlsSurface"] = ModThemePanelRoles.TavernControls,
            ["TavernSurface"] = ModThemePanelRoles.Tavern,
            ["BoardSurface"] = ModThemePanelRoles.Board,
            ["ReserveSurface"] = ModThemePanelRoles.Reserve,
            ["HeroDockSurface"] = ModThemePanelRoles.HeroDock,
            ["TurnRailSurface"] = ModThemePanelRoles.TurnRail,
            ["HeroPortraitFrame"] = ModThemePanelRoles.HeroPortrait,
            ["OpponentEntry"] = ModThemePanelRoles.OpponentEntry,
            ["OpponentEntrySelf"] = ModThemePanelRoles.OpponentEntrySelf,
            ["OpponentEntryEliminated"] = ModThemePanelRoles.OpponentEntryEliminated,
            ["OpponentPortraitFrame"] = ModThemePanelRoles.OpponentPortrait,
            ["TierBadge"] = ModThemePanelRoles.TierBadge,
            ["AttackBadge"] = ModThemePanelRoles.AttackBadge,
            ["HealthBadge"] = ModThemePanelRoles.HealthBadge,
            ["ArmorBadge"] = ModThemePanelRoles.ArmorBadge,
            ["ResourceBadge"] = ModThemePanelRoles.ResourceBadge,
            ["CardInspectSurface"] = ModThemePanelRoles.CardInspect,
            ["InteractionSurface"] = ModThemePanelRoles.Interaction,
            ["ShopkeeperDropTarget"] = ModThemePanelRoles.Shopkeeper,
            ["CombatOverlay"] = ModThemePanelRoles.CombatOverlay,
        };

    private readonly HashSet<ulong> _presentationStyledPanels = [];

    internal void EnsurePresentationRuntimeInvariants()
    {
        EnsureTavernControlsParentage();
        RepairPanelStyleSlots(this);
    }

    private void EnsureTavernControlsParentage()
    {
        const string controlsPath = "Margin/Shell/CenterStage/PreparationPanel/TavernControls/ControlsRow";
        var controlsRow = GetNodeOrNull<HBoxContainer>(controlsPath);
        if (controlsRow is null)
            return;

        EnsureChildOf(GetNodeOrNull<Button>("%UpgradeButton"), controlsRow);
        EnsureChildOf(GetNodeOrNull<Button>("%RefreshButton"), controlsRow);
        EnsureChildOf(GetNodeOrNull<Button>("%FreezeButton"), controlsRow);
    }

    private static void EnsureChildOf(Node? child, Node expectedParent)
    {
        if (child is null || child.GetParent() == expectedParent)
            return;

        child.GetParent()?.RemoveChild(child);
        expectedParent.AddChild(child);
    }

    private void RepairPanelStyleSlots(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is PanelContainer panel && _presentationStyledPanels.Add(panel.GetInstanceId()))
                RepairPanelStyleSlot(panel);

            RepairPanelStyleSlots(child);
        }
    }

    private void RepairPanelStyleSlot(PanelContainer panel)
    {
        if (_modTheme is null)
            return;

        var variation = panel.ThemeTypeVariation.ToString();
        if (string.IsNullOrWhiteSpace(variation) || !PanelRoleByVariation.TryGetValue(variation, out var role))
            return;

        if (!_modTheme.Components.TryGetValue(ModThemeComponentRoles.Panel, out var baseStyle))
            return;
        _modTheme.Components.TryGetValue(role, out var roleStyle);

        var layers = new[]
        {
            baseStyle,
            TryNormal(baseStyle),
            roleStyle,
            TryNormal(roleStyle),
        };

        var backgroundAsset = LastString(layers, style => style.BackgroundAsset);
        var backgroundColor = LastString(layers, style => style.BackgroundColor);
        var borderColor = LastString(layers, style => style.BorderColor);
        var borderWidth = LastValue(layers, style => style.BorderWidth);
        var radiusToken = LastString(layers, style => style.Radius);
        var padding = LastReference(layers, style => style.Padding);
        var slice = LastReference(layers, style => style.Slice);
        var opacity = LastValue(layers, style => style.Opacity);

        StyleBox box;
        if (!string.IsNullOrWhiteSpace(backgroundAsset) && _themeBuilder?.LoadImage(backgroundAsset) is { } texture)
        {
            var textured = new StyleBoxTexture { Texture = texture };
            if (slice is not null)
            {
                textured.TextureMarginLeft = slice.Left;
                textured.TextureMarginTop = slice.Top;
                textured.TextureMarginRight = slice.Right;
                textured.TextureMarginBottom = slice.Bottom;
            }

            if (TryResolveThemeColor(backgroundColor, out var tint))
                textured.ModulateColor = ApplyOpacity(tint, opacity);
            else if (opacity is not null)
                textured.ModulateColor = new Color(1, 1, 1, (float)opacity.Value);
            box = textured;
        }
        else
        {
            var flat = new StyleBoxFlat
            {
                BgColor = TryResolveThemeColor(backgroundColor, out var background)
                    ? ApplyOpacity(background, opacity)
                    : Colors.Transparent,
            };

            if (TryResolveThemeColor(borderColor, out var border))
                flat.BorderColor = border;
            if (borderWidth is not null)
            {
                flat.BorderWidthLeft = borderWidth.Value;
                flat.BorderWidthTop = borderWidth.Value;
                flat.BorderWidthRight = borderWidth.Value;
                flat.BorderWidthBottom = borderWidth.Value;
            }

            if (!string.IsNullOrWhiteSpace(radiusToken) && _modTheme.Radii.TryGetValue(radiusToken, out var radius))
            {
                flat.CornerRadiusTopLeft = radius;
                flat.CornerRadiusTopRight = radius;
                flat.CornerRadiusBottomLeft = radius;
                flat.CornerRadiusBottomRight = radius;
            }

            box = flat;
        }

        if (padding is not null)
        {
            if (_modTheme.Spacing.TryGetValue(padding.Horizontal, out var horizontal))
            {
                box.ContentMarginLeft = horizontal;
                box.ContentMarginRight = horizontal;
            }
            if (_modTheme.Spacing.TryGetValue(padding.Vertical, out var vertical))
            {
                box.ContentMarginTop = vertical;
                box.ContentMarginBottom = vertical;
            }
        }

        panel.AddThemeStyleboxOverride("panel", box);
    }

    private bool TryResolveThemeColor(string? token, out Color color)
    {
        color = default;
        if (_modTheme?.TryResolveColor(token, out var resolved) != true)
            return false;
        color = Color.FromHtml(resolved);
        return true;
    }

    private static Color ApplyOpacity(Color color, double? opacity) =>
        opacity is null ? color : new Color(color, color.A * (float)opacity.Value);

    private static ModThemeStyle? TryNormal(ModThemeStyle? style) =>
        style is not null && style.States.TryGetValue("normal", out var normal) ? normal : null;

    private static string? LastString(
        IEnumerable<ModThemeStyle?> layers,
        Func<ModThemeStyle, string?> selector)
    {
        string? value = null;
        foreach (var layer in layers)
            if (layer is not null)
                value = selector(layer) ?? value;
        return value;
    }

    private static T? LastValue<T>(
        IEnumerable<ModThemeStyle?> layers,
        Func<ModThemeStyle, T?> selector)
        where T : struct
    {
        T? value = null;
        foreach (var layer in layers)
            if (layer is not null)
                value = selector(layer) ?? value;
        return value;
    }

    private static T? LastReference<T>(
        IEnumerable<ModThemeStyle?> layers,
        Func<ModThemeStyle, T?> selector)
        where T : class
    {
        T? value = null;
        foreach (var layer in layers)
            if (layer is not null)
                value = selector(layer) ?? value;
        return value;
    }
}
