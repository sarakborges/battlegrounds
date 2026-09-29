using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _referenceCockpitBound;
    private Control? _referenceCockpit;
    private Control? _referenceHeroCluster;
    private Button? _referenceCombineButton;
    private PanelContainer? _referenceHeroDock;
    private PanelContainer? _referenceReserveShelf;
    private Control? _referenceCenterStage;

    private void RefreshReferenceCockpitLayout()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
        {
            if (_referenceCockpit is not null)
                _referenceCockpit.Visible = false;
            return;
        }

        if (!_hudBound)
            return;

        if (!_referenceCockpitBound)
        {
            BindReferenceCockpitLayout();
            _referenceCockpitBound = true;
        }

        if (_referenceCockpit is null || _referenceCenterStage is null || _referenceHeroCluster is null)
            return;

        _referenceCockpit.Visible = true;
        ApplyReferenceCockpitGeometry();
    }

    private void BindReferenceCockpitLayout()
    {
        const string preparationPath = "Margin/Shell/CenterStage/PreparationPanel";
        _referenceCenterStage = GetNodeOrNull<Control>("Margin/Shell/CenterStage");
        _referenceHeroDock = GetNodeOrNull<PanelContainer>($"{preparationPath}/HeroDock");
        _referenceReserveShelf = GetNodeOrNull<PanelContainer>($"{preparationPath}/ReserveShelf");
        _referenceHeroCluster = _hudHeroPortrait.GetParent()?.GetParent() as Control;
        _referenceCombineButton = GetNodeOrNull<Button>("%CombineButton");

        if (_referenceCenterStage is null || _referenceHeroCluster is null)
            return;

        _referenceCockpit = GetNodeOrNull<Control>("ReferenceCockpitOverlay");
        if (_referenceCockpit is null)
        {
            _referenceCockpit = new Control
            {
                Name = "ReferenceCockpitOverlay",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 40,
            };
            AddChild(_referenceCockpit);
        }

        ReparentForReferenceLayout(_referenceHeroCluster, _referenceCockpit);
        ReparentForReferenceLayout(_powerButton, _referenceCockpit);
        ReparentForReferenceLayout(_reserveButtons, _referenceCockpit);
        ReparentForReferenceLayout(_hudResourceBadge, _referenceCockpit);
        if (_referenceCombineButton is not null)
            ReparentForReferenceLayout(_referenceCombineButton, _referenceCockpit);
        ReparentForReferenceLayout(_endPreparationButton, _referenceCockpit);

        if (_referenceHeroDock is not null)
        {
            _referenceHeroDock.Visible = false;
            _referenceHeroDock.CustomMinimumSize = Vector2.Zero;
        }

        if (_referenceReserveShelf is not null)
        {
            _referenceReserveShelf.Visible = false;
            _referenceReserveShelf.CustomMinimumSize = Vector2.Zero;
        }

        if (_turnButtonOverlay is not null)
            _turnButtonOverlay.Visible = false;
    }

    private void ApplyReferenceCockpitGeometry()
    {
        if (_referenceCockpit is null || _referenceCenterStage is null || _referenceHeroCluster is null)
            return;

        var rootRect = GetGlobalRect();
        var centerRect = _referenceCenterStage.GetGlobalRect();
        _referenceCockpit.Position = centerRect.Position - rootRect.Position;
        _referenceCockpit.Size = centerRect.Size;

        var width = _referenceCockpit.Size.X;
        var height = _referenceCockpit.Size.Y;
        if (width <= 1.0f || height <= 1.0f)
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
        var handCardHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.PreferredCardHeight(ModThemeMetricKeys.Row.Reserve),
            1.0f,
            1024.0f);
        var handPadding = ResolvePresentationMetric(
            ModThemeMetricKeys.Row.Padding(ModThemeMetricKeys.Row.Reserve),
            0.0f,
            256.0f);
        var resourceWidth = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockResourceBadgeWidth,
            1.0f,
            1024.0f);
        var resourceHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.HeroDockResourceBadgeHeight,
            1.0f,
            512.0f);
        var readyHeight = ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.TurnRailEndButtonHeight,
            1.0f,
            512.0f);

        var handHeight = Mathf.Max(72.0f, handCardHeight + handPadding * 2.0f);
        var badgeAllowance = Mathf.Max(healthWidth, armorWidth) * 0.30f;
        var heroWidth = portraitSize + badgeAllowance;
        var heroHeight = portraitSize + badgeAllowance * 0.45f;
        var centerX = width * 0.5f;
        var handTop = height - handHeight;
        var heroOverlap = Mathf.Min(18.0f, handHeight * 0.20f);
        var heroY = handTop - heroHeight + heroOverlap;

        ResetFreeControl(_referenceHeroCluster);
        _referenceHeroCluster.Position = new Vector2(centerX - heroWidth * 0.5f, heroY);
        _referenceHeroCluster.Size = new Vector2(heroWidth, heroHeight);
        _referenceHeroCluster.CustomMinimumSize = _referenceHeroCluster.Size;

        var powerSize = Mathf.Clamp(portraitSize * 0.68f, 58.0f, 96.0f);
        ResetFreeControl(_powerButton);
        _powerButton.Position = new Vector2(
            centerX + portraitSize * 0.5f + 10.0f,
            heroY + heroHeight * 0.5f - powerSize * 0.5f);
        _powerButton.Size = new Vector2(powerSize, powerSize);
        _powerButton.CustomMinimumSize = _powerButton.Size;

        ResetFreeControl(_reserveButtons);
        _reserveButtons.Position = new Vector2(0.0f, handTop);
        _reserveButtons.Size = new Vector2(width, handHeight);
        _reserveButtons.CustomMinimumSize = new Vector2(0.0f, handHeight);
        _reserveButtons.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _reserveButtons.QueueSort();

        ResetFreeControl(_hudResourceBadge);
        var resolvedResourceWidth = Mathf.Min(resourceWidth, Mathf.Max(96.0f, width * 0.16f));
        _hudResourceBadge.Position = new Vector2(
            width - resolvedResourceWidth - 18.0f,
            height - resourceHeight - 10.0f);
        _hudResourceBadge.Size = new Vector2(resolvedResourceWidth, resourceHeight);
        _hudResourceBadge.CustomMinimumSize = _hudResourceBadge.Size;

        if (_referenceCombineButton is not null)
        {
            var combineWidth = ResolvePresentationMetric(
                ModThemeMetricKeys.Layout.HeroDockCombineButtonWidth,
                1.0f,
                512.0f);
            var combineHeight = ResolvePresentationMetric(
                ModThemeMetricKeys.Layout.HeroDockCombineButtonHeight,
                1.0f,
                512.0f);
            ResetFreeControl(_referenceCombineButton);
            _referenceCombineButton.Position = new Vector2(
                width - resolvedResourceWidth - combineWidth - 28.0f,
                height - combineHeight - 14.0f);
            _referenceCombineButton.Size = new Vector2(combineWidth, combineHeight);
        }

        var readyWidth = Mathf.Max(92.0f, readyHeight * 1.45f);
        ResetFreeControl(_endPreparationButton);
        _endPreparationButton.Position = new Vector2(
            width - readyWidth - 10.0f,
            Mathf.Clamp(height * 0.43f - readyHeight * 0.5f, 8.0f, height - readyHeight - 8.0f));
        _endPreparationButton.Size = new Vector2(readyWidth, readyHeight);
        _endPreparationButton.CustomMinimumSize = _endPreparationButton.Size;
    }

    private static void ReparentForReferenceLayout(Control control, Control parent)
    {
        if (control.GetParent() == parent)
            return;

        control.GetParent()?.RemoveChild(control);
        parent.AddChild(control);
    }

    private static void ResetFreeControl(Control control)
    {
        control.AnchorLeft = 0.0f;
        control.AnchorTop = 0.0f;
        control.AnchorRight = 0.0f;
        control.AnchorBottom = 0.0f;
        control.OffsetLeft = 0.0f;
        control.OffsetTop = 0.0f;
        control.OffsetRight = 0.0f;
        control.OffsetBottom = 0.0f;
        control.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }
}
