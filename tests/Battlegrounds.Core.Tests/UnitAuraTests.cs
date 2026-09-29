using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests;

public sealed class UnitAuraTests
{
    [Fact]
    public void PreparationAuraAppearsOnDeployAndDisappearsWithSource()
    {
        var aura = new UnitAuraDefinition(new EffectTargetSelector(EffectTargetScope.Friendly, excludeSource: true), 2, 3);
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 1, 2, auras: [aura]);
        var ally = new UnitDefinition(new UnitId("ally"), "Ally", 1, 1, 1);
        var pool = new ScriptedPool([source, ally]);
        var rules = new PreparationRules(10, 0, 10, 0, 1, 1, 7, 10, 2, [2, 2], [1]);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 8));
        var engine = new PreparationEngine(rules, pool, new SeededRandomSource(3));
        engine.BeginPreparation(match);
        var player = match.Players[0];

        AcquireById(engine, match, player.Id, source.Id);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        AcquireById(engine, match, player.Id, ally.Id);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var buffed = player.Field.Single(unit => unit.Definition.Id == ally.Id);
        Assert.Equal(3, buffed.Attack);
        Assert.Equal(4, buffed.Health);

        var sourceIndex = Enumerable.Range(0, player.Field.Count).Single(index => player.Field[index].Definition.Id == source.Id);
        Assert.True(engine.Execute(match, new ReleaseUnitCommand(player.Id, sourceIndex)).Succeeded);
        var unbuffed = Assert.Single(player.Field);
        Assert.Equal(1, unbuffed.Attack);
        Assert.Equal(1, unbuffed.Health);
    }

    [Fact]
    public void CombatAuraProvidesAttackBeforeFirstStrike()
    {
        var aura = new UnitAuraDefinition(new EffectTargetSelector(EffectTargetScope.Friendly, excludeSource: true), 5, 0);
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 0, 5, auras: [aura]);
        var ally = new UnitDefinition(new UnitId("ally"), "Ally", 1, 0, 5);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 1, 4);
        var input = new CombatInput(
  new CombatParticipant(new PlayerId(0), [Snap(1, source), Snap(2, ally)]),
  new CombatParticipant(new PlayerId(1), [Snap(3, enemy)]));

        var result = new CombatEngine().Resolve(input, new CombatRules(StartingSidePolicy.LargerFieldThenRandom), new SeededRandomSource(1));
        Assert.Equal(new PlayerId(0), result.WinnerPlayerId);
    }

    [Fact]
    public void CombatAuraDisappearsWhenSourceDies()
    {
        var aura = new UnitAuraDefinition(new EffectTargetSelector(EffectTargetScope.Friendly, excludeSource: true), 30, 0);
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 1, 1, auras: [aura]);
        var ally = new UnitDefinition(new UnitId("ally"), "Ally", 1, 0, 5);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 1, 20);
        var input = new CombatInput(
  new CombatParticipant(new PlayerId(0), [Snap(1, source), Snap(2, ally)]),
  new CombatParticipant(new PlayerId(1), [Snap(3, enemy)]));

        var result = new CombatEngine().Resolve(input, new CombatRules(StartingSidePolicy.LargerFieldThenRandom), new SeededRandomSource(1));
        Assert.Equal(new PlayerId(1), result.WinnerPlayerId);
    }


    [Fact]
    public void EnemyAttackAuraAppliesBeforeCombatAndClampsAttackAtZero()
    {
        var aura = new UnitAuraDefinition(
            new EffectTargetSelector(EffectTargetScope.Enemy),
            attackDelta: -3);
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 0, 10, auras: [aura]);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 2, 10);
        var input = new CombatInput(
            new CombatParticipant(new PlayerId(0), [Snap(1, source)]),
            new CombatParticipant(new PlayerId(1), [Snap(2, enemy)]));

        var result = new CombatEngine().Resolve(input, new CombatRules(StartingSidePolicy.Random), new SeededRandomSource(1));

        Assert.True(result.IsDraw);
        Assert.Empty(result.Attacks);
    }

    [Fact]
    public void PreparationBehaviorAuraIsDerivedAndNotBakedIntoCombatSnapshot()
    {
        var doubleStrike = new BehaviorDefinition(new BehaviorId("double-strike"), "Double Strike", NativeBehaviorKeys.ExtraAttack);
        var aura = new UnitAuraDefinition(
            new EffectTargetSelector(EffectTargetScope.Friendly, excludeSource: true),
            grantedBehaviors: [doubleStrike]);
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 1, 2, auras: [aura]);
        var ally = new UnitDefinition(new UnitId("ally"), "Ally", 1, 1, 1);
        var pool = new ScriptedPool([source, ally]);
        var rules = new PreparationRules(10, 0, 10, 0, 1, 1, 7, 10, 2, [2, 2], [1]);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 8));
        var engine = new PreparationEngine(rules, pool, new SeededRandomSource(3));
        engine.BeginPreparation(match);
        var player = match.Players[0];

        AcquireById(engine, match, player.Id, source.Id);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        AcquireById(engine, match, player.Id, ally.Id);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var buffed = player.Field.Single(unit => unit.Definition.Id == ally.Id);
        Assert.Contains(buffed.Behaviors, behavior => behavior.Handler == NativeBehaviorKeys.ExtraAttack);

        var combatInput = CombatInput.FromFields(player.Id, player.Field, new PlayerId(1), []);
        var allySnapshot = combatInput.Left.Units.Single(unit => unit.UnitId == ally.Id);
        Assert.DoesNotContain(allySnapshot.Behaviors, behavior => behavior.Handler == NativeBehaviorKeys.ExtraAttack);

        var sourceIndex = Enumerable.Range(0, player.Field.Count).Single(index => player.Field[index].Definition.Id == source.Id);
        Assert.True(engine.Execute(match, new ReleaseUnitCommand(player.Id, sourceIndex)).Succeeded);
        Assert.DoesNotContain(Assert.Single(player.Field).Behaviors, behavior => behavior.Handler == NativeBehaviorKeys.ExtraAttack);
    }

    [Fact]
    public void CombatBehaviorAuraGrantsExtraAttack()
    {
        var doubleStrike = new BehaviorDefinition(new BehaviorId("double-strike"), "Double Strike", NativeBehaviorKeys.ExtraAttack);
        var aura = new UnitAuraDefinition(
            new EffectTargetSelector(EffectTargetScope.Friendly, excludeSource: true),
            grantedBehaviors: [doubleStrike]);
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 0, 20, auras: [aura]);
        var ally = new UnitDefinition(new UnitId("ally"), "Ally", 1, 2, 20);
        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 1, 30);
        var input = new CombatInput(
            new CombatParticipant(new PlayerId(0), [Snap(1, source), Snap(2, ally)]),
            new CombatParticipant(new PlayerId(1), [Snap(3, enemy)]));

        var result = new CombatEngine().Resolve(input, new CombatRules(StartingSidePolicy.LargerFieldThenRandom), new SeededRandomSource(1));

        Assert.True(result.Attacks.Count >= 2);
        Assert.All(result.Attacks.Take(2), attack => Assert.Equal(new UnitInstanceId(2), attack.AttackerInstanceId));
    }

    private static CombatUnitSnapshot Snap(long id, UnitDefinition definition) =>
        new(new UnitInstanceId(id), definition.Id, definition.Tier, definition.BaseAttack, definition.BaseHealth, definition: definition);

    private static void AcquireById(PreparationEngine engine, MatchState match, PlayerId playerId, UnitId id)
    {
        var player = match.Players.Single(candidate => candidate.Id == playerId);
        var slot = player.Offer.Select((definition, index) => (definition, index)).First(pair => pair.definition.Id == id).index;
        Assert.True(engine.Execute(match, new AcquireUnitCommand(playerId, slot)).Succeeded);
    }

    private sealed class ScriptedPool(IReadOnlyList<UnitDefinition> units) : IUnitPool
    {
        public IReadOnlyList<UnitDefinition> DrawOffer(int maximumTier, int count, IRandomSource randomSource) => units.Take(count).ToArray();
        public IReadOnlyList<UnitDefinition> ExchangeOffer(IReadOnlyCollection<UnitDefinition> returnedUnits, int maximumTier, int count, IRandomSource randomSource) => units.Take(count).ToArray();
        public void ReturnUnit(UnitDefinition definition) { }
        public int GetAvailableCopies(UnitId unitId) => 0;
    }
}
