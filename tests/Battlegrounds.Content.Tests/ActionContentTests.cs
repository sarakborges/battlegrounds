using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class ActionContentTests
{
    [Fact]
    public void Load_ExposesIndependentActionCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var mod = new ModLoader().Load(path);

        var action = mod.Actions.GetRequired(new ActionId("training"));
        Assert.Equal("Training", action.Name);
        Assert.Equal(1, action.Tier);
        Assert.Equal(2, action.Cost);
        Assert.Single(action.Effects);
    }
}
