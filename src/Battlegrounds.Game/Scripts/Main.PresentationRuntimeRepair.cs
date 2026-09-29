using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _presentationRuntimeRepaired;

    internal void EnsurePresentationRuntimeInvariants()
    {
        EnsureTavernControlsParentage();

        if (_presentationRuntimeRepaired || Theme is null)
            return;

        RepairPanelStyleSlots(this);
        _presentationRuntimeRepaired = true;
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
            if (child is PanelContainer panel)
                RepairPanelStyleSlot(panel);

            RepairPanelStyleSlots(child);
        }
    }

    private void RepairPanelStyleSlot(PanelContainer panel)
    {
        var variation = panel.ThemeTypeVariation;
        if (variation.IsEmpty || Theme is null || !Theme.HasStylebox("normal", variation))
            return;

        panel.AddThemeStyleboxOverride("panel", Theme.GetStylebox("normal", variation));
    }
}
