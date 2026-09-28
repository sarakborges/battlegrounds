using Godot;

namespace Battlegrounds.Game;

public partial class PreparationSpatialTargetsDriver : Node
{
    public override void _Process(double delta)
    {
        if (GetParent() is not Main main)
            return;

        var heroDock = main.GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/HeroDock");
        if (heroDock is null)
            return;

        var target = heroDock.GetNodeOrNull<PreparationDropTarget>("PurchaseDropTarget");
        if (target is null)
        {
            target = new PreparationDropTarget
            {
                Name = "PurchaseDropTarget",
                Role = PreparationDropTargetRole.AcquireOffer,
                ThemeTypeVariation = "HeroPurchaseDropTarget",
                ZIndex = 50,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            heroDock.AddChild(target);
            target.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        var viewport = main.GetViewport();
        var draggingOffer = viewport.GuiIsDragging() &&
                            PreparationDragPayload.TryReadOffer(viewport.GuiGetDragData(), out _);
        target.Visible = draggingOffer;
        target.MouseFilter = draggingOffer
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
    }
}
