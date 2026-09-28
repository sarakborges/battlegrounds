using Godot;

namespace Battlegrounds.Game;

internal sealed partial class PresentationCardButton : Button
{
    private const float DragThreshold = 5.0f;

    private readonly TextureRect _art;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _stats;
    private readonly Label _description;
    private string? _dragPayload;
    private bool _dragEnabled;
    private bool _dragActive;
    private bool _dragPointerDown;
    private Vector2 _dragPressPosition;
    private Action? _dragStarted;
    private Action? _dragEnded;

    public PresentationCardButton()
    {
        Text = string.Empty;
        ThemeTypeVariation = "CardButton";
        CustomMinimumSize = new Vector2(92, 0);
        ClipContents = true;

        var margin = IgnoreMouse(new MarginContainer());
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var column = IgnoreMouse(new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        column.AddThemeConstantOverride("separation", 2);
        margin.AddChild(column);

        _art = IgnoreMouse(new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        });
        column.AddChild(_art);

        _title = CreateLabel("HeadingLabel");
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        _subtitle = CreateLabel("CaptionLabel");
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;

        _stats = CreateLabel("BodyLabel");
        _stats.HorizontalAlignment = HorizontalAlignment.Center;

        _description = CreateLabel("CaptionLabel", wrap: true);
        _description.Visible = false;

        column.AddChild(_title);
        column.AddChild(_subtitle);
        column.AddChild(_stats);
        column.AddChild(_description);
    }

    public override void _Ready()
    {
        ApplyFootprintForParent();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!_dragEnabled || _dragActive || string.IsNullOrWhiteSpace(_dragPayload))
            return;

        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _dragPointerDown = mouseButton.Pressed;
            if (mouseButton.Pressed)
                _dragPressPosition = mouseButton.Position;
            return;
        }

        if (!_dragPointerDown || @event is not InputEventMouseMotion motion)
            return;

        if (motion.Position.DistanceTo(_dragPressPosition) < DragThreshold)
            return;

        _dragPointerDown = false;
        BeginDrag();
        ForceDrag(_dragPayload!, CreateDragPreview(_dragPressPosition));
        AcceptEvent();
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!_dragEnabled || _dragActive || string.IsNullOrWhiteSpace(_dragPayload))
            return default;

        _dragPointerDown = false;
        BeginDrag();
        SetDragPreview(CreateDragPreview(atPosition));
        return _dragPayload;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_dragActive)
            return;

        _dragActive = false;
        _dragPointerDown = false;
        SelfModulate = Colors.White;
        _dragEnded?.Invoke();
    }

    public void Configure(
        string title,
        string subtitle,
        string stats,
        string? description,
        Texture2D? texture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        _title.Text = title;
        SetOptionalText(_subtitle, subtitle);
        SetOptionalText(_stats, stats);
        _description.Text = description ?? string.Empty;
        _description.Visible = false;
        TooltipText = string.IsNullOrWhiteSpace(description) ? title : $"{title}\n{description}";

        _art.Texture = texture;
        _art.Visible = texture is not null;
    }

    public void ConfigureOfferDrag(int slot, bool enabled, Action? dragEnded = null) =>
        ConfigureDrag(PreparationDragPayload.Offer(slot), enabled, null, dragEnded);

    public void ConfigureReserveUnitDrag(int slot, bool enabled, Action? dragEnded = null) =>
        ConfigureDrag(PreparationDragPayload.ReserveUnit(slot), enabled, null, dragEnded);

    public void ConfigureFieldDrag(
        int index,
        bool enabled,
        Action<int> dragStarted,
        Action dragEnded) =>
        ConfigureDrag(
            PreparationDragPayload.Field(index),
            enabled,
            () => dragStarted(index),
            dragEnded);

    public void SetSelected(bool selected)
    {
        ToggleMode = true;
        ButtonPressed = selected;
    }

    private void ConfigureDrag(string payload, bool enabled, Action? dragStarted, Action? dragEnded)
    {
        _dragPayload = payload;
        _dragEnabled = enabled;
        _dragStarted = dragStarted;
        _dragEnded = dragEnded;
        MouseDefaultCursorShape = enabled ? CursorShape.PointingHand : CursorShape.Arrow;
        ButtonMask = enabled ? (MouseButtonMask)0 : MouseButtonMask.Left;
        FocusMode = enabled ? FocusModeEnum.None : FocusModeEnum.All;

        if (!enabled && !_dragActive)
        {
            _dragPointerDown = false;
            SelfModulate = Colors.White;
        }
    }

    private void BeginDrag()
    {
        _dragActive = true;
        SelfModulate = new Color(1, 1, 1, 0.10f);
        _dragStarted?.Invoke();
    }

    private Control CreateDragPreview(Vector2 grabOffset)
    {
        var root = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };

        var preview = new PresentationCardButton
        {
            Position = -grabOffset,
            Size = Size,
            CustomMinimumSize = Size,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
            ButtonMask = (MouseButtonMask)0,
            PivotOffset = grabOffset,
            Scale = new Vector2(1.045f, 1.045f),
            Rotation = Mathf.DegToRad(-1.5f),
            TooltipText = string.Empty,
        };
        preview.Configure(
            _title.Text,
            _subtitle.Text,
            _stats.Text,
            _description.Text,
            _art.Texture);
        root.AddChild(preview);
        return root;
    }

    private void ApplyFootprintForParent()
    {
        var parentName = GetParent()?.Name.ToString();
        switch (parentName)
        {
            case "LeaderButtons":
                ApplyFootprint(156, 118, showSubtitle: false, "leader");
                break;
            case "OfferButtons":
                ApplyFootprint(118, 76, showSubtitle: false, "shop");
                break;
            case "FieldButtons":
                ApplyFootprint(138, 112, showSubtitle: false, "board");
                ButtonMask = (MouseButtonMask)0;
                FocusMode = FocusModeEnum.None;
                break;
            case "ReserveButtons":
                ApplyFootprint(90, 34, showSubtitle: false, "reserve");
                break;
            case "InteractionButtons":
                ApplyFootprint(124, 82, showSubtitle: false, "choice");
                break;
            default:
                SetMeta("presentation_footprint", "default");
                break;
        }
    }

    private void ApplyFootprint(float minimumWidth, float artHeight, bool showSubtitle, string role)
    {
        CustomMinimumSize = new Vector2(minimumWidth, 0);
        _art.CustomMinimumSize = new Vector2(0, artHeight);
        _subtitle.Visible = showSubtitle && !string.IsNullOrWhiteSpace(_subtitle.Text);
        SetMeta("presentation_footprint", role);
    }

    private static Label CreateLabel(string variation, bool wrap = false)
    {
        var label = IgnoreMouse(new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = variation,
        });
        if (wrap)
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static void SetOptionalText(Label label, string? value)
    {
        label.Text = value ?? string.Empty;
        label.Visible = !string.IsNullOrWhiteSpace(value);
    }

    private static T IgnoreMouse<T>(T control)
        where T : Control
    {
        control.MouseFilter = MouseFilterEnum.Ignore;
        return control;
    }
}
