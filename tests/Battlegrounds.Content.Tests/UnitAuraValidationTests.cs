using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
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

        var siegeColossus = package.Units.GetRequired(new UnitId("siege-colossus"));
        var enemyAura = Assert.Single(siegeColossus.Auras);
        Assert.Equal(EffectTargetScope.Enemy, enemyAura.Target.Scope);
        Assert.Equal(-1, enemyAura.AttackDelta);
    }
}
