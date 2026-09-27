using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Choices;
using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.AI;

public sealed record PreparationAiResult(
    int CommandsExecuted,
    bool PlayerReady,
    int Round,
    PreparationAiCommandCounts CommandCounts);

/// <summary>
/// A deterministic, theme-neutral baseline agent. It never mutates match state directly:
/// every game action goes through the same public commands used by presentation code.
/// </summary>
public sealed class PreparationAiAgent
{
    private readonly PreparationRules _rules;
    private readonly IRandomSource _randomSource;
    private readonly LeaderCatalog? _leaders;
    private readonly PowerCatalog? _powers;
    private readonly UnitCombineCatalog? _combines;

    public PreparationAiAgent(
        PreparationRules rules,
        IRandomSource randomSource,
        LeaderCatalog? leaders = null,
        PowerCatalog? powers = null,
        UnitCombineCatalog? combines = null)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _leaders = leaders;
        _powers = powers;
        _combines = combines;
    }

    public LeaderId SelectLeader(LeaderSelectionState selection, PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var offer = selection.GetOffer(playerId);
        if (offer.Count == 0) throw new InvalidOperationException("AI cannot select from an empty Leader offer.");

        LeaderId selected;
        if (_leaders is null)
        {
            selected = offer[_randomSource.NextInt(0, offer.Count)];
        }
        else
        {
            var candidates = offer
                .Select(id => _leaders.GetRequired(id))
                .ToArray();
            var index = ChooseBestIndex(
                candidates,
                leader => (long)leader.HealthModifier + leader.StartingArmor,
                leader => leader.Id.Value);
            selected = candidates[index].Id;
        }

        var result = selection.Select(playerId, selected);
        if (!result.Succeeded)
            throw new InvalidOperationException($"AI selected an invalid Leader: {result.FailureCode}.");
        return selected;
    }

    public PreparationAiResult PlayPreparation(
        MatchEngine engine,
        MatchState match,
        PlayerId playerId,
        int maximumCommands = 128,
        PreparationAiPersonality personality = PreparationAiPersonality.Tempo,
        PreparationAiStrategy? strategy = null,
        IPreparationAiCommandObserver? observer = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(match);
        if (maximumCommands <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCommands));
        if (match.Phase != MatchPhase.Preparation)
            throw new InvalidOperationException($"AI can only play Preparation while the match is in Preparation, not {match.Phase}.");
        if (!match.TryGetPlayer(playerId, out var player))
            throw new ArgumentException($"Unknown player '{playerId}'.", nameof(playerId));
        if (player.IsEliminated) throw new InvalidOperationException("An eliminated player cannot be controlled in Preparation.");

        strategy ??= PreparationAiStrategy.Balanced;
        var memory = new TurnMemory(match.Round);
        var executed = 0;
        var commandCounts = PreparationAiCommandCounts.Zero;

        while (match.Phase == MatchPhase.Preparation && !player.IsReadyForCombat)
        {
            if (executed >= maximumCommands)
                throw new InvalidOperationException($"AI exceeded its {maximumCommands}-command Preparation safety budget.");

            var command = ChooseNextCommand(match, player, memory, personality, strategy);
            observer?.BeforeCommand(player, command);
            var result = engine.ExecutePreparation(match, command);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"AI emitted illegal command '{command.GetType().Name}': {result.FailureCode}.");
            }

            observer?.AfterAcceptedCommand(player, command);
            executed++;
            commandCounts = commandCounts.Add(command);
            if (command is RefreshOfferCommand) memory.Refreshes++;
        }

        return new PreparationAiResult(executed, player.IsReadyForCombat, match.Round, commandCounts);
    }

    private IPreparationCommand ChooseNextCommand(
        MatchState match,
        PlayerState player,
        TurnMemory memory,
        PreparationAiPersonality personality,
        PreparationAiStrategy strategy)
    {
        if (memory.Round != match.Round)
            throw new InvalidOperationException("AI turn memory belongs to a different round.");

        if (player.PendingChoice is PendingUnitChoice unitChoice)
        {
            var optionIndex = ChooseBestIndex(
                unitChoice.Options,
                unit => ScoreUnitDefinition(unit, strategy),
                unit => unit.Id.Value);
            return new ResolveUnitChoiceCommand(player.Id, unitChoice.Id, optionIndex);
        }

        if (player.PendingChoice is PendingActionChoice actionChoice)
        {
            var optionIndex = ChooseBestIndex(
                actionChoice.Options,
                ScoreActionDefinition,
                action => action.Id.Value);
            return new ResolveActionChoiceCommand(player.Id, actionChoice.Id, optionIndex);
        }

        var combine = TryCreateCombineCommand(player);
        if (combine is not null) return combine;

        if (player.Field.Count < _rules.FieldCapacity && player.Reserve.Count > 0)
        {
            var reserveIndex = ChooseBestIndex(
                player.Reserve,
                unit => ScoreUnitInstance(unit, strategy),
                unit => unit.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return new DeployUnitCommand(player.Id, reserveIndex);
        }

        var release = TryCreateReplacementRelease(player, strategy);
        if (release is not null) return release;

        var action = TryCreatePlayActionCommand(match, player);
        if (action is not null) return action;

        var strategic = personality switch
        {
            PreparationAiPersonality.Tempo => TryCreateTempoCommand(match, player, memory, strategy),
            PreparationAiPersonality.Greedy => TryCreateGreedyCommand(match, player, memory, strategy),
            PreparationAiPersonality.Roller => TryCreateRollerCommand(match, player, memory, strategy),
            _ => throw new ArgumentOutOfRangeException(nameof(personality), personality, "Unsupported AI personality."),
        };
        if (strategic is not null) return strategic;

        if (!player.IsOfferFrozen && player.PlayableOfferCount > 0)
            return new FreezeOfferCommand(player.Id);

        return new EndPreparationCommand(player.Id);
    }

    private IPreparationCommand? TryCreateTempoCommand(
        MatchState match,
        PlayerState player,
        TurnMemory memory,
        PreparationAiStrategy strategy)
    {
        var power = TryCreatePowerCommand(match, player);
        if (power is not null) return power;

        var acquire = TryCreateAcquireCommand(player, strategy);
        if (acquire is not null) return acquire;

        var upgrade = TryCreateUpgradeCommand(player);
        if (upgrade is not null) return upgrade;

        return TryCreateRefreshCommand(player, memory, maximumRefreshes: 1);
    }

    private IPreparationCommand? TryCreateGreedyCommand(
        MatchState match,
        PlayerState player,
        TurnMemory memory,
        PreparationAiStrategy strategy)
    {
        var upgrade = TryCreateUpgradeCommand(player);
        if (upgrade is not null) return upgrade;

        var power = TryCreatePowerCommand(match, player);
        if (power is not null) return power;

        var acquire = TryCreateAcquireCommand(player, strategy);
        if (acquire is not null) return acquire;

        return TryCreateRefreshCommand(player, memory, maximumRefreshes: 1);
    }

    private IPreparationCommand? TryCreateRollerCommand(
        MatchState match,
        PlayerState player,
        TurnMemory memory,
        PreparationAiStrategy strategy)
    {
        var power = TryCreatePowerCommand(match, player);
        if (power is not null) return power;

        var refresh = TryCreateRefreshCommand(player, memory, maximumRefreshes: 3);
        if (refresh is not null) return refresh;

        var acquire = TryCreateAcquireCommand(player, strategy);
        if (acquire is not null) return acquire;

        return TryCreateUpgradeCommand(player);
    }

    private UpgradeTierCommand? TryCreateUpgradeCommand(PlayerState player)
    {
        if (player.UpgradeCost is not int upgradeCost ||
            player.Tier >= _rules.MaximumTier ||
            player.Resource < upgradeCost)
        {
            return null;
        }

        return new UpgradeTierCommand(player.Id);
    }

    private RefreshOfferCommand? TryCreateRefreshCommand(PlayerState player, TurnMemory memory, int maximumRefreshes)
    {
        if (memory.Refreshes >= maximumRefreshes || player.Resource < _rules.RefreshCost)
            return null;
        return new RefreshOfferCommand(player.Id);
    }

    private CombineUnitsCommand? TryCreateCombineCommand(PlayerState player)
    {
        if (_combines is null || _combines.All.Count == 0) return null;

        foreach (var recipe in _combines.All.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            var reserve = player.Reserve.Where(unit => unit.Definition.Id == recipe.SourceUnitId).ToArray();
            var field = player.Field.Where(unit => unit.Definition.Id == recipe.SourceUnitId).ToArray();
            if (reserve.Length + field.Length < recipe.RequiredCopies) continue;

            var selected = reserve
                .OrderBy(ScoreUnitInstance)
                .ThenBy(unit => unit.Id.Value)
                .Concat(field.OrderBy(ScoreUnitInstance).ThenBy(unit => unit.Id.Value))
                .Take(recipe.RequiredCopies)
                .ToArray();
            var reserveSources = selected.Count(unit => reserve.Any(candidate => candidate.Id == unit.Id));
            var reserveCountAfter = player.PlayableReserveCount - reserveSources + 1;
            if (reserveCountAfter > _rules.ReserveCapacity) continue;

            return new CombineUnitsCommand(player.Id, recipe.Id, selected.Select(unit => unit.Id).ToArray());
        }

        return null;
    }

    private ReleaseUnitCommand? TryCreateReplacementRelease(PlayerState player, PreparationAiStrategy strategy)
    {
        if (player.Field.Count < _rules.FieldCapacity || player.Reserve.Count == 0) return null;

        var reserveIndex = ChooseBestIndex(
            player.Reserve,
            unit => ScoreUnitInstance(unit, strategy),
            unit => unit.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var weakestFieldIndex = Enumerable.Range(0, player.Field.Count)
            .OrderBy(index => ScoreUnitInstance(player.Field[index], strategy))
            .ThenBy(index => player.Field[index].Id.Value)
            .First();

        if (ScoreUnitInstance(player.Reserve[reserveIndex], strategy) <=
            ScoreUnitInstance(player.Field[weakestFieldIndex], strategy))
        {
            return null;
        }

        return new ReleaseUnitCommand(player.Id, weakestFieldIndex);
    }

    private PlayActionCommand? TryCreatePlayActionCommand(MatchState match, PlayerState player)
    {
        if (player.ActionReserve.Count == 0) return null;

        var candidates = new List<ActionCandidate>();
        for (var index = 0; index < player.ActionReserve.Count; index++)
        {
            var action = player.ActionReserve[index];
            var target = SelectTarget(match, player.Id, action.Definition.Effects);
            if (target.RequiresTarget && target.Target is null) continue;
            candidates.Add(new ActionCandidate(index, action, target.Target));
        }
        if (candidates.Count == 0) return null;

        var selectedIndex = ChooseBestIndex(
            candidates,
            candidate => ScoreActionDefinition(candidate.Action.Definition),
            candidate => candidate.Action.Definition.Id.Value);
        var selected = candidates[selectedIndex];
        var playableReserveSlot = player.Reserve.Count + selected.ActionIndex;
        return new PlayActionCommand(player.Id, playableReserveSlot, selected.Target);
    }

    private UsePowerCommand? TryCreatePowerCommand(MatchState match, PlayerState player)
    {
        if (_powers is null ||
            player.Leader?.CurrentPowerId is not PowerId powerId ||
            !_powers.TryGet(powerId, out var power) ||
            power.Activation is null ||
            !player.Leader.CanUse(power) ||
            player.Resource < power.Activation.Cost)
        {
            return null;
        }

        var trigger = power.FindTrigger(NativeTriggerKeys.OnActivate)
            ?? throw new InvalidOperationException($"Activatable Power '{power.Id}' has no onActivate trigger.");
        var target = SelectTarget(match, player.Id, trigger.Effects);
        if (target.RequiresTarget && target.Target is null) return null;
        return new UsePowerCommand(player.Id, target.Target);
    }

    private AcquirePlayableCommand? TryCreateAcquireCommand(PlayerState player, PreparationAiStrategy strategy)
    {
        if (player.PlayableReserveCount >= _rules.ReserveCapacity || player.PlayableOfferCount == 0)
            return null;

        var affordable = player.PlayableOffer
            .Where(entry => player.Resource >= GetAcquireCost(entry))
            .ToArray();
        if (affordable.Length == 0) return null;

        var index = ChooseBestIndex(
            affordable,
            entry => ScoreOffer(entry, strategy),
            entry => entry.Id);
        return new AcquirePlayableCommand(player.Id, affordable[index].Slot);
    }

    private SelectedTarget SelectTarget(
        MatchState match,
        PlayerId ownerPlayerId,
        IReadOnlyList<EffectDefinition> effects)
    {
        var selectedEffects = effects
            .Select(effect => (Effect: effect, Selector: GetTargetSelector(effect)))
            .Where(value => value.Selector?.Scope == EffectTargetScope.Selected)
            .Select(value => (value.Effect, Selector: value.Selector!))
            .ToArray();
        if (selectedEffects.Length == 0) return new SelectedTarget(false, null);

        var candidates = match.Players
            .SelectMany(player => player.Field.Select(unit => new TargetCandidate(player.Id, unit)))
            .Where(candidate => candidate.Unit.IsAlive)
            .Where(candidate => selectedEffects.All(value => MatchesSelector(candidate.Unit, value.Selector)))
            .ToArray();
        if (candidates.Length == 0) return new SelectedTarget(true, null);

        var prefersEnemy = selectedEffects.Any(value => value.Effect is DealDamageEffectDefinition or DestroyUnitEffectDefinition);
        var preferred = candidates
            .Where(candidate => prefersEnemy
                ? candidate.OwnerPlayerId != ownerPlayerId
                : candidate.OwnerPlayerId == ownerPlayerId)
            .ToArray();
        if (preferred.Length == 0) preferred = candidates;

        var index = ChooseBestIndex(
            preferred,
            candidate => ScoreUnitInstance(candidate.Unit),
            candidate => candidate.OwnerPlayerId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                         candidate.Unit.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new SelectedTarget(true, preferred[index].Unit.Id);
    }

    private static EffectTargetSelector? GetTargetSelector(EffectDefinition effect) =>
        effect switch
        {
            ModifyStatsEffectDefinition value => value.Target,
            DealDamageEffectDefinition value => value.Target,
            DestroyUnitEffectDefinition value => value.Target,
            TriggerEventEffectDefinition value => value.Target,
            AddBehaviorEffectDefinition value => value.Target,
            RemoveBehaviorEffectDefinition value => value.Target,
            TransformUnitEffectDefinition value => value.Target,
            CopyUnitToReserveEffectDefinition value => value.Target,
            ApplyUnitModifierEffectDefinition value => value.Target,
            RemoveUnitModifierEffectDefinition value => value.Target,
            _ => null,
        };

    private static bool MatchesSelector(UnitInstance unit, EffectTargetSelector selector) =>
        (selector.RequiredTypeId is null || unit.Definition.Types.Any(type => type.Id == selector.RequiredTypeId.Value)) &&
        (selector.RequiredTagId is null || unit.Definition.Tags.Any(tag => tag.Id == selector.RequiredTagId.Value));

    private int GetAcquireCost(Battlegrounds.Core.Domain.Playables.PlayableOfferEntry entry) =>
        entry.Action?.Cost ?? _rules.AcquireCost;

    private long ScoreOffer(
        Battlegrounds.Core.Domain.Playables.PlayableOfferEntry entry,
        PreparationAiStrategy strategy) =>
        entry.Unit is not null
            ? ScoreUnitDefinition(entry.Unit, strategy) - GetAcquireCost(entry)
            : ScoreActionDefinition(entry.Action!) - GetAcquireCost(entry);

    private static long ScoreUnitDefinition(UnitDefinition unit) =>
        ((long)unit.Tier * 10_000L) + ((long)unit.BaseAttack * 10L) + unit.BaseHealth;

    private static long ScoreUnitDefinition(UnitDefinition unit, PreparationAiStrategy strategy) =>
        ScoreUnitDefinition(unit) + strategy.GetUnitPreferenceBonus(unit);

    private static long ScoreUnitInstance(UnitInstance unit) =>
        ((long)unit.Definition.Tier * 10_000L) + ((long)unit.Attack * 10L) + unit.Health;

    private static long ScoreUnitInstance(UnitInstance unit, PreparationAiStrategy strategy) =>
        ScoreUnitInstance(unit) + strategy.GetUnitPreferenceBonus(unit.Definition);

    private static long ScoreActionDefinition(ActionDefinition action) =>
        ((long)action.Tier * 10_000L) + (action.Effects.Count * 10L) - action.Cost;

    private int ChooseBestIndex<T>(IReadOnlyList<T> values, Func<T, long> score, Func<T, string> stableId)
    {
        if (values.Count == 0) throw new ArgumentException("Cannot choose from an empty set.", nameof(values));
        var scored = values
            .Select((value, index) => new Scored<T>(value, index, score(value), stableId(value)))
            .ToArray();
        var bestScore = scored.Max(value => value.Score);
        var tied = scored
            .Where(value => value.Score == bestScore)
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        return tied[_randomSource.NextInt(0, tied.Length)].Index;
    }

    private sealed class TurnMemory
    {
        public int Round { get; }
        public int Refreshes { get; set; }
        public TurnMemory(int round) => Round = round;
    }

    private sealed record Scored<T>(T Value, int Index, long Score, string StableId);
    private sealed record ActionCandidate(int ActionIndex, ActionInstance Action, UnitInstanceId? Target);
    private sealed record TargetCandidate(PlayerId OwnerPlayerId, UnitInstance Unit);
    private readonly record struct SelectedTarget(bool RequiresTarget, UnitInstanceId? Target);
}
