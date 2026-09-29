using Godot;

namespace Battlegrounds.Game;

internal static class FramedCosmeticPortrait
{
    internal const string DefaultFrameNodeName = "CosmeticFrame";

    public static TextureRect ApplyFrame(
        Control host,
        Texture2D frameTexture,
        string frameNodeName = DefaultFrameNodeName,
        int zIndex = 10)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(frameTexture);

        host.ClipContents = true;
        if (host is PanelContainer panel)
            panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        var frame = host.GetNodeOrNull<TextureRect>(frameNodeName);
        if (frame is null)
        {
            frame = new TextureRect
            {
                Name = frameNodeName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ZIndex = zIndex,
            };
            host.AddChild(frame);
            frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        frame.Texture = frameTexture;
        frame.Visible = true;
        frame.ZIndex = zIndex;
        frame.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return frame;
    }

    public static void ConfigureArt(
        TextureRect art,
        TextureRect.StretchModeEnum stretchMode = TextureRect.StretchModeEnum.KeepAspectCovered)
    {
        ArgumentNullException.ThrowIfNull(art);

        art.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        art.StretchMode = stretchMode;
        art.MouseFilter = Control.MouseFilterEnum.Ignore;
        art.ZIndex = 0;
        art.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public static void ConfigureSquare(Control host, float size)
    {
        ArgumentNullException.ThrowIfNull(host);
        var clamped = Mathf.Max(1.0f, size);
        host.CustomMinimumSize = new Vector2(clamped, clamped);
        host.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        host.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }
}
