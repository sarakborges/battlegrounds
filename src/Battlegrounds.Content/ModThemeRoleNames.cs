namespace Battlegrounds.Content;

public static class ModThemeInteractionRoles
{
    public const string DragPreview = "drag.preview";
    public const string DropTargetValid = "dropTarget.valid";
    public const string DropTargetValidActive = "dropTarget.valid.active";
    public const string DropTargetInvalid = "dropTarget.invalid";
    public const string DropTargetInvalidActive = "dropTarget.invalid.active";
}

public static class ModThemeRoleNames
{
    private static readonly HashSet<string> ComponentRoles = new(StringComparer.Ordinal)
    {
        ModThemeComponentRoles.Button,
        ModThemeComponentRoles.ButtonPrimary,
        ModThemeComponentRoles.Card,
        ModThemeComponentRoles.Input,
        ModThemeComponentRoles.Panel,
        ModThemePanelRoles.HeroPortrait,
        ModThemePanelRoles.OpponentEntry,
        ModThemePanelRoles.OpponentEntrySelf,
        ModThemePanelRoles.OpponentEntryEliminated,
        ModThemePanelRoles.OpponentPortrait,
        ModThemePanelRoles.TierBadge,
        ModThemePanelRoles.HealthBadge,
        ModThemePanelRoles.ArmorBadge,
        ModThemePanelRoles.ResourceBadge,
        ModThemePanelRoles.Interaction,
        ModThemePanelRoles.Shopkeeper,
        ModThemePanelRoles.CombatOverlay,
        ModThemeLabelRoles.HeroName,
        ModThemeLabelRoles.HealthValue,
        ModThemeLabelRoles.ArmorValue,
        ModThemeLabelRoles.TierValue,
        ModThemeLabelRoles.ResourceValue,
        ModThemeInteractionRoles.DragPreview,
        ModThemeInteractionRoles.DropTargetValid,
        ModThemeInteractionRoles.DropTargetValidActive,
        ModThemeInteractionRoles.DropTargetInvalid,
        ModThemeInteractionRoles.DropTargetInvalidActive,
    };

    private static readonly HashSet<string> ScreenRoles = new(StringComparer.Ordinal)
    {
        ModThemeScreenRoles.Launcher,
        ModThemeScreenRoles.Preparation,
        ModThemeScreenRoles.Combat,
    };

    public static bool IsSupportedComponent(string role) =>
        !string.IsNullOrWhiteSpace(role) && ComponentRoles.Contains(role);

    public static bool IsSupportedScreen(string role) =>
        !string.IsNullOrWhiteSpace(role) && ScreenRoles.Contains(role);
}
