namespace Battlegrounds.Content;

internal sealed partial class ModThemeValidator
{
    static ModThemeValidator()
    {
        StyleKeys.UnionWith(["iconAsset", "width", "height", "iconWidth", "iconHeight"]);
        StateStyleKeys.UnionWith(["iconAsset", "width", "height", "iconWidth", "iconHeight"]);
    }
}
