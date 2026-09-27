using Godot;

namespace Battlegrounds.Game;

public partial class PreparationSpatialTargetsDriver : Node
{
    public override void _Process(double delta)
    {
        if (GetParent() is not Main main)
            return;

        var frame = main.GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroPortraitFrame");
        if (frame is null || frame.GetNodeOrNull<PreparationDropTarget>("PurchaseDropTarget") is not null)
            return;

        var target = new PreparationDropTarget
        {
            Name = "PurchaseDropTarget",
            Role = PreparationDropTargetRole.AcquireOffer,
            ThemeTypeVariation = "HeroPurchaseDropTarget",
            ZIndex = 5,
        };
        frame.AddChild(target);
        target.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }
}
