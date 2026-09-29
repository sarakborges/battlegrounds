using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _preparationPresentationPolishBound;
    private PanelContainer? _preparationReserveShelf;
    private HBoxContainer? _bottomHandRow;
    private Control? _bottomHandBalanceSpacer;
    private Control? _turnButtonOverlay;

    private void RefreshPreparationPresentationPolish()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
            return;

        if (!_preparationPresentationPolishBound)
        {
            BindPreparationPresentationPolish();
            _preparationPresentationPolishBound = true;
        }

        _endPreparationButton.Text = Text("ui.ready");

        if (match.TryGetPlayer(_session.HumanPlayerId, out var human) && _preparationReserveShelf is not null)
        {
            _preparationReserveShelf.Visible = true;
            _reserveButtons.Visible = human.PlayableReserveCount > 0;
        }

        if (!_hudBound)
            return;

        _hudResourcePips.Visible = false;
        _hudResourcePips.Text = string.Empty;
        _hudHeroName.Visible = false;
        if (_hudHeroPortrait.GetParent() is PanelContainer portraitFrame &&
            portraitFrame.GetNodeOrNull<Label>("PortraitNameplate") is { } nameplate)
        {
            nameplate.Visible = false;
        }

        ApplyBattlegroundsHeroComposition();
        ApplyBottomHandComposition();
        ApplyTurnButtonComposition();
    }

    private void BindPreparationPresentationPolish()
    {
        const string preparationPath = "Margin/Shell/CenterStage/PreparationPanel";
        var preparation = GetNode<VBoxContainer>(preparationPath);
        var board = GetNode<PanelContainer>($"{preparationPath}/BoardStage");
        var reserve = GetNode<PanelContainer>($"{preparationPath}/ReserveShelf");
        var heroDock = GetNode<PanelContainer>($"{preparationPath}/HeroDock");
        _preparationReserveShelf = reserve;

        // The board itself owns all spare center height. Do not create a second
        // dashboard-like rectangle between the board and the hero cockpit.
        board.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        heroDock.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        reserve.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;

        if (preparation.GetNodeOrNull<PanelContainer>("TableSpace") is { } obsoleteTableSpace)
        {
            obsoleteTableSpace.Visible = false;
            obsoleteTableSpace.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            obsoleteTableSpace.CustomMinimumSize = Vector2.Zero;
        }

        // Exact bottom grammar from the reference: board -> hero/power -> hand.
        preparation.MoveChild(board, GetPreparationBoardIndex(preparation));
        preparation.MoveChild(heroDock, board.GetIndex() + 1);
        preparation.MoveChild(reserve, heroDock.GetIndex() + 1);

        var heroRow = heroDock.GetNode<HBoxContainer>("HeroDockRow");
        var leftSpacer = heroRow.GetNode<Control>("LeftSpacer");
        var rightSpacer = heroRow.GetNode<Control>("RightSpacer");
        var heroCore = heroRow.GetNode<VBoxContainer>("HeroCore");
        var portraitCluster = heroRow.GetNodeOrNull<Control>("HeroPortraitCluster");
        if (portraitCluster is not null)
        {
            var first = leftSpacer.GetIndex() + 1;
            heroRow.MoveChild(portraitCluster, first);
            heroRow.MoveChild(heroCore, first + 1);
            heroRow.Alignment = BoxContainer.AlignmentMode.Center;
            heroRow.AddThemeConstantOverride("separation", 4);

            leftSpacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rightSpacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            heroCore.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            heroCore.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            heroCore.Alignment = BoxContainer.AlignmentMode.End;
            portraitCluster.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            portraitCluster.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            _powerButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            _powerButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        }

        BuildBottomHandRow(reserve);
        BuildFloatingTurnButton();
    }

    private static int GetPreparationBoardIndex(VBoxContainer preparation)
    {
        var shelf = preparation.GetNodeOrNull<PanelContainer>("TavernShelf");
        return shelf is null ? 0 : shelf.GetIndex() + 1;
    }

    private void BuildBottomHandRow(PanelContainer reserve)
    {
        if (_bottomHandRow is not null)
            return;

        if (_reserveButtons.GetParent() is Node reserveParent)
            reserveParent.RemoveChild(_reserveButtons);
        if (_hudResourceBadge.GetParent() is Node resourceParent)
            resourceParent.RemoveChild(_hudResourceBadge);

        _bottomHandRow = new HBoxContainer
        {
            Name = "BottomHandRow",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _bottomHandRow.AddThemeConstantOverride("separation", 8);
        reserve.AddChild(_bottomHandRow);

        _bottomHandBalanceSpacer = new Control
        {
            Name = "HandBalanceSpacer",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
        };
        _bottomHandRow.AddChild(_bottomHandBalanceSpacer);

        _reserveButtons.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _reserveButtons.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        _bottomHandRow.AddChild(_reserveButtons);

        _hudResourceBadge.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _hudResourceBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        _bottomHandRow.AddChild(_hudResourceBadge);
    }

    private void ApplyBottomHandComposition()
    {
        if (_bottomHandRow is null || _bottomHandBalanceSpacer is null)
            return;

        var resourceWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockResourceBadgeWidth,
            1.0f,
            1024.0f);
        var resourceHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockResourceBadgeHeight,
            1.0f,
            512.0f);
        var handHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardHeight(ModThemeMetricKeys.Row.Reserve),
            1.0f,
            1024.0f);
        var handPadding = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.Padding(ModThemeMetricKeys.Row.Reserve),
            0.0f,
            256.0f);

        _bottomHandBalanceSpacer.CustomMinimumSize = new Vector2(resourceWidth, resourceHeight);
        _hudResourceBadge.CustomMinimumSize = new Vector2(resourceWidth, resourceHeight);
        _reserveButtons.CustomMinimumSize = new Vector2(0.0f, handHeight + handPadding * 2.0f);
        _reserveButtons.QueueSort();
    }

    private void BuildFloatingTurnButton()
    {
        if (_turnButtonOverlay is not null)
            return;

        var turnRail = GetNode<PanelContainer>("Margin/Shell/TurnRail");
        if (_endPreparationButton.GetParent() is Node oldParent)
            oldParent.RemoveChild(_endPreparationButton);

        _turnButtonOverlay = new Control
        {
            Name = "PreparationTurnOverlay",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 30,
        };
        AddChild(_turnButtonOverlay);
        _turnButtonOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _turnButtonOverlay.AddChild(_endPreparationButton);

        turnRail.Visible = false;
        turnRail.CustomMinimumSize = Vector2.Zero;
        ApplyTurnButtonComposition();
    }

    private void ApplyTurnButtonComposition()
    {
        if (_turnButtonOverlay is null)
            return;

        var buttonHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.TurnRailEndButtonHeight,
            1.0f,
            512.0f);
        var width = Mathf.Max(92.0f, buttonHeight * 1.45f);

        _endPreparationButton.AnchorLeft = 1.0f;
        _endPreparationButton.AnchorTop = 0.5f;
        _endPreparationButton.AnchorRight = 1.0f;
        _endPreparationButton.AnchorBottom = 0.5f;
        _endPreparationButton.OffsetLeft = -width - 18.0f;
        _endPreparationButton.OffsetTop = -buttonHeight * 0.5f;
        _endPreparationButton.OffsetRight = -18.0f;
        _endPreparationButton.OffsetBottom = buttonHeight * 0.5f;
        _endPreparationButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _endPreparationButton.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }

    private void ApplyBattlegroundsHeroComposition()
    {
        const string heroRowPath = "Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroDockRow";
        var heroRow = GetNodeOrNull<HBoxContainer>(heroRowPath);
        var portraitCluster = heroRow?.GetNodeOrNull<Control>("HeroPortraitCluster");
        var frame = portraitCluster?.GetNodeOrNull<PanelContainer>("HeroPortraitFrame");
        if (heroRow is null || portraitCluster is null || frame is null)
            return;

        var portraitSize = ResolvePresentationMetric(
            ModThemeMetricKeys.Hud.HeroPortraitSize,
            1.0f,
            2048.0f);
        var healthWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockHealthBadgeWidth,
            1.0f,
            512.0f);
        var armorWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockArmorBadgeWidth,
            1.0f,
            512.0f);

        var badgeAllowance = Mathf.Max(healthWidth, armorWidth) * 0.28f;
        portraitCluster.CustomMinimumSize = new Vector2(
            portraitSize + badgeAllowance,
            portraitSize + badgeAllowance * 0.35f);

        frame.AnchorLeft = 0.5f;
        frame.AnchorTop = 0.5f;
        frame.AnchorRight = 0.5f;
        frame.AnchorBottom = 0.5f;
        frame.OffsetLeft = -portraitSize * 0.5f;
        frame.OffsetTop = -portraitSize * 0.5f;
        frame.OffsetRight = portraitSize * 0.5f;
        frame.OffsetBottom = portraitSize * 0.5f;
        frame.ClipContents = true;
        _hudHeroPortrait.CustomMinimumSize = new Vector2(portraitSize, portraitSize);
        _hudHeroPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;

        // The SVG overlay is the actual portrait frame. Keep the PanelContainer
        // itself transparent so we do not draw a second competing frame behind it.
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthTop = 0,
            BorderWidthRight = 0,
            BorderWidthBottom = 0,
        });

        ApplyHeroPowerStyle(portraitSize);
    }

    private void ApplyHeroPowerStyle(float portraitSize)
    {
        var powerSize = Mathf.Clamp(portraitSize * 0.68f, 58.0f, 92.0f);
        _powerButton.CustomMinimumSize = new Vector2(powerSize, powerSize);
        _powerButton.Text = "✦";
        _powerButton.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(powerSize * 0.34f));

        ResolveThemeColor("surfaceCool", out var normalColor);
        ResolveThemeColor("surfaceHover", out var hoverColor);
        ResolveThemeColor("surfacePressed", out var pressedColor);
        ResolveThemeColor("focus", out var borderColor);
        ResolveThemeColor("text", out var textColor);
        _powerButton.AddThemeColorOverride("font_color", textColor);
        _powerButton.AddThemeColorOverride("font_hover_color", textColor);
        _powerButton.AddThemeColorOverride("font_pressed_color", textColor);

        var radius = Mathf.RoundToInt(powerSize * 0.5f);
        _powerButton.AddThemeStyleboxOverride("normal", CreateRoundPowerBox(normalColor, borderColor, radius, 3));
        _powerButton.AddThemeStyleboxOverride("hover", CreateRoundPowerBox(hoverColor, borderColor, radius, 4));
        _powerButton.AddThemeStyleboxOverride("pressed", CreateRoundPowerBox(pressedColor, borderColor, radius, 3));
        _powerButton.AddThemeStyleboxOverride("focus", CreateRoundPowerBox(hoverColor, borderColor, radius, 4));
        _powerButton.AddThemeStyleboxOverride("disabled", CreateRoundPowerBox(normalColor.Darkened(0.25f), borderColor.Darkened(0.25f), radius, 2));
    }

    private static StyleBoxFlat CreateRoundPowerBox(
        Color background,
        Color border,
        int radius,
        int borderWidth)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 4,
            ContentMarginTop = 4,
            ContentMarginRight = 4,
            ContentMarginBottom = 4,
        };
    }
}
