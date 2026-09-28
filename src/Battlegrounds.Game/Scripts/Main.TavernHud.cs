using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Players;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _tavernHudBound;
    private Label _tavernUpgradeCost = null!;
    private Label _tavernRefreshCost = null!;
    private Label _tavernFreezeCost = null!;
    private HorizontalCardRow? _tavernActionOffers;

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

        RefreshPreparationChrome(human, match.Phase);
    }

    private void RefreshPreparationChrome(PlayerState human, MatchPhase phase)
    {
        var isPreparation = phase == MatchPhase.Preparation;
        _status.Visible = !isPreparation;
        if (!isPreparation)
            return;

        HideDirectPlaceholderLabels(_offerButtons);
        HideDirectPlaceholderLabels(_fieldButtons);
        HideDirectPlaceholderLabels(_reserveButtons);
        PartitionTavernOffers(human);
    }

    private static void HideDirectPlaceholderLabels(Node row)
    {
        foreach (var label in row.GetChildren().OfType<Label>())
            label.Visible = false;
    }

    private void PartitionTavernOffers(PlayerState human)
    {
        if (_tavernActionOffers is null)
            return;

        var entries = human.PlayableOffer;
        var cards = _offerButtons.GetChildren()
            .OfType<PresentationCardButton>()
            .Where(card => !card.IsQueuedForDeletion())
            .ToArray();

        if (cards.Length != entries.Count)
            return;

        var actionIndices = entries
            .Select((entry, index) => (entry, index))
            .Where(value => value.entry.Kind == PlayableKind.Action)
            .Select(value => value.index)
            .ToArray();

        ClearLiveChildren(_tavernActionOffers);
        if (actionIndices.Length == 0)
        {
            _tavernActionOffers.Visible = false;
            return;
        }

        foreach (var index in actionIndices)
        {
            var card = cards[index];
            _offerButtons.RemoveChild(card);
            _tavernActionOffers.AddChild(card);
        }

        _tavernActionOffers.Visible = true;
        _offerButtons.QueueSort();
        _tavernActionOffers.QueueSort();
    }

    private static void ClearLiveChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
            if (!child.IsQueuedForDeletion())
                child.QueueFree();
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
        shopkeeper.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        shopkeeper.MouseFilter = Control.MouseFilterEnum.Stop;
        shopkeeper.ClipContents = true;
        BindShopkeeperCosmetic(shopkeeper);
        BindShopkeeperFrame(shopkeeper);
        BindActionOfferShelf(shelfRow);

        controlsRow.MoveChild(_upgradeButton, 0);
        controlsRow.MoveChild(tierBadge, 1);
        controlsRow.MoveChild(shopkeeper, 2);
        controlsRow.MoveChild(_refreshButton, 3);
        controlsRow.MoveChild(_freezeButton, 4);
        controlsRow.Alignment = BoxContainer.AlignmentMode.Center;

        if (shopkeeper.GetNodeOrNull<Control>("Content") is VBoxContainer shopkeeperContent)
        {
            shopkeeperContent.ZIndex = 3;
            shopkeeperContent.Alignment = BoxContainer.AlignmentMode.End;
            if (shopkeeperContent.GetNodeOrNull<Label>("Name") is { } name)
                name.Visible = false;
            if (shopkeeperContent.GetNodeOrNull<Label>("SellHint") is { } sellHint)
                sellHint.Visible = false;
        }

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

    private void BindActionOfferShelf(HBoxContainer shelfRow)
    {
        _tavernActionOffers = shelfRow.GetNodeOrNull<HorizontalCardRow>("ActionOfferButtons");
        if (_tavernActionOffers is null)
        {
            _tavernActionOffers = new HorizontalCardRow
            {
                Name = "ActionOfferButtons",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                ClipContents = false,
            };
            shelfRow.AddChild(_tavernActionOffers);
        }

        ApplyRowLayout(_tavernActionOffers, ModThemeMetricKeys.Row.Offer);
        var preferredWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardWidth(ModThemeMetricKeys.Row.Offer),
            1.0f,
            2048.0f);
        var padding = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.Padding(ModThemeMetricKeys.Row.Offer),
            0.0f,
            512.0f);
        _tavernActionOffers.CustomMinimumSize = new Vector2(preferredWidth + (padding * 2.0f), 0);
        shelfRow.MoveChild(_tavernActionOffers, _offerButtons.GetIndex() + 1);
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
                CustomMinimumSize = new Vector2(
                    ResolvePresentationMetric(ModThemeMetricKeys.Layout.TavernShopkeeperWidth, 1.0f, 2048.0f),
                    ResolvePresentationMetric(ModThemeMetricKeys.Layout.TavernControlsMinimumHeight, 1.0f, 2048.0f)),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 1,
            };
            shopkeeper.AddChild(art);
            shopkeeper.MoveChild(art, 0);
        }

        var hasImage = PresentationTextures.TryGetShopkeeperImage(out var texture);
        art.Texture = texture;
        art.Visible = hasImage && texture is not null;
        if (!art.Visible)
            GD.PushWarning("Shopkeeper cosmetic could not be resolved; rendering the shopkeeper slot without art.");
    }

    private void BindShopkeeperFrame(PanelContainer shopkeeper)
    {
        if (_modTheme is null ||
            _themeBuilder is null ||
            !_modTheme.Components.TryGetValue(ModThemePanelRoles.Shopkeeper, out var style) ||
            style is null ||
            string.IsNullOrWhiteSpace(style.BackgroundAsset))
        {
            return;
        }

        var texture = _themeBuilder.LoadImage(style.BackgroundAsset);
        if (texture is null)
            return;

        var frame = shopkeeper.GetNodeOrNull<TextureRect>("CosmeticFrame");
        if (frame is null)
        {
            frame = new TextureRect
            {
                Name = "CosmeticFrame",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 2,
            };
            shopkeeper.AddChild(frame);
        }

        frame.Texture = texture;
        frame.Visible = true;
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
