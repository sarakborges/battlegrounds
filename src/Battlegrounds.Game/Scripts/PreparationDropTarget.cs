using Godot;

namespace Battlegrounds.Game;

public enum PreparationDropTargetRole
{
    AcquireOffer = 0,
    SellFieldUnit = 1,
}

public partial class PreparationDropTarget : Control
{
    [Export] public PreparationDropTargetRole Role { get; set; }

    private bool _hot;

    public PreparationDropTarget()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        var main = FindMain();
        if (main is null)
        {
            SetHot(false);
            return false;
        }

        var valid = Role switch
        {
            PreparationDropTargetRole.AcquireOffer when PreparationDragPayload.TryReadOffer(data, out var slot) =>
                main.CanAcquireOfferFromDrag(slot),
            PreparationDropTargetRole.SellFieldUnit when PreparationDragPayload.TryReadField(data, out var fieldIndex) =>
                main.CanSellFieldUnitFromDrag(fieldIndex),
            _ => false,
        };

        SetHot(valid);
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var main = FindMain();
        if (main is null)
            return;

        SetHot(false);
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

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExitSelf || what == NotificationDragEnd)
            SetHot(false);
    }

    private void SetHot(bool hot)
    {
        if (_hot == hot)
            return;

        _hot = hot;
        var baseVariation = Role == PreparationDropTargetRole.AcquireOffer
            ? "HeroPurchaseDropTarget"
            : "ShopkeeperDropTarget";
        ThemeTypeVariation = hot ? $"{baseVariation}Hot" : baseVariation;
        Scale = hot ? new Vector2(1.055f, 1.055f) : Vector2.One;
        ZIndex = hot ? 25 : 0;
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
