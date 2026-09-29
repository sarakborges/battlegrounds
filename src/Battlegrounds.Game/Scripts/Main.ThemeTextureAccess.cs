using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    internal Texture2D? ResolveComponentBackgroundTexture(string role)
    {
        if (_modTheme is null || _themeBuilder is null ||
            !_modTheme.Components.TryGetValue(role, out var style) ||
            string.IsNullOrWhiteSpace(style.BackgroundAsset))
        {
            return null;
        }

        return _themeBuilder.LoadImage(style.BackgroundAsset);
    }
}
