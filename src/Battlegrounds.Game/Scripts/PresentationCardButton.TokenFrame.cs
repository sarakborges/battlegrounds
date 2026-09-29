using Godot;

namespace Battlegrounds.Game;

internal sealed partial class PresentationCardButton
{
    private TextureRect? _semanticTokenFrame;

    internal void ConfigureTokenFrame(Texture2D? texture)
    {
        // BoardCardButton already renders the mod-driven token frame as the button
        // background. Drawing the same SVG again above the portrait hid the art
        // whenever the frame asset had an opaque center. Keep the content slightly
        // inset instead, so the background frame remains visible around real art.
        if (_semanticTokenFrame is not null)
            _semanticTokenFrame.Visible = false;

        var margin = GetChildren().OfType<MarginContainer>().FirstOrDefault();
        if (margin is null)
            return;

        const int inset = 7;
        margin.AddThemeConstantOverride("margin_left", inset);
        margin.AddThemeConstantOverride("margin_top", inset);
        margin.AddThemeConstantOverride("margin_right", inset);
        margin.AddThemeConstantOverride("margin_bottom", inset);
        margin.ZIndex = 1;
    }

    private TextureRect CreateSemanticTokenFrame()
    {
        var frame = new TextureRect
        {
            Name = "SemanticTokenFrame",
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ZIndex = 0,
            Visible = false,
        };
        AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return frame;
    }
}
