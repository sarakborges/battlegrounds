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
        CustomMinimumSize = new Vector2(0, 104);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ClipContents = true;

        var margin = IgnoreMouse(new MarginContainer());
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var row = IgnoreMouse(new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        _art = IgnoreMouse(new TextureRect
        {
            CustomMinimumSize = new Vector2(84, 84),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        });
        row.AddChild(_art);

        var copy = IgnoreMouse(new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        copy.AddThemeConstantOverride("separation", 2);
        row.AddChild(copy);

        _title = CreateLabel(18);
        _subtitle = CreateLabel(13);
        _stats = CreateLabel(14);
        _description = CreateLabel(12, wrap: true);

        copy.AddChild(_title);
        copy.AddChild(_subtitle);
        copy.AddChild(_stats);
        copy.AddChild(_description);
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
        SetOptionalText(_description, description);
        TooltipText = string.IsNullOrWhiteSpace(description) ? title : $"{title}\n{description}";

        _art.Texture = texture;
        _art.Visible = texture is not null;
    }

    public void SetSelected(bool selected)
    {
        ToggleMode = true;
        ButtonPressed = selected;
    }

    private static Label CreateLabel(int fontSize, bool wrap = false)
    {
        var label = IgnoreMouse(new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        label.AddThemeFontSizeOverride("font_size", fontSize);
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
