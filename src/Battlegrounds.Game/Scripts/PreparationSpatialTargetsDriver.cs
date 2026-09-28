using Godot;

namespace Battlegrounds.Game;

public partial class PreparationSpatialTargetsDriver : Node
{
    private const string PurchaseTargetName = "HeroPurchaseDropTarget";

    public override void _Process(double delta)
    {
        if (GetParent() is not Main main)
            return;

        var heroCore = main.GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroCore");
        if (heroCore is null)
            return;

        var target = main.GetNodeOrNull<PreparationDropTarget>(PurchaseTargetName);
        if (target is null)
        {
            target = new PreparationDropTarget
            {
                Name = PurchaseTargetName,
                Role = PreparationDropTargetRole.AcquireOffer,
                ThemeTypeVariation = "HeroPurchaseDropTarget",
                ZIndex = 100,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            main.AddChild(target);
        }

        // Keep the actual drop target outside HeroDock's HBoxContainer. Containers own the
        // layout of their children, so placing the overlay inside HeroDock can collapse or
        // relocate it. As a root overlay it can exactly mirror the hero's visible rectangle.
        var heroRect = heroCore.GetGlobalRect();
        var mainRect = main.GetGlobalRect();
        target.Position = heroRect.Position - mainRect.Position;
        target.Size = heroRect.Size;

        var viewport = main.GetViewport();
        var draggingOffer = viewport.GuiIsDragging() &&
                            PreparationDragPayload.TryReadOffer(viewport.GuiGetDragData(), out _);
        target.Visible = draggingOffer && heroCore.Visible;
        target.MouseFilter = target.Visible
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
    }
}
