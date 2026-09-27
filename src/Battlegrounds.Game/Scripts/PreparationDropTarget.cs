using Godot;

namespace Battlegrounds.Game;

public enum PreparationDropTargetRole
{
    AcquireOffer = 0,
    SellFieldUnit = 1,
}

public partial class PreparationDropTarget : PanelContainer
{
    [Export] public PreparationDropTargetRole Role { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        var main = FindMain();
        if (main is null)
            return false;

        return Role switch
        {
            PreparationDropTargetRole.AcquireOffer when PreparationDragPayload.TryReadOffer(data, out var slot) =>
                main.CanAcquireOfferFromDrag(slot),
            PreparationDropTargetRole.SellFieldUnit when PreparationDragPayload.TryReadField(data, out var fieldIndex) =>
                main.CanSellFieldUnitFromDrag(fieldIndex),
            _ => false,
        };
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var main = FindMain();
        if (main is null)
            return;

        switch (Role)
        {
            case PreparationDropTargetRole.AcquireOffer
                when PreparationDragPayload.TryReadOffer(data, out var slot):
                main.AcquireOfferFromDrag(slot);
                break;
            case PreparationDropTargetRole.SellFieldUnit
                when PreparationDragPayload.TryReadField(data, out var fieldIndex):
                main.SellFieldUnitFromDrag(fieldIndex);
                break;
        }
    }

    private Main? FindMain()
    {
        Node? node = this;
        while (node is not null)
        {
            if (node is Main main)
                return main;
            node = node.GetParent();
        }

        return null;
    }
}
