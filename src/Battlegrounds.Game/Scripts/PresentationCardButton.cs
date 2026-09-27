using Godot;

namespace Battlegrounds.Game;

internal sealed partial class PresentationCardButton : Button
{
    private readonly TextureRect _art;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _stats;
    private readonly Label _description;
    private string? _dragPayload;
    private bool _dragEnabled;

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

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!_dragEnabled || string.IsNullOrWhiteSpace(_dragPayload))
            return default;

        SetDragPreview(CreateDragPreview());
        return _dragPayload;
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

    public void ConfigureOfferDrag(int slot, bool enabled) =>
        ConfigureDrag(PreparationDragPayload.Offer(slot), enabled);

    public void ConfigureFieldDrag(int index, bool enabled) =>
        ConfigureDrag(PreparationDragPayload.Field(index), enabled);

    public void SetSelected(bool selected)
    {
        ToggleMode = true;
        ButtonPressed = selected;
    }

    private void ConfigureDrag(string payload, bool enabled)
    {
        _dragPayload = payload;
        _dragEnabled = enabled;
        MouseDefaultCursorShape = enabled ? CursorShape.Drag : CursorShape.Arrow;
    }

    private Control CreateDragPreview()
    {
        var preview = new PanelContainer
        {
            CustomMinimumSize = new Vector2(Mathf.Max(92.0f, Size.X * 0.72f), Mathf.Max(72.0f, Size.Y * 0.58f)),
            ThemeTypeVariation = "DragPreview",
            MouseFilter = MouseFilterEnum.Ignore,
        };

        var column = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 2);
        preview.AddChild(column);

        if (_art.Texture is not null)
        {
            column.AddChild(new TextureRect
            {
                Texture = _art.Texture,
                CustomMinimumSize = new Vector2(0, 48),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }

        column.AddChild(new Label
        {
            Text = _title.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = "HeadingLabel",
            MouseFilter = MouseFilterEnum.Ignore,
        });

        return preview;
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
