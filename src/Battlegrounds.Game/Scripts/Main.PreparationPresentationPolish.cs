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
            // The hand belongs to the bottom cockpit. Keep that bottom bar present
            // even when the hand is empty because the resource display still lives
            // there, exactly like the Battlegrounds reference.
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
    }

    private void BindPreparationPresentationPolish()
    {
        const string preparationPath = "Margin/Shell/CenterStage/PreparationPanel";
        var preparation = GetNode<VBoxContainer>(preparationPath);
        var board = GetNode<PanelContainer>($"{preparationPath}/BoardStage");
        var reserve = GetNode<PanelContainer>($"{preparationPath}/ReserveShelf");
        var heroDock = GetNode<PanelContainer>($"{preparationPath}/HeroDock");
        _preparationReserveShelf = reserve;

        board.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        heroDock.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        reserve.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;

        var tableSpace = preparation.GetNodeOrNull<PanelContainer>("TableSpace");
        if (tableSpace is null)
        {
            tableSpace = new PanelContainer
            {
                Name = "TableSpace",
                ThemeTypeVariation = "BoardSurface",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            preparation.AddChild(tableSpace);
        }

        // Reference grammar, top to bottom: board -> open table -> hero/power -> hand.
        // The hand is the actual bottom row, not a strip above the hero.
        preparation.MoveChild(tableSpace, board.GetIndex() + 1);
        preparation.MoveChild(heroDock, tableSpace.GetIndex() + 1);
        preparation.MoveChild(reserve, heroDock.GetIndex() + 1);

        var heroRow = heroDock.GetNode<HBoxContainer>("HeroDockRow");
        var leftSpacer = heroRow.GetNode<Control>("LeftSpacer");
        var heroCore = heroRow.GetNode<VBoxContainer>("HeroCore");
        var portraitCluster = heroRow.GetNodeOrNull<Control>("HeroPortraitCluster");
        if (portraitCluster is not null)
        {
            var first = leftSpacer.GetIndex() + 1;
            heroRow.MoveChild(portraitCluster, first);
            heroRow.MoveChild(heroCore, first + 1);
            heroRow.Alignment = BoxContainer.AlignmentMode.Center;

            heroCore.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            heroCore.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            heroCore.Alignment = BoxContainer.AlignmentMode.End;
            portraitCluster.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            portraitCluster.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            _powerButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            _powerButton.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        }

        BuildBottomHandRow(reserve);

        var turnRail = GetNode<PanelContainer>("Margin/Shell/TurnRail");
        turnRail.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _endPreparationButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
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
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
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

        // The reference uses a compact framed hero above the bottom hand, not a
        // tall portrait card. Keep this footprint square so it can share the same
        // frame grammar used by the bartender and leader selection.
        var badgeAllowance = Mathf.Max(healthWidth, armorWidth) * 0.42f;
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

        ApplyHeroPortraitFrameStyle(frame, portraitSize);
        ApplyHeroPowerStyle(portraitSize, portraitSize);
    }

    private void ApplyHeroPortraitFrameStyle(PanelContainer frame, float portraitWidth)
    {
        if (!ResolveThemeColor("surfaceDeep", out var background))
            background = new Color(0.08f, 0.05f, 0.04f, 1.0f);
        if (!ResolveThemeColor("focus", out var border))
            border = new Color(0.9f, 0.68f, 0.38f, 1.0f);

        var radius = Mathf.RoundToInt(portraitWidth * 0.18f);
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 3,
            ContentMarginTop = 3,
            ContentMarginRight = 3,
            ContentMarginBottom = 3,
        };
        frame.AddThemeStyleboxOverride("panel", box);
    }

    private void ApplyHeroPowerStyle(float portraitWidth, float portraitHeight)
    {
        var powerSize = Mathf.Clamp(
            Mathf.Min(portraitWidth, portraitHeight) * 0.72f,
            54.0f,
            96.0f);
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
