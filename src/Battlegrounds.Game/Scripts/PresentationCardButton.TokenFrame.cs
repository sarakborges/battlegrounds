using Godot;

namespace Battlegrounds.Game;

internal sealed partial class PresentationCardButton
{
    private TextureRect? _semanticTokenFrame;

    internal void ConfigureTokenFrame(Texture2D? texture)
    {
        if (texture is null)
        {
            if (_semanticTokenFrame is not null)
                _semanticTokenFrame.Visible = false;
            return;
        }

        _semanticTokenFrame ??= CreateSemanticTokenFrame();
        _semanticTokenFrame.Texture = texture;
        _semanticTokenFrame.Visible = true;
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
        };
        AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return frame;
    }
}
