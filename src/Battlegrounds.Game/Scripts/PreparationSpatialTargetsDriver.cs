using Godot;

namespace Battlegrounds.Game;

public partial class PreparationSpatialTargetsDriver : Node
{
    private const string CatcherName = "PreparationDragCatcher";
    private const string HeroFeedbackName = "HeroDropFeedback";
    private const string ShopkeeperFeedbackName = "ShopkeeperDropFeedback";
    private const string BoardFeedbackName = "BoardDropFeedback";

    public override void _Process(double delta)
    {
        if (GetParent() is not Main main)
            return;

        var catcher = EnsureCatcher(main);
        var heroFeedback = EnsureFeedback(main, HeroFeedbackName);
        var shopkeeperFeedback = EnsureFeedback(main, ShopkeeperFeedbackName);
        var boardFeedback = EnsureFeedback(main, BoardFeedbackName);

        var viewport = main.GetViewport();
        if (!viewport.GuiIsDragging())
        {
            SetIdle(catcher, heroFeedback, shopkeeperFeedback, boardFeedback);
            return;
        }

        var data = viewport.GuiGetDragData();
        if (!main.CanResolvePreparationDrag(data))
        {
            SetIdle(catcher, heroFeedback, shopkeeperFeedback, boardFeedback);
            return;
        }

        var pointer = viewport.GetMousePosition();
        var overValidTarget = false;

        var hero = main.GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroCore");
        var shopkeeper = main.GetNodeOrNull<Control>(
            "Margin/Shell/CenterStage/PreparationPanel/TavernShelf/ShelfRow/ShopkeeperSlot");
        var board = main.GetNodeOrNull<Control>("%FieldButtons");

        if (PreparationDragPayload.TryReadOffer(data, out var offerSlot))
        {
            var enabled = main.CanAcquireOfferFromDrag(offerSlot);
            overValidTarget |= ConfigureFeedback(main, heroFeedback, hero, enabled, pointer);
            HideFeedback(shopkeeperFeedback);
            HideFeedback(boardFeedback);
        }
        else if (PreparationDragPayload.TryReadField(data, out var fieldIndex))
        {
            var enabled = main.CanSellFieldUnitFromDrag(fieldIndex);
            HideFeedback(heroFeedback);
            overValidTarget |= ConfigureFeedback(main, shopkeeperFeedback, shopkeeper, enabled, pointer);
            overValidTarget |= ConfigureFeedback(main, boardFeedback, board, enabled, pointer);
        }
        else if (PreparationDragPayload.TryReadReserveUnit(data, out var reserveSlot))
        {
            var enabled = main.CanDeployReserveFromDrag(reserveSlot);
            HideFeedback(heroFeedback);
            HideFeedback(shopkeeperFeedback);
            overValidTarget |= ConfigureFeedback(main, boardFeedback, board, enabled, pointer);
        }
        else
        {
            SetIdle(catcher, heroFeedback, shopkeeperFeedback, boardFeedback);
            return;
        }

        catcher.Visible = true;
        catcher.MouseFilter = Control.MouseFilterEnum.Stop;
        catcher.MouseDefaultCursorShape = overValidTarget
            ? main.PreparationValidDropCursor
            : main.PreparationDragCursor;
    }

    private static PreparationDropTarget EnsureCatcher(Main main)
    {
        var catcher = main.GetNodeOrNull<PreparationDropTarget>(CatcherName);
        if (catcher is not null)
            return catcher;

        catcher = new PreparationDropTarget
        {
            Name = CatcherName,
            Role = PreparationDropTargetRole.SpatialResolver,
            ZIndex = 200,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        main.AddChild(catcher);
        catcher.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return catcher;
    }

    private static PanelContainer EnsureFeedback(Main main, string name)
    {
        var feedback = main.GetNodeOrNull<PanelContainer>(name);
        if (feedback is not null)
            return feedback;

        feedback = new PanelContainer
        {
            Name = name,
            ZIndex = 210,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None,
        };
        main.AddChild(feedback);
        return feedback;
    }

    private static bool ConfigureFeedback(
        Main main,
        PanelContainer feedback,
        Control? target,
        bool enabled,
        Vector2 pointer)
    {
        if (!enabled || target is null || !target.Visible)
        {
            HideFeedback(feedback);
            return false;
        }

        var padding = main.PreparationDropTargetPadding;
        var targetRect = target.GetGlobalRect();
        var mainRect = main.GetGlobalRect();
        var expansion = new Vector2(padding, padding);
        var expanded = new Rect2(targetRect.Position - expansion, targetRect.Size + expansion * 2.0f);
        var active = expanded.HasPoint(pointer);

        feedback.Position = expanded.Position - mainRect.Position;
        feedback.Size = expanded.Size;
        feedback.AddThemeStyleboxOverride("panel", main.BuildPreparationDropTargetStyle(active));
        feedback.Visible = true;
        return active;
    }

    private static void SetIdle(
        PreparationDropTarget catcher,
        PanelContainer heroFeedback,
        PanelContainer shopkeeperFeedback,
        PanelContainer boardFeedback)
    {
        catcher.Visible = false;
        catcher.MouseFilter = Control.MouseFilterEnum.Ignore;
        HideFeedback(heroFeedback);
        HideFeedback(shopkeeperFeedback);
        HideFeedback(boardFeedback);
    }

    private static void HideFeedback(PanelContainer feedback)
    {
        feedback.Visible = false;
    }
}
