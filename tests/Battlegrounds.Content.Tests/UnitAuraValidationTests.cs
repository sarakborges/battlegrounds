using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class UnitAuraValidationTests
{
    [Fact]
    public void WarbandsContinuousAuraContentValidatesAndLoadsBehaviorAura()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "warbands");
        var report = new ModValidator().Validate(path);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues.Select(issue => $"{issue.Code} {issue.File} {issue.Path}: {issue.Message}")));

        var package = new ModLoader().Load(path);
        var moonfang = package.Units.GetRequired(new UnitId("moonfang-alpha"));
        var aura = Assert.Single(moonfang.Auras);
        Assert.Contains(aura.GrantedBehaviors, behavior => behavior.Handler == NativeBehaviorKeys.ExtraAttack);
    }
}
