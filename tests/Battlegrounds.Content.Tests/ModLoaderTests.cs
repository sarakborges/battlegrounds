using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class ModLoaderTests
{
    [Fact]
    public void Load_ReadsRulesLeadersTaxonomyBehaviorsTriggersUnitsAndPoolFromModDirectory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var mod = new ModLoader().Load(path);

        Assert.Equal("example", mod.Id);
        Assert.Equal("Energy", mod.Terminology["resource"]);
        Assert.Equal("Leader", mod.Terminology["leader"]);
        Assert.Equal(2, mod.MatchRules.MinimumPlayers);
        Assert.Equal(8, mod.MatchRules.MaximumPlayers);
        Assert.Equal(30, mod.MatchRules.StartingHealth);
        Assert.Equal(3, mod.PreparationRules.StartingResource);
        Assert.Equal(StartingSidePolicy.LargerFieldThenRandom, mod.CombatRules.StartingSidePolicy);
        Assert.Equal(PostCombatDamagePolicy.WinnerTierPlusSurvivorTiers, mod.CombatRules.PostCombatDamagePolicy);

        var steady = mod.Leaders.GetRequired(new LeaderId("steady"));
        Assert.Equal("Steady Leader", steady.Name);
        Assert.Equal(0, steady.HealthModifier);
        Assert.Equal(2, steady.StartingArmor);

        var vital = mod.Leaders.GetRequired(new LeaderId("vital"));
        Assert.Equal(5, vital.HealthModifier);
        Assert.Equal(0, vital.StartingArmor);

        var protector = mod.Behaviors.GetRequired(new BehaviorId("protector"));
        Assert.Equal(NativeBehaviorKeys.TargetPriority, protector.Handler);
        Assert.Equal("Organic", mod.UnitTypes.GetRequired(new UnitTypeId("organic")).Name);
        Assert.Equal("Starter", mod.Tags.GetRequired(new TagId("starter")).Name);

        var guard = mod.Units.GetRequired(new UnitId("guard"));
        Assert.Equal(new BehaviorId("protector"), Assert.Single(guard.Behaviors).Id);
        Assert.Equal(new UnitTypeId("construct"), Assert.Single(guard.Types).Id);
        Assert.Equal(new TagId("starter"), Assert.Single(guard.Tags).Id);
        var trigger = Assert.Single(guard.Triggers);
        Assert.Equal(NativeTriggerKeys.OnCombatStart, trigger.Event);
        var effect = Assert.IsType<ModifyStatsEffectDefinition>(Assert.Single(trigger.Effects));
        Assert.Equal(1, effect.HealthDelta);

        var pool = mod.CreateUnitPool();
        Assert.Equal(15, pool.GetAvailableCopies(new UnitId("scout")));
    }
}
