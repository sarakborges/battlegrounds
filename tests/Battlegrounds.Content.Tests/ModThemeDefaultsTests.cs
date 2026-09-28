using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class ModThemeDefaultsTests
{
    [Fact]
    public void LoadEngineDefault_ProvidesCompleteAssetFreePresentationBaseline()
    {
        var theme = new ModThemeLoader().LoadEngineDefault();

        Assert.Equal(1, theme.Version);
        Assert.False(theme.IsEmpty);
        Assert.Equal(8, theme.Metrics[ModThemeMetricKeys.Layout.OuterMargin]);
        Assert.Equal(64, theme.Metrics[ModThemeMetricKeys.Launcher.MarginHorizontal]);
        Assert.Equal(92, theme.Metrics[ModThemeMetricKeys.Card.MinimumWidth(ModThemeMetricKeys.Card.DefaultRole)]);
        Assert.Equal(3, theme.Metrics[ModThemeMetricKeys.Drag.DropTargetShadowScale]);
        Assert.Equal(0.9, theme.Metrics[ModThemeMetricKeys.Motion.CombatPlaybackStepSeconds]);
        Assert.Equal(1.05, theme.Metrics[ModThemeMetricKeys.Motion.Cue.PulseScale]);
        Assert.Equal(0.22, theme.Metrics[ModThemeMetricKeys.Motion.Cue.ShakeDurationSeconds]);
        Assert.Equal(0.25, theme.Metrics[ModThemeMetricKeys.Motion.Cue.FadeOpacity]);
        Assert.Equal(0.28, theme.Metrics[ModThemeMetricKeys.Motion.Cue.PopDurationSeconds]);
        Assert.Equal(0.12, theme.Components[ModThemeInteractionRoles.DragPreview].Opacity);
        Assert.Equal("#00000000", theme.Components[ModThemeInteractionRoles.DropTargetValid].BackgroundColor);
        Assert.Empty(theme.Fonts);
        Assert.DoesNotContain(theme.Components.Values, style => !string.IsNullOrWhiteSpace(style.BackgroundAsset));
        Assert.DoesNotContain(theme.Screens.Values, screen => !string.IsNullOrWhiteSpace(screen.BackgroundAsset));
    }

    [Fact]
    public void Layer_PartialModStylePreservesUnspecifiedBasePropertiesAndStates()
    {
        var path = Path.Combine(Path.GetTempPath(), "battlegrounds-theme-layer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(path, "presentation"));
        File.WriteAllText(
            Path.Combine(path, "presentation", "theme.json"),
            """
            {
              "version": 1,
              "colors": {
                "accent": "#123456"
              },
              "components": {
                "button.primary": {
                  "backgroundColor": "accent",
                  "states": {
                    "hover": {
                      "opacity": 0.8
                    }
                  }
                }
              }
            }
            """);

        try
        {
            var loader = new ModThemeLoader();
            var baseTheme = loader.LoadEngineDefault();
            var modTheme = loader.Load(path);

            var layered = ModThemeCatalog.Layer(baseTheme, modTheme);
            var primary = layered.Components[ModThemeComponentRoles.ButtonPrimary];

            Assert.Equal("accent", primary.BackgroundColor);
            Assert.Equal("primaryText", primary.TextColor);
            Assert.Equal("primaryHover", primary.States["hover"].BackgroundColor);
            Assert.Equal(0.8, primary.States["hover"].Opacity);
            Assert.Equal("primaryPressed", primary.States["pressed"].BackgroundColor);
            Assert.Equal(8, layered.Metrics[ModThemeMetricKeys.Layout.OuterMargin]);
            Assert.Equal(0.9, layered.Metrics[ModThemeMetricKeys.Motion.CombatPlaybackStepSeconds]);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
