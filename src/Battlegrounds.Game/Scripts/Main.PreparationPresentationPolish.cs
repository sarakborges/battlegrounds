using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _preparationPresentationPolishBound;
    private PanelContainer? _preparationReserveShelf;

    private void RefreshPreparationPresentationPolish()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
            return;

        if (!_preparationPresentationPolishBound)
        {
            BindPreparationPresentationPolish();
            _preparationPresentationPolishBound = true;
        }

        // A short readiness label keeps the turn control from forcing a giant
        // right-hand rail. The command is still EndPreparationCommand; this is
        // presentation only.
        _endPreparationButton.Text = Text("ui.ready");

        if (match.TryGetPlayer(_session.HumanPlayerId, out var human) && _preparationReserveShelf is not null)
        {
            // An empty hand should not read as a permanent dashboard strip. The
            // reserve shelf returns as soon as there is something to show.
            _preparationReserveShelf.Visible = human.PlayableReserveCount > 0;
        }

        if (!_hudBound)
            return;

        // Until the mod provides coin art, the numeric current/max resource is
        // substantially cleaner than the unicode dot row (which several fonts
        // render as an ellipsis-like cluster).
        _hudResourcePips.Visible = false;
        _hudResourcePips.Text = string.Empty;

        // The leader name is already available via the portrait tooltip and the
        // opponent rail. Keeping another nameplate inside the compact hero
        // portrait caused the fallback monogram, name, health, and armor to
        // overlap.
        _hudHeroName.Visible = false;
        if (_hudHeroPortrait.GetParent() is PanelContainer portraitFrame &&
            portraitFrame.GetNodeOrNull<Label>("PortraitNameplate") is { } nameplate)
        {
            nameplate.Visible = false;
        }

        ApplyBattlegroundsHeroComposition();
    }

    private void BindPreparationPresentationPolish()
    {
        const string preparationPath = "Margin/Shell/CenterStage/PreparationPanel";
        var preparation = GetNode<VBoxContainer>(preparationPath);
        var board = GetNode<PanelContainer>($"{preparationPath}/BoardStage");
        var reserve = GetNode<PanelContainer>($"{preparationPath}/ReserveShelf");
        var heroDock = GetNode<PanelContainer>($"{preparationPath}/HeroDock");
        _preparationReserveShelf = reserve;

        // Board/hand should occupy their semantic rows, not consume every spare
        // vertical pixel. The remaining height is still part of the board surface,
        // so the center reads as one table instead of stacked dashboard panels.
        board.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        reserve.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        heroDock.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;

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
            preparation.MoveChild(tableSpace, heroDock.GetIndex());
        }

        // Battlegrounds bottom grammar: portrait is the visual anchor; Hero Power
        // sits immediately to its right and the resource readout follows as a
        // compact secondary element. Health/armor remain attached to the portrait.
        var heroRow = heroDock.GetNode<HBoxContainer>("HeroDockRow");
        var leftSpacer = heroRow.GetNode<Control>("LeftSpacer");
        var heroCore = heroRow.GetNode<VBoxContainer>("HeroCore");
        var portraitCluster = heroRow.GetNodeOrNull<Control>("HeroPortraitCluster");
        var resourceBadge = heroRow.GetNode<PanelContainer>("ResourceBadge");
        if (portraitCluster is not null)
        {
            var first = leftSpacer.GetIndex() + 1;
            heroRow.MoveChild(portraitCluster, first);
            heroRow.MoveChild(heroCore, first + 1);
            heroRow.MoveChild(resourceBadge, first + 2);
            heroRow.Alignment = BoxContainer.AlignmentMode.Center;

            heroCore.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            heroCore.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            heroCore.Alignment = BoxContainer.AlignmentMode.End;
            portraitCluster.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            portraitCluster.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            resourceBadge.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            resourceBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            _powerButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            _powerButton.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        }

        var turnRail = GetNode<PanelContainer>("Margin/Shell/TurnRail");
        turnRail.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _endPreparationButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
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
        var dockHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Hud.HeroDockMinimumHeight,
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

        // Use the dock's own semantic height to give the portrait a vertical hero
        // silhouette instead of the square dashboard thumbnail we had before.
        var portraitWidth = portraitSize;
        var portraitHeight = Mathf.Max(portraitSize, dockHeight - 8.0f);
        var badgeAllowance = Mathf.Max(healthWidth, armorWidth) * 0.42f;
        portraitCluster.CustomMinimumSize = new Vector2(
            portraitWidth + badgeAllowance,
            portraitHeight + badgeAllowance * 0.35f);

        frame.AnchorLeft = 0.5f;
        frame.AnchorTop = 0.5f;
        frame.AnchorRight = 0.5f;
        frame.AnchorBottom = 0.5f;
        frame.OffsetLeft = -portraitWidth * 0.5f;
        frame.OffsetTop = -portraitHeight * 0.5f;
        frame.OffsetRight = portraitWidth * 0.5f;
        frame.OffsetBottom = portraitHeight * 0.5f;
        frame.ClipContents = true;
        _hudHeroPortrait.CustomMinimumSize = new Vector2(portraitWidth, portraitHeight);
        _hudHeroPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;

        ApplyHeroPortraitFrameStyle(frame, portraitWidth);
        ApplyHeroPowerStyle(portraitWidth, portraitHeight);

        _hudResourceBadge.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _hudResourceBadge.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
    }

    private void ApplyHeroPortraitFrameStyle(PanelContainer frame, float portraitWidth)
    {
        if (!ResolveThemeColor("surfaceDeep", out var background))
            background = new Color(0.08f, 0.05f, 0.04f, 1.0f);
        if (!ResolveThemeColor("focus", out var border))
            border = new Color(0.9f, 0.68f, 0.38f, 1.0f);

        var topRadius = Mathf.RoundToInt(portraitWidth * 0.28f);
        var bottomRadius = Mathf.RoundToInt(portraitWidth * 0.08f);
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 4,
            BorderWidthTop = 4,
            BorderWidthRight = 4,
            BorderWidthBottom = 4,
            CornerRadiusTopLeft = topRadius,
            CornerRadiusTopRight = topRadius,
            CornerRadiusBottomLeft = bottomRadius,
            CornerRadiusBottomRight = bottomRadius,
            ContentMarginLeft = 4,
            ContentMarginTop = 4,
            ContentMarginRight = 4,
            ContentMarginBottom = 4,
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
