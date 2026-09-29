using Godot;

namespace Battlegrounds.Game;

internal sealed partial class PresentationCardButton
{
    private TextureRect? _semanticTokenFrame;

    internal void ConfigureTokenFrame(Texture2D? texture)
    {
        var margin = GetChildren().OfType<MarginContainer>().FirstOrDefault();
        if (margin is null)
            return;

        const int inset = 7;
        margin.AddThemeConstantOverride("margin_left", inset);
        margin.AddThemeConstantOverride("margin_top", inset);
        margin.AddThemeConstantOverride("margin_right", inset);
        margin.AddThemeConstantOverride("margin_bottom", inset);
        margin.ZIndex = 1;

        // The token asset is a transparent foreground frame. Keep the actual
        // portrait below it instead of relying on the Button stylebox, which
        // otherwise disappears behind the art or gets drawn twice.
        var empty = new StyleBoxEmpty();
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, empty);

        _semanticTokenFrame ??= CreateSemanticTokenFrame();
        _semanticTokenFrame.Texture = texture;
        _semanticTokenFrame.Visible = texture is not null;
        _semanticTokenFrame.ZIndex = 3;
        _semanticTokenFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    private TextureRect CreateSemanticTokenFrame()
    {
        var frame = new TextureRect
        {
            Name = "SemanticTokenFrame",
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ZIndex = 3,
            Visible = false,
        };
        AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return frame;
    }
}
