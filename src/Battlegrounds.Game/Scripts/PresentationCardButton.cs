using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

internal sealed partial class PresentationCardButton : Button
{
    private const float DragThreshold = 5.0f;

    private readonly TextureRect _art;
    private readonly VBoxContainer _column;
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
        ClipContents = true;

        var margin = IgnoreMouse(new MarginContainer());
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _column = IgnoreMouse(new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        margin.AddChild(_column);

        _art = IgnoreMouse(new TextureRect
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        });
        _column.AddChild(_art);

        _title = CreateLabel("HeadingLabel");
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        _subtitle = CreateLabel("CaptionLabel");
        _subtitle.HorizontalAlignment = HorizontalAlignment.Center;

        _stats = CreateLabel("BodyLabel");
        _stats.HorizontalAlignment = HorizontalAlignment.Center;

        _description = CreateLabel("CaptionLabel", wrap: true);
        _description.Visible = false;

        _column.AddChild(_title);
        _column.AddChild(_subtitle);
        _column.AddChild(_stats);
        _column.AddChild(_description);
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
        MouseDefaultCursorShape = enabled
            ? FindMain()?.PreparationDragCursor ?? CursorShape.PointingHand
            : CursorShape.Arrow;
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
        var main = FindMain() ?? throw new InvalidOperationException("Presentation card is not attached to Main.");
        SelfModulate = new Color(1, 1, 1, main.PreparationDragSourceOpacity);
        _dragStarted?.Invoke();
    }

    private Control CreateDragPreview(Vector2 grabOffset)
    {
        var main = FindMain() ?? throw new InvalidOperationException("Presentation card is not attached to Main.");
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
            Scale = new Vector2(main.PreparationDragPreviewScale, main.PreparationDragPreviewScale),
            Rotation = Mathf.DegToRad(main.PreparationDragPreviewRotationDegrees),
            TooltipText = string.Empty,
        };
        preview.SetMeta("presentation_skip_footprint", true);
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
        if (HasMeta("presentation_skip_footprint"))
            return;

        var parentName = GetParent()?.Name.ToString();
        var (role, disablePointerInteraction) = parentName switch
        {
            "LeaderButtons" => (ModThemeMetricKeys.Card.LeaderRole, false),
            "OfferButtons" => (ModThemeMetricKeys.Card.ShopRole, false),
            "FieldButtons" => (ModThemeMetricKeys.Card.BoardRole, true),
            "ReserveButtons" => (ModThemeMetricKeys.Card.ReserveRole, false),
            "InteractionButtons" => (ModThemeMetricKeys.Card.ChoiceRole, false),
            _ => (ModThemeMetricKeys.Card.DefaultRole, false),
        };

        var main = FindMain() ?? throw new InvalidOperationException("Presentation card is not attached to Main.");
        var minimumWidth = main.ResolvePresentationMetric(
            ModThemeMetricKeys.Card.MinimumWidth(role),
            1.0f,
            2048.0f);
        var artHeight = main.ResolvePresentationMetric(
            ModThemeMetricKeys.Card.ArtHeight(role),
            0.0f,
            2048.0f);
        var contentGap = main.ResolvePresentationMetric(ModThemeMetricKeys.Card.ContentGap, 0.0f, 256.0f);
        _column.AddThemeConstantOverride("separation", Mathf.RoundToInt(contentGap));

        ApplyFootprint(minimumWidth, artHeight, showSubtitle: false, role);
        if (disablePointerInteraction)
        {
            ButtonMask = (MouseButtonMask)0;
            FocusMode = FocusModeEnum.None;
        }
    }

    private void ApplyFootprint(float minimumWidth, float artHeight, bool showSubtitle, string role)
    {
        CustomMinimumSize = new Vector2(minimumWidth, 0);
        _art.CustomMinimumSize = new Vector2(0, artHeight);
        _subtitle.Visible = showSubtitle && !string.IsNullOrWhiteSpace(_subtitle.Text);
        SetMeta("presentation_footprint", role);
    }

    private Main? FindMain()
    {
        Node? node = GetParent();
        while (node is not null)
        {
            if (node is Main main)
                return main;
            node = node.GetParent();
        }

        return null;
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
