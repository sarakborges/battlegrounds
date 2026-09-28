using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class WarbandsPlayableContentTests
{
    [Fact]
    public void Warbands_MaintainsPlayableSliceBreadthAndArchetypeDepth()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "warbands");

        var mod = new ModLoader().Load(path);

        Assert.True(mod.Units.All.Count >= 20, "Warbands should keep at least 20 authored Units.");
        Assert.True(mod.Leaders.All.Count >= 4, "Warbands should keep multiple Leader choices.");
        Assert.True(mod.Powers.All.Count >= 4, "Warbands should keep multiple authored Powers.");
        Assert.True(mod.Combines.All.Count >= 3, "Warbands should keep several authored combine recipes.");
        Assert.True(mod.UnitTypes.All.Count >= 3, "Warbands should keep at least three composition archetypes.");

        var tiers = mod.Units.All
            .Select(unit => unit.Tier)
            .Distinct()
            .OrderBy(tier => tier)
            .ToArray();

        Assert.True(tiers.Length >= 4, "Warbands should have a meaningful multi-tier progression curve.");
        Assert.Equal(1, tiers[0]);
        Assert.True(
            tiers.SequenceEqual(Enumerable.Range(1, tiers[^1])),
            $"Warbands Unit tiers should form a gapless curve from 1 through {tiers[^1]}.");

        foreach (var type in mod.UnitTypes.All)
        {
            var typedUnits = mod.Units.All
                .Where(unit => unit.Types.Any(candidate => candidate.Id == type.Id))
                .ToArray();
            var typedTiers = typedUnits
                .Select(unit => unit.Tier)
                .Distinct()
                .Count();

            Assert.True(
                typedUnits.Length >= 6,
                $"Archetype '{type.Id}' should have at least six Units so it can support a real composition.");
            Assert.True(
                typedTiers >= 3,
                $"Archetype '{type.Id}' should appear across at least three tiers so it has an early/mid/late progression path.");
        }
    }
}
