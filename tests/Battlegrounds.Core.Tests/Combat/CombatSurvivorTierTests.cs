using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Combat;

public sealed class CombatSurvivorTierTests
{
    [Fact]
    public void Resolve_PreservesAuthoredTierForSummonedSurvivor()
    {
        var token = new UnitDefinition(new UnitId("token"), "Token", tier: 4, baseAttack: 5, baseHealth: 5);
        var summoner = new UnitDefinition(
            new UnitId("summoner"),
            "Summoner",
            tier: 2,
            baseAttack: 1,
            baseHealth: 1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [new SummonUnitEffectDefinition(token.Id)]),
            ]);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", tier: 1, baseAttack: 2, baseHealth: 2);
        var catalog = new UnitCatalog([summoner, token, enemy]);
        var input = new CombatInput(
            Participant(0, Snapshot(1, summoner)),
            Participant(1, Snapshot(2, enemy)));

        var result = new CombatEngine(
            fieldCapacity: 7,
            catalog,
            new BehaviorCatalog(Array.Empty<BehaviorDefinition>()))
            .Resolve(input, new CombatRules(StartingSidePolicy.Random), new MinimumRandomSource());

        var survivor = Assert.Single(result.LeftSurvivors);
        Assert.True(survivor.InstanceId.Value > 2);
        Assert.Equal(4, survivor.Tier);
    }

    private static CombatParticipant Participant(int playerId, params CombatUnitSnapshot[] units) =>
        new(new PlayerId(playerId), units);

    private static CombatUnitSnapshot Snapshot(long instanceId, UnitDefinition definition) =>
        new(
            new UnitInstanceId(instanceId),
            definition.Id,
            definition.Tier,
            definition.BaseAttack,
            definition.BaseHealth,
            definition.Behaviors.Select(behavior => new CombatBehaviorSnapshot(behavior.Id, behavior.Handler)),
            definition);

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
