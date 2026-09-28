using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

internal sealed record PresentationCardInspectData(
    string Title,
    string Subtitle,
    string Stats,
    string? Description,
    string? Details,
    Texture2D? Texture,
    int? Tier = null,
    int? Attack = null,
    int? Health = null);

internal sealed partial class PresentationCardButton : Button
{
    private const float DragThreshold = 5.0f;

    private readonly TextureRect _art;
    private readonly VBoxContainer _column;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _stats;
    private readonly Label _description;
    private readonly PanelContainer _tierBadge;
    private readonly Label _tierValue;
    private readonly PanelContainer _attackBadge;
    private readonly Label _attackValue;
    private readonly PanelContainer _healthBadge;
    private readonly Label _healthValue;
    private string? _dragPayload;
    private bool _dragEnabled;
    private bool _dragActive;
    private bool _dragPointerDown;
    private Vector2 _dragPressPosition;
    private Action? _dragStarted;
    private Action? _dragEnded;
    private bool _tokenMode;
    private int? _tokenTier;
    private int _tokenAttack;
    private int _tokenHealth;
    private ModPresentationEntityKind? _entityKind;
    private string? _entityId;
    private PresentationCardInspectData? _inspectData;

    public PresentationCardButton()
    {
        Text = string.Empty;
        ThemeTypeVariation = "CardButton";
        ClipContents = true;
        MouseEntered += HandleMouseEntered;
        MouseExited += HandleMouseExited;

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

        (_tierBadge, _tierValue) = CreateStatBadge("TierBadge", "TierValueLabel");
        (_attackBadge, _attackValue) = CreateStatBadge("AttackBadge", "AttackValueLabel");
        (_healthBadge, _healthValue) = CreateStatBadge("HealthBadge", "HealthValueLabel");
        AddChild(_tierBadge);
        AddChild(_attackBadge);
        AddChild(_healthBadge);
    }

    public override void _Ready()
    {
        ApplyFootprintForParent();
        if (_entityKind is ModPresentationEntityKind entityKind && !string.IsNullOrWhiteSpace(_entityId))
            FindMain()?.ConfigureCompactTokenForParent(this, entityKind, _entityId);
        ApplyTokenVisualMode();
    }

    public override void _ExitTree()
    {
        FindMain()?.HideCardInspect(this);
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
        _inspectData = new PresentationCardInspectData(
            title,
            subtitle,
            stats,
            description,
            null,
            texture);
    }

    public void ConfigureIdentity(ModPresentationEntityKind entityKind, string entityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        _entityKind = entityKind;
        _entityId = entityId;
    }

    public void ConfigureInspectDetails(string? details)
    {
        if (_inspectData is not null)
            _inspectData = _inspectData with { Details = details };
    }

    public void ConfigureToken(int? tier, int attack, int health, string? inspectDetails = null)
    {
        _tokenMode = true;
        _tokenTier = tier;
        _tokenAttack = attack;
        _tokenHealth = health;
        _tierValue.Text = tier?.ToString() ?? string.Empty;
        _attackValue.Text = attack.ToString();
        _healthValue.Text = health.ToString();
        TooltipText = string.Empty;

        if (_inspectData is not null)
        {
            _inspectData = _inspectData with
            {
                Details = inspectDetails,
                Tier = tier,
                Attack = attack,
                Health = health,
            };
        }

        if (IsInsideTree())
            ApplyTokenVisualMode();
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
        main.HideCardInspect(this);
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
        preview.ConfigureInspectDetails(_inspectData?.Details);
        if (_tokenMode)
            preview.ConfigureToken(_tokenTier, _tokenAttack, _tokenHealth, _inspectData?.Details);
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

    private void ApplyTokenVisualMode()
    {
        if (!_tokenMode)
            return;

        ThemeTypeVariation = "BoardCardButton";
        _title.Visible = false;
        _subtitle.Visible = false;
        _stats.Visible = false;
        _description.Visible = false;
        _tierBadge.Visible = _tokenTier.HasValue;
        _attackBadge.Visible = true;
        _healthBadge.Visible = true;
        _art.SizeFlagsVertical = SizeFlags.ExpandFill;
        _column.AddThemeConstantOverride("separation", 0);
        ApplyTokenBadgeLayout();
    }

    private void ApplyTokenBadgeLayout()
    {
        var main = FindMain();
        if (main is null)
            return;

        var badgeSize = main.ResolvePresentationMetric(ModThemeMetricKeys.Card.TokenBadgeSize, 16.0f, 128.0f);
        var inset = main.ResolvePresentationMetric(ModThemeMetricKeys.Card.TokenBadgeInset, 0.0f, 64.0f);

        SetBadgeRect(_tierBadge, 0.0f, 0.0f, inset, inset, badgeSize);
        SetBadgeRect(_attackBadge, 0.0f, 1.0f, inset, -inset - badgeSize, badgeSize);
        SetBadgeRect(_healthBadge, 1.0f, 1.0f, -inset - badgeSize, -inset - badgeSize, badgeSize);
    }

    private static void SetBadgeRect(Control badge, float anchorX, float anchorY, float left, float top, float size)
    {
        badge.AnchorLeft = anchorX;
        badge.AnchorTop = anchorY;
        badge.AnchorRight = anchorX;
        badge.AnchorBottom = anchorY;
        badge.OffsetLeft = left;
        badge.OffsetTop = top;
        badge.OffsetRight = left + size;
        badge.OffsetBottom = top + size;
        badge.CustomMinimumSize = new Vector2(size, size);
    }

    private void HandleMouseEntered()
    {
        if (_dragActive || _inspectData is null)
            return;

        FindMain()?.ShowCardInspect(this, _inspectData);
    }

    private void HandleMouseExited()
    {
        FindMain()?.HideCardInspect(this);
    }

    private static (PanelContainer Panel, Label Label) CreateStatBadge(string panelVariation, string labelVariation)
    {
        var panel = IgnoreMouse(new PanelContainer
        {
            ThemeTypeVariation = panelVariation,
            Visible = false,
            ZIndex = 4,
        });
        var label = CreateLabel(labelVariation);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(label);
        return (panel, label);
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
