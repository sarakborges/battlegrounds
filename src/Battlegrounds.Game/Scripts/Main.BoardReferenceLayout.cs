using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _boardReferenceBound;
    private Control? _boardReferenceOverlay;
    private PanelContainer? _boardReferenceOpponentRail;
    private HBoxContainer? _boardReferenceShopRow;
    private MarginContainer? _boardReferenceMargin;
    private HBoxContainer? _boardReferenceShell;
    private VBoxContainer? _boardReferenceCenterStage;
    private PanelContainer? _boardReferenceTavernControls;
    private PanelContainer? _boardReferenceTavernShelf;
    private PanelContainer? _boardReferenceBoardStage;
    private PanelContainer? _boardReferenceTurnRail;

    private void RefreshBoardReferenceLayout()
    {
        if (_session?.Match is not MatchState match)
            return;

        if (!_hudBound)
            return;

        if (!_boardReferenceBound)
        {
            BindBoardReferenceLayout();
            _boardReferenceBound = true;
        }

        if (_boardReferenceOverlay is null)
            return;

        var preparation = match.Phase == MatchPhase.Preparation;
        _boardReferenceShopRow?.SetVisible(preparation);
        _fieldButtons.Visible = preparation;

        ApplyBoardShellGeometry();
        ApplyOpponentRailGeometry();

        if (!preparation)
            return;

        ApplyPreparationBoardGeometry();
        ApplyReferenceShopGeometry();
        ApplyReferenceFieldGeometry();
        ApplyReferenceCockpitFinalGeometry();
    }

    private void BindBoardReferenceLayout()
    {
        _boardReferenceMargin = GetNodeOrNull<MarginContainer>("Margin");
        _boardReferenceShell = GetNodeOrNull<HBoxContainer>("Margin/Shell");
        _boardReferenceCenterStage = GetNodeOrNull<VBoxContainer>("Margin/Shell/CenterStage");
        _boardReferenceOpponentRail = GetNodeOrNull<PanelContainer>("Margin/Shell/OpponentRail");
        _boardReferenceTavernControls = GetNodeOrNull<PanelContainer>("Margin/Shell/CenterStage/PreparationPanel/TavernControls");
        _boardReferenceTavernShelf = GetNodeOrNull<PanelContainer>("Margin/Shell/CenterStage/PreparationPanel/TavernShelf");
        _boardReferenceBoardStage = GetNodeOrNull<PanelContainer>("Margin/Shell/CenterStage/PreparationPanel/BoardStage");
        _boardReferenceTurnRail = GetNodeOrNull<PanelContainer>("Margin/Shell/TurnRail");

        _boardReferenceOverlay = GetNodeOrNull<Control>("ReferenceBoardOverlay");
        if (_boardReferenceOverlay is null)
        {
            _boardReferenceOverlay = new Control
            {
                Name = "ReferenceBoardOverlay",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 20,
            };
            AddChild(_boardReferenceOverlay);
            _boardReferenceOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        if (_boardReferenceOpponentRail is not null)
            ReparentBoardControl(_boardReferenceOpponentRail, _boardReferenceOverlay);

        ReparentBoardControl(_fieldButtons, _boardReferenceOverlay);

        _boardReferenceShopRow = _boardReferenceOverlay.GetNodeOrNull<HBoxContainer>("ReferenceShopRow");
        if (_boardReferenceShopRow is null)
        {
            _boardReferenceShopRow = new HBoxContainer
            {
                Name = "ReferenceShopRow",
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 24,
            };
            _boardReferenceShopRow.AddThemeConstantOverride("separation", 14);
            _boardReferenceOverlay.AddChild(_boardReferenceShopRow);
        }

        ReparentBoardControl(_offerButtons, _boardReferenceShopRow);
        if (_tavernActionOffers is not null)
            ReparentBoardControl(_tavernActionOffers, _boardReferenceShopRow);

        if (_boardReferenceTavernShelf is not null)
        {
            _boardReferenceTavernShelf.Visible = false;
            _boardReferenceTavernShelf.CustomMinimumSize = Vector2.Zero;
        }

        if (_boardReferenceTurnRail is not null)
        {
            _boardReferenceTurnRail.Visible = false;
            _boardReferenceTurnRail.CustomMinimumSize = Vector2.Zero;
        }
    }

    private void ApplyBoardShellGeometry()
    {
        if (_boardReferenceMargin is not null)
        {
            _boardReferenceMargin.OffsetLeft = 0;
            _boardReferenceMargin.OffsetTop = 0;
            _boardReferenceMargin.OffsetRight = 0;
            _boardReferenceMargin.OffsetBottom = 0;
        }

        if (_boardReferenceShell is not null)
        {
            _boardReferenceShell.AddThemeConstantOverride("separation", 0);
            _boardReferenceShell.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _boardReferenceShell.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }

        if (_boardReferenceCenterStage is not null)
        {
            _boardReferenceCenterStage.CustomMinimumSize = Vector2.Zero;
            _boardReferenceCenterStage.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _boardReferenceCenterStage.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }
    }

    private void ApplyOpponentRailGeometry()
    {
        if (_boardReferenceOpponentRail is null)
            return;

        var width = Size.X;
        var height = Size.Y;
        if (width <= 1 || height <= 1)
            return;

        var railWidth = Mathf.Clamp(width * 0.09f, 132.0f, 158.0f);
        var entryHeight = Mathf.Clamp(height * 0.074f, 56.0f, 68.0f);
        var liveEntries = Math.Max(1, _opponentEntries.GetChildren().OfType<PanelContainer>().Count());
        var railHeight = Mathf.Min(height - 16.0f, liveEntries * entryHeight + 12.0f);

        ResetFreeControl(_boardReferenceOpponentRail);
        _boardReferenceOpponentRail.Position = new Vector2(8.0f, 8.0f);
        _boardReferenceOpponentRail.Size = new Vector2(railWidth, railHeight);
        _boardReferenceOpponentRail.CustomMinimumSize = Vector2.Zero;
        _boardReferenceOpponentRail.ZIndex = 30;

        foreach (var entry in _opponentEntries.GetChildren().OfType<PanelContainer>())
            entry.CustomMinimumSize = new Vector2(0, entryHeight);
    }

    private void ApplyPreparationBoardGeometry()
    {
        var width = Size.X;
        var height = Size.Y;
        if (width <= 1 || height <= 1)
            return;

        if (_boardReferenceTavernControls is not null)
        {
            MakePanelTransparent(_boardReferenceTavernControls);
            _boardReferenceTavernControls.CustomMinimumSize = new Vector2(0, Mathf.Clamp(height * 0.195f, 138.0f, 176.0f));
        }

        if (_boardReferenceBoardStage is not null)
        {
            MakePanelTransparent(_boardReferenceBoardStage);
            _boardReferenceBoardStage.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }

        if (_boardReferenceTavernShelf is not null)
            MakePanelTransparent(_boardReferenceTavernShelf);

        var controlsRow = _boardReferenceTavernControls?.GetNodeOrNull<HBoxContainer>("ControlsRow");
        if (controlsRow is null)
            return;

        controlsRow.Alignment = BoxContainer.AlignmentMode.Center;
        controlsRow.AddThemeConstantOverride("separation", Mathf.RoundToInt(Mathf.Clamp(width * 0.007f, 8.0f, 14.0f)));

        if (controlsRow.GetNodeOrNull<PanelContainer>("TierBadge") is { } tierBadge)
        {
            var tierSize = Mathf.Clamp(height * 0.043f, 32.0f, 40.0f);
            tierBadge.CustomMinimumSize = new Vector2(tierSize, tierSize);
            tierBadge.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            tierBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        }

        if (controlsRow.GetNodeOrNull<PanelContainer>("ShopkeeperSlot") is { } shopkeeper)
        {
            var keeperSide = Mathf.Clamp(height * 0.165f, 112.0f, 146.0f);
            shopkeeper.CustomMinimumSize = new Vector2(keeperSide, keeperSide);
            shopkeeper.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            shopkeeper.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            MakePanelTransparent(shopkeeper);
            if (shopkeeper.GetNodeOrNull<Control>("CosmeticLayer") is { } layer)
                FramedCosmeticPortrait.ConfigureSquare(layer, keeperSide);
        }

        var controlWidth = Mathf.Clamp(width * 0.044f, 64.0f, 78.0f);
        var controlHeight = Mathf.Clamp(height * 0.105f, 78.0f, 96.0f);
        SetControlFootprint(_upgradeButton, controlWidth, controlHeight);
        SetControlFootprint(_refreshButton, controlWidth, controlHeight);
        SetControlFootprint(_freezeButton, controlWidth, controlHeight);
    }

    private void ApplyReferenceShopGeometry()
    {
        if (_boardReferenceShopRow is null)
            return;

        var width = Size.X;
        var height = Size.Y;
        var rowWidth = width * 0.55f;
        var rowHeight = Mathf.Clamp(height * 0.205f, 142.0f, 176.0f);

        ResetFreeControl(_boardReferenceShopRow);
        _boardReferenceShopRow.Position = new Vector2((width - rowWidth) * 0.5f, height * 0.205f);
        _boardReferenceShopRow.Size = new Vector2(rowWidth, rowHeight);
        _boardReferenceShopRow.CustomMinimumSize = Vector2.Zero;
        _boardReferenceShopRow.ZIndex = 24;

        var cardWidth = Mathf.Clamp(width * 0.064f, 94.0f, 112.0f);
        var cardHeight = Mathf.Clamp(height * 0.165f, 126.0f, 148.0f);
        ConfigureReferenceRow(_offerButtons, cardWidth, cardHeight, 14.0f, 2.0f);
        if (_tavernActionOffers is not null)
            ConfigureReferenceRow(_tavernActionOffers, cardWidth, cardHeight, 14.0f, 2.0f);
    }

    private void ApplyReferenceFieldGeometry()
    {
        var width = Size.X;
        var height = Size.Y;
        var rowWidth = width * 0.61f;
        var rowHeight = Mathf.Clamp(height * 0.19f, 132.0f, 166.0f);

        ResetFreeControl(_fieldButtons);
        _fieldButtons.Position = new Vector2((width - rowWidth) * 0.5f, height * 0.485f);
        _fieldButtons.Size = new Vector2(rowWidth, rowHeight);
        _fieldButtons.CustomMinimumSize = Vector2.Zero;
        _fieldButtons.ClipContents = false;
        _fieldButtons.ZIndex = 26;

        var cardWidth = Mathf.Clamp(width * 0.061f, 88.0f, 106.0f);
        var cardHeight = Mathf.Clamp(height * 0.15f, 112.0f, 136.0f);
        ConfigureReferenceRow(_fieldButtons, cardWidth, cardHeight, 11.0f, 2.0f);
    }

    private void ApplyReferenceCockpitFinalGeometry()
    {
        if (_referenceHeroCluster is null || _referenceCockpit is null)
            return;

        var width = Size.X;
        var height = Size.Y;
        var portraitSize = Mathf.Clamp(height * 0.13f, 96.0f, 116.0f);
        var heroWidth = portraitSize + 34.0f;
        var heroHeight = portraitSize + 18.0f;
        var heroTop = height * 0.715f;
        var centerX = width * 0.50f;

        ResetFreeControl(_referenceHeroCluster);
        _referenceHeroCluster.Position = new Vector2(centerX - heroWidth * 0.5f, heroTop);
        _referenceHeroCluster.Size = new Vector2(heroWidth, heroHeight);
        _referenceHeroCluster.CustomMinimumSize = _referenceHeroCluster.Size;
        _referenceHeroCluster.ZIndex = 44;

        if (_referenceHeroCluster.GetNodeOrNull<PanelContainer>("HeroPortraitFrame") is { } heroFrame)
        {
            heroFrame.AnchorLeft = 0.5f;
            heroFrame.AnchorTop = 0.5f;
            heroFrame.AnchorRight = 0.5f;
            heroFrame.AnchorBottom = 0.5f;
            heroFrame.OffsetLeft = -portraitSize * 0.5f;
            heroFrame.OffsetTop = -portraitSize * 0.5f;
            heroFrame.OffsetRight = portraitSize * 0.5f;
            heroFrame.OffsetBottom = portraitSize * 0.5f;
            heroFrame.ClipContents = false;
        }

        var powerSize = Mathf.Clamp(portraitSize * 0.64f, 62.0f, 76.0f);
        ResetFreeControl(_powerButton);
        _powerButton.Position = new Vector2(
            centerX + portraitSize * 0.52f + 10.0f,
            heroTop + heroHeight * 0.5f - powerSize * 0.5f);
        _powerButton.Size = new Vector2(powerSize, powerSize);
        _powerButton.CustomMinimumSize = _powerButton.Size;
        _powerButton.ZIndex = 45;

        var handTop = height * 0.855f;
        var handHeight = height - handTop;
        ResetFreeControl(_reserveButtons);
        _reserveButtons.Position = new Vector2(width * 0.26f, handTop);
        _reserveButtons.Size = new Vector2(width * 0.48f, handHeight);
        _reserveButtons.CustomMinimumSize = Vector2.Zero;
        _reserveButtons.ClipContents = false;
        _reserveButtons.PreferredCardWidth = Mathf.Clamp(width * 0.052f, 76.0f, 92.0f);
        _reserveButtons.MinimumCardWidth = Mathf.Clamp(width * 0.043f, 66.0f, 78.0f);
        _reserveButtons.PreferredCardHeight = Mathf.Clamp(height * 0.14f, 104.0f, 126.0f);
        _reserveButtons.Gap = 8.0f;
        _reserveButtons.Padding = 0.0f;
        _reserveButtons.ZIndex = 46;
        _reserveButtons.QueueSort();

        var resourceWidth = Mathf.Clamp(width * 0.115f, 150.0f, 194.0f);
        var resourceHeight = Mathf.Clamp(height * 0.055f, 40.0f, 48.0f);
        ResetFreeControl(_hudResourceBadge);
        _hudResourceBadge.Position = new Vector2(width * 0.68f, height - resourceHeight - 10.0f);
        _hudResourceBadge.Size = new Vector2(resourceWidth, resourceHeight);
        _hudResourceBadge.CustomMinimumSize = _hudResourceBadge.Size;
        _hudResourceBadge.ZIndex = 47;

        var readyWidth = Mathf.Clamp(width * 0.067f, 96.0f, 116.0f);
        var readyHeight = Mathf.Clamp(height * 0.068f, 50.0f, 62.0f);
        ResetFreeControl(_endPreparationButton);
        _endPreparationButton.Position = new Vector2(width * 0.755f, height * 0.405f);
        _endPreparationButton.Size = new Vector2(readyWidth, readyHeight);
        _endPreparationButton.CustomMinimumSize = _endPreparationButton.Size;
        _endPreparationButton.ZIndex = 48;
        ApplyBoardReadyStyle(_endPreparationButton);
    }

    private static void ConfigureReferenceRow(
        HorizontalCardRow row,
        float cardWidth,
        float cardHeight,
        float gap,
        float padding)
    {
        row.PreferredCardWidth = cardWidth;
        row.MinimumCardWidth = cardWidth * 0.76f;
        row.PreferredCardHeight = cardHeight;
        row.Gap = gap;
        row.Padding = padding;
        row.CustomMinimumSize = Vector2.Zero;
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        row.QueueSort();
    }

    private static void ReparentBoardControl(Control control, Control parent)
    {
        if (control.GetParent() == parent)
            return;
        control.GetParent()?.RemoveChild(control);
        parent.AddChild(control);
    }

    private static void SetControlFootprint(Control control, float width, float height)
    {
        control.CustomMinimumSize = new Vector2(width, height);
        control.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }

    private static void MakePanelTransparent(PanelContainer panel)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
        });
    }

    private static void ApplyBoardReadyStyle(Button button)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
        };
        var hover = new StyleBoxFlat
        {
            BgColor = new Color(1, 1, 1, 0.08f),
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
        };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("focus", hover);
    }
}
