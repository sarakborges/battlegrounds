using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private ModThemeCatalog? _engineTheme;
    private ModThemeCatalog? _modTheme;

    private void InitializeTheme()
    {
        var modDirectory = ProjectSettings.GlobalizePath(ModPath);
        var loader = new ModThemeLoader();
        _engineTheme ??= loader.LoadEngineDefault();
        var modTheme = loader.Load(modDirectory);
        _modTheme = ModThemeCatalog.Layer(_engineTheme, modTheme);
    }
}
