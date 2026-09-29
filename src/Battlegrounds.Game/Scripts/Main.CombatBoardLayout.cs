using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private Control? _combatBoardOverlay;
    private VBoxContainer? _combatTopBoardReference;
    private VBoxContainer? _combatBottomBoardReference;
    private HBoxContainer? _combatControlsReference;

    private void RefreshCombatBoardLayout()
    {
        if (_combatOverlay?.Visible != true || _combatPlayback is null)
        {
            if (_combatBoardOverlay is not null)
                _combatBoardOverlay.Visible = false;
            return;
        }

        EnsureCombatBoardOverlay();
        if (_combatBoardOverlay is null || _combatTopBoardReference is null || _combatBottomBoardReference is null)
            return;

        _combatBoardOverlay.Visible = true;
        MakePanelTransparent(_combatOverlay);

        var width = Size.X;
        var height = Size.Y;
        if (width <= 1.0f || height <= 1.0f)
            return;

        // Two explicit battlefield bands. Seven minions fit in either row without
        // allowing one or two units to inflate into the middle of the whole screen.
        LayoutCombatBoard(
            _combatTopBoardReference,
            new Rect2(width * 0.20f, height * 0.055f, width * 0.62f, height * 0.36f));
        LayoutCombatBoard(
            _combatBottomBoardReference,
            new Rect2(width * 0.20f, height * 0.505f, width * 0.62f, height * 0.36f));

        if (_combatTopHeroPortrait is not null)
            ConfigureCombatHeroFootprint(_combatTopHeroPortrait, height);
        if (_combatBottomHeroPortrait is not null)
            ConfigureCombatHeroFootprint(_combatBottomHeroPortrait, height);

        ConfigureCombatUnitRow(_combatRightUnits, width, height);
        ConfigureCombatUnitRow(_combatLeftUnits, width, height);

        if (_combatEvent is not null)
        {
            ResetFreeControl(_combatEvent);
            _combatEvent.Position = new Vector2(width * 0.25f, height * 0.445f);
            _combatEvent.Size = new Vector2(width * 0.50f, Mathf.Clamp(height * 0.055f, 38.0f, 52.0f));
            _combatEvent.ZIndex = 20;
            _combatEvent.HorizontalAlignment = HorizontalAlignment.Center;
            _combatEvent.VerticalAlignment = VerticalAlignment.Center;
        }

        if (_combatControlsReference is not null)
        {
            ResetFreeControl(_combatControlsReference);
            var controlsWidth = Mathf.Clamp(width * 0.22f, 300.0f, 380.0f);
            var controlsHeight = 48.0f;
            _combatControlsReference.Position = new Vector2(
                (width - controlsWidth) * 0.5f,
                height - controlsHeight - 8.0f);
            _combatControlsReference.Size = new Vector2(controlsWidth, controlsHeight);
            _combatControlsReference.CustomMinimumSize = Vector2.Zero;
            _combatControlsReference.Alignment = BoxContainer.AlignmentMode.Center;
            _combatControlsReference.ZIndex = 25;
        }
    }

    private void EnsureCombatBoardOverlay()
    {
        if (_combatOverlay is null)
            return;

        _combatBoardOverlay ??= _combatOverlay.GetNodeOrNull<Control>("CombatBoardReferenceOverlay");
        if (_combatBoardOverlay is null)
        {
            _combatBoardOverlay = new Control
            {
                Name = "CombatBoardReferenceOverlay",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 10,
            };
            _combatOverlay.AddChild(_combatBoardOverlay);
            _combatBoardOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        _combatTopBoardReference ??= _combatOverlay.FindChild("CombatTopBoard", recursive: true, owned: false) as VBoxContainer;
        _combatBottomBoardReference ??= _combatOverlay.FindChild("CombatBottomBoard", recursive: true, owned: false) as VBoxContainer;
        _combatControlsReference ??= _combatOverlay.FindChild("CombatControls", recursive: true, owned: false) as HBoxContainer;

        if (_combatTopBoardReference is not null)
            ReparentBoardControl(_combatTopBoardReference, _combatBoardOverlay);
        if (_combatBottomBoardReference is not null)
            ReparentBoardControl(_combatBottomBoardReference, _combatBoardOverlay);
        if (_combatEvent is not null)
            ReparentBoardControl(_combatEvent, _combatBoardOverlay);
        if (_combatControlsReference is not null)
            ReparentBoardControl(_combatControlsReference, _combatBoardOverlay);
    }

    private static void LayoutCombatBoard(VBoxContainer board, Rect2 rect)
    {
        ResetFreeControl(board);
        board.Position = rect.Position;
        board.Size = rect.Size;
        board.CustomMinimumSize = Vector2.Zero;
        board.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        board.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        board.Alignment = BoxContainer.AlignmentMode.Center;
        board.AddThemeConstantOverride("separation", 4);
        board.ZIndex = 15;
    }

    private static void ConfigureCombatHeroFootprint(PanelContainer hero, float viewportHeight)
    {
        var side = Mathf.Clamp(viewportHeight * 0.105f, 78.0f, 98.0f);
        FramedCosmeticPortrait.ConfigureSquare(hero, side);
        hero.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        hero.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        hero.ZIndex = 18;
    }

    private static void ConfigureCombatUnitRow(HBoxContainer? units, float viewportWidth, float viewportHeight)
    {
        if (units is null)
            return;

        units.Alignment = BoxContainer.AlignmentMode.Center;
        units.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        units.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        units.AddThemeConstantOverride("separation", Mathf.RoundToInt(Mathf.Clamp(viewportWidth * 0.006f, 6.0f, 10.0f)));

        var tokenWidth = Mathf.Clamp(viewportWidth * 0.057f, 84.0f, 102.0f);
        var tokenHeight = Mathf.Clamp(viewportHeight * 0.145f, 106.0f, 128.0f);
        foreach (var card in units.GetChildren().OfType<PresentationCardButton>())
        {
            card.CustomMinimumSize = new Vector2(tokenWidth, tokenHeight);
            card.Size = card.CustomMinimumSize;
        }
    }
}
