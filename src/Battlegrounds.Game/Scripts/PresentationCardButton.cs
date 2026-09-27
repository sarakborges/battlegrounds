using Godot;

namespace Battlegrounds.Game;

internal sealed partial class PresentationCardButton : Button
{
    private readonly TextureRect _art;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _stats;
    private readonly Label _description;

    public PresentationCardButton()
    {
        Text = string.Empty;
        ThemeTypeVariation = "CardButton";
        CustomMinimumSize = new Vector2(92, 108);
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
            CustomMinimumSize = new Vector2(0, 68),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
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

    public void SetSelected(bool selected)
    {
        ToggleMode = true;
        ButtonPressed = selected;
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
