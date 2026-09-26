using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class ModLoaderTests
{
    [Fact]
    public void Load_ReadsRulesTerminologyUnitsAndPoolFromModDirectory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var mod = new ModLoader().Load(path);

        Assert.Equal("example", mod.Id);
        Assert.Equal("Energy", mod.Terminology["resource"]);
        Assert.Equal(2, mod.MatchRules.MinimumPlayers);
        Assert.Equal(8, mod.MatchRules.MaximumPlayers);
        Assert.Equal(3, mod.PreparationRules.StartingResource);
        Assert.Equal(3, mod.PreparationRules.AcquireCost);
        Assert.Equal(7, mod.PreparationRules.FieldCapacity);
        Assert.Equal(StartingSidePolicy.LargerFieldThenRandom, mod.CombatRules.StartingSidePolicy);
        Assert.Equal("Scout", mod.Units.GetRequired(new UnitId("scout")).Name);

        var pool = mod.CreateUnitPool();
        Assert.Equal(15, pool.GetAvailableCopies(new UnitId("scout")));
    }
}
