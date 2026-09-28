using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class StaticPresentationStringTests
{
    [Theory]
    [InlineData("example", "RELEASE", "DROP HERE")]
    [InlineData("warbands", "SHOPKEEPER", "SELL")]
    public void Resolve_StaticGameplayCopyComesFromSelectedMod(
        string modDirectoryName,
        string releaseTargetTitle,
        string releaseTargetHint)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", modDirectoryName);

        var report = new ModValidator().Validate(path);
        var text = new ModLoader().Load(path).Presentation.Resolve("en");

        Assert.True(report.IsValid);
        Assert.Equal(releaseTargetTitle, text.Get("ui.releaseTargetTitle"));
        Assert.Equal(releaseTargetHint, text.Get("ui.releaseTargetHint"));
        Assert.Equal("Setup", text.Get("ui.phaseSetup"));
        Assert.Equal("Finished", text.Get("ui.phaseFinished"));
    }
}
