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
    public const string UnitToken = "unit-token";
    public const string ActionToken = "action-token";
    public const string UnitCardPreview = "unit-card-preview";
    public const string ActionCardPreview = "action-card-preview";
    public const string LeaderInspector = "leader-inspector";
    public const string PowerTooltip = "power-tooltip";
    public const string PlayerChip = "player-chip";
    public const string EndPreparationButton = "button.endPreparation";

    private static readonly HashSet<string> ComponentRoles = new(StringComparer.Ordinal)
    {
        ModThemeComponentRoles.Button,
        ModThemeComponentRoles.ButtonPrimary,
        ModThemeComponentRoles.ButtonTierUpgrade,
        ModThemeComponentRoles.ButtonOfferRefresh,
        ModThemeComponentRoles.ButtonOfferFreeze,
        ModThemeComponentRoles.ButtonPower,
        EndPreparationButton,
        UnitToken,
        ActionToken,
        UnitCardPreview,
        ActionCardPreview,
        LeaderInspector,
        PowerTooltip,
        PlayerChip,
        ModThemeComponentRoles.Input,
        ModThemeComponentRoles.Panel,
        ModThemePanelRoles.OpponentRail,
        ModThemePanelRoles.PreparationControls,
        ModThemePanelRoles.Offer,
        ModThemePanelRoles.Board,
        ModThemePanelRoles.Reserve,
        ModThemePanelRoles.LeaderDock,
        ModThemePanelRoles.LeaderPortrait,
        ModThemePanelRoles.OpponentEntry,
        ModThemePanelRoles.OpponentEntrySelf,
        ModThemePanelRoles.OpponentEntryEliminated,
        ModThemePanelRoles.OpponentPortrait,
        ModThemePanelRoles.TierBadge,
        ModThemePanelRoles.AttackBadge,
        ModThemePanelRoles.HealthBadge,
        ModThemePanelRoles.ArmorBadge,
        ModThemePanelRoles.ResourceBadge,
        ModThemePanelRoles.Interaction,
        ModThemePanelRoles.PreparationHost,
        ModThemePanelRoles.CombatOverlay,
        ModThemeLabelRoles.LeaderName,
        ModThemeLabelRoles.AttackValue,
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
