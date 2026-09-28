using Godot;

namespace Battlegrounds.Game;

public partial class PreparationSpatialTargetsDriver : Node
{
    private const string PurchaseTargetName = "OfferDragCatcher";

    public override void _Process(double delta)
    {
        if (GetParent() is not Main main)
            return;

        var target = main.GetNodeOrNull<PreparationDropTarget>(PurchaseTargetName);
        if (target is null)
        {
            target = new PreparationDropTarget
            {
                Name = PurchaseTargetName,
                Role = PreparationDropTargetRole.AcquireOffer,
                ZIndex = 100,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            main.AddChild(target);
            target.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        // During an offer drag, cover the viewport with an invisible catcher. This keeps
        // Godot from showing the forbidden/disabled drop cursor over unrelated controls.
        // The catcher does NOT decide whether a purchase is valid: _DropData delegates to
        // Main.CompleteOfferDragFromPointer, which buys only when the pointer is on HeroCore.
        var viewport = main.GetViewport();
        var draggingOffer = viewport.GuiIsDragging() &&
                            PreparationDragPayload.TryReadOffer(viewport.GuiGetDragData(), out _);
        target.Visible = draggingOffer;
        target.MouseFilter = draggingOffer
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
    }
}
