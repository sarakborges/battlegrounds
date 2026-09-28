using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _tavernHudBound;
    private Label _tavernUpgradeCost = null!;
    private Label _tavernRefreshCost = null!;
    private Label _tavernFreezeCost = null!;

    private void RefreshTavernHud()
    {
        if (_session?.Match is not MatchState match ||
            !match.TryGetPlayer(_session.HumanPlayerId, out var human))
            return;

        if (!_tavernHudBound)
        {
            BindBattlegroundsTavernHeader();
            _tavernHudBound = true;
        }

        var rules = _session.Mod.PreparationRules;
        var canAct = match.Phase == MatchPhase.Preparation &&
                     !human.IsEliminated &&
                     !human.IsReadyForCombat &&
                     human.PendingChoice is null &&
                     !_interaction.IsActive;

        _upgradeButton.ThemeTypeVariation = "TavernUpgradeButton";
        _refreshButton.ThemeTypeVariation = "TavernRefreshButton";
        _freezeButton.ThemeTypeVariation = "TavernFreezeButton";

        _upgradeButton.Text = "★";
        _refreshButton.Text = "↻";
        _freezeButton.Text = "❄";

        _tavernUpgradeCost.Text = human.UpgradeCost?.ToString() ?? "—";
        _tavernRefreshCost.Text = rules.RefreshCost.ToString();
        _tavernFreezeCost.Text = "0";

        _upgradeButton.Disabled = !canAct || human.UpgradeCost is null || human.Resource < human.UpgradeCost.Value;
        _refreshButton.Disabled = !canAct || human.Resource < rules.RefreshCost;
        _freezeButton.Disabled = !canAct;
        _freezeButton.ToggleMode = true;
        _freezeButton.ButtonPressed = human.IsOfferFrozen;

        var upgradeDescription = human.UpgradeCost is int upgradeCost
            ? $"{Text("ui.upgradeTier", ("tier", Term("tier")))} · {upgradeCost} {Term("resource")}"
            : $"{Text("ui.upgradeTier", ("tier", Term("tier")))} · {Text("ui.maximum")}";
        _upgradeButton.TooltipText = upgradeDescription;
        _refreshButton.TooltipText = $"{Text("ui.refresh")} · {rules.RefreshCost} {Term("resource")}";
        _freezeButton.TooltipText = human.IsOfferFrozen
            ? Text("ui.unfreezeOffer", ("offer", Term("offer")))
            : Text("ui.freezeOffer", ("offer", Term("offer")));
    }

    private void BindBattlegroundsTavernHeader()
    {
        const string preparation = "Margin/Shell/CenterStage/PreparationPanel";
        var controlsRow = GetNode<HBoxContainer>($"{preparation}/TavernControls/ControlsRow");
        var shelfRow = GetNode<HBoxContainer>($"{preparation}/TavernShelf/ShelfRow");
        var tierBadge = GetNode<PanelContainer>($"{preparation}/TavernControls/ControlsRow/TierBadge");
        var controlSpacer = GetNode<Control>($"{preparation}/TavernControls/ControlsRow/ControlSpacer");
        var balanceSpacer = GetNode<Control>($"{preparation}/TavernShelf/ShelfRow/TavernBalanceSpacer");

        var shopkeeper = GetNodeOrNull<PanelContainer>($"{preparation}/TavernControls/ControlsRow/ShopkeeperSlot") ??
                         GetNode<PanelContainer>($"{preparation}/TavernShelf/ShelfRow/ShopkeeperSlot");
        if (shopkeeper.GetParent() != controlsRow)
        {
            shopkeeper.GetParent().RemoveChild(shopkeeper);
            controlsRow.AddChild(shopkeeper);
        }

        controlSpacer.Visible = false;
        balanceSpacer.Visible = false;
        shopkeeper.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        shopkeeper.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        shopkeeper.MouseFilter = Control.MouseFilterEnum.Stop;
        shopkeeper.ClipContents = true;

        controlsRow.MoveChild(_upgradeButton, 0);
        controlsRow.MoveChild(tierBadge, 1);
        controlsRow.MoveChild(shopkeeper, 2);
        controlsRow.MoveChild(_refreshButton, 3);
        controlsRow.MoveChild(_freezeButton, 4);
        controlsRow.Alignment = BoxContainer.AlignmentMode.Center;

        if (shopkeeper.GetNodeOrNull<Control>("Content") is VBoxContainer shopkeeperContent)
        {
            shopkeeperContent.Alignment = BoxContainer.AlignmentMode.End;
            shopkeeperContent.ZIndex = 2;
            if (shopkeeperContent.GetNodeOrNull<Label>("Name") is { } name)
                name.Visible = false;
            if (shopkeeperContent.GetNodeOrNull<Label>("SellHint") is { } sellHint)
            {
                sellHint.Text = Text("ui.releaseTargetTitle");
                sellHint.ThemeTypeVariation = "CaptionLabel";
                sellHint.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }

        BindShopkeeperCosmetic(shopkeeper);

        _tavernUpgradeCost = EnsureTavernCostBadge(_upgradeButton, "UpgradeCost");
        _tavernRefreshCost = EnsureTavernCostBadge(_refreshButton, "RefreshCost");
        _tavernFreezeCost = EnsureTavernCostBadge(_freezeButton, "FreezeCost");

        _upgradeButton.ClipContents = false;
        _refreshButton.ClipContents = false;
        _freezeButton.ClipContents = false;
        _upgradeButton.FocusMode = Control.FocusModeEnum.All;
        _refreshButton.FocusMode = Control.FocusModeEnum.All;
        _freezeButton.FocusMode = Control.FocusModeEnum.All;

        shelfRow.Alignment = BoxContainer.AlignmentMode.Center;
    }

    private void BindShopkeeperCosmetic(PanelContainer shopkeeper)
    {
        var art = shopkeeper.GetNodeOrNull<TextureRect>("CosmeticArt");
        if (art is null)
        {
            art = new TextureRect
            {
                Name = "CosmeticArt",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 1,
            };
            art.CustomMinimumSize = new Vector2(
                ResolvePresentationMetric(ModThemeMetricKeys.Layout.TavernShopkeeperWidth, 1.0f, 2048.0f),
                ResolvePresentationMetric(ModThemeMetricKeys.Layout.TavernControlHeight, 1.0f, 2048.0f));
            shopkeeper.AddChild(art);
            shopkeeper.MoveChild(art, 0);
        }

        var hasImage = PresentationTextures.TryGetShopkeeperImage(out var texture);
        art.Texture = texture;
        art.Visible = hasImage && texture is not null;

        if (!art.Visible)
            GD.PushWarning("Shopkeeper cosmetic could not be resolved; rendering the shopkeeper slot without art.");
    }

    private Label EnsureTavernCostBadge(Button button, string name)
    {
        if (button.GetNodeOrNull<PanelContainer>(name) is { } existing &&
            existing.GetNodeOrNull<Label>("Value") is { } existingValue)
            return existingValue;

        var badge = new PanelContainer
        {
            Name = name,
            ThemeTypeVariation = "ResourceBadge",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 5,
        };
        badge.AnchorLeft = 0.0f;
        badge.AnchorTop = 0.0f;
        badge.AnchorRight = 0.0f;
        badge.AnchorBottom = 0.0f;
        badge.OffsetLeft = 2.0f;
        badge.OffsetTop = 2.0f;
        badge.OffsetRight = 30.0f;
        badge.OffsetBottom = 30.0f;
        button.AddChild(badge);

        var value = new Label
        {
            Name = "Value",
            ThemeTypeVariation = "CaptionLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (ResolveThemeColor("resourceAccent", out var resourceColor))
            value.AddThemeColorOverride("font_color", resourceColor);
        badge.AddChild(value);
        return value;
    }
}
