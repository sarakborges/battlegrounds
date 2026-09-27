using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Preparation;

internal sealed class PreparationEffectEngine
{
    private readonly PreparationRules _rules;
    private readonly IUnitPool _unitPool;
    private readonly IRandomSource _randomSource;
    private readonly UnitCatalog? _unitCatalog;
    private readonly BehaviorCatalog? _behaviorCatalog;
    private readonly PowerCatalog? _powerCatalog;
    private MatchState? _runtimeMatch;
    private int _runtimeRound = -1;
    private PreparationEffectWorld? _runtimeWorld;
    private GameEffectRuntime? _runtime;
    private long _nextSyntheticInstanceId = long.MaxValue;

    public PreparationEffectEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog,
        PowerCatalog? powerCatalog = null)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _unitPool = unitPool ?? throw new ArgumentNullException(nameof(unitPool));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _unitCatalog = unitCatalog;
        _behaviorCatalog = behaviorCatalog;
        _powerCatalog = powerCatalog;
    }

    public void ProcessGameEvent(
        MatchState match,
        PlayerState owner,
        NativeGameEventKey @event,
        UnitDefinition? unit = null)
    {
        var (_, runtime) = GetRuntime(match);
        runtime.RecordGameEvent(owner.Id, @event, unit);
    }

    public void ProcessPlayedUnit(MatchState match, PlayerState owner, UnitInstance unit)
    {
        var (world, runtime) = GetRuntime(match);
        var subject = world.Wrap(unit, owner.Id);

        runtime.RecordGameEvent(owner.Id, NativeGameEventKeys.UnitPlayed, unit.Definition);
        runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnPlay, subject));

        if (unit.IsAlive && owner.Field.Any(candidate => candidate.Id == unit.Id))
        {
            runtime.RecordGameEvent(owner.Id, NativeGameEventKeys.UnitSummoned, unit.Definition);
            runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnSummon, subject));
        }
    }

    public void ProcessPower(
        MatchState match,
        PlayerState owner,
        PowerDefinition power,
        UnitInstanceId? selectedTargetInstanceId)
    {
        ArgumentNullException.ThrowIfNull(power);
        var trigger = power.FindTrigger(NativeTriggerKeys.OnActivate)
            ?? throw new InvalidOperationException($"Power '{power.Id}' has no onActivate trigger.");
        var (_, runtime) = GetRuntime(match);
        var source = CreatePowerSource(owner, power);
        runtime.ProcessTrigger(source, trigger, selectedTargetInstanceId);
    }

    public void ProcessPowerEvent(MatchState match, PlayerState owner, NativeTriggerKey eventKey)
    {
        if (eventKey != NativeTriggerKeys.OnMatchStart &&
            eventKey != NativeTriggerKeys.OnTurnStart &&
            eventKey != NativeTriggerKeys.OnTurnEnd)
        {
            throw new ArgumentException("Preparation power lifecycle event is not supported here.", nameof(eventKey));
        }

        if (!TryGetCurrentPower(owner, out var power) || power.FindTrigger(eventKey) is null)
        {
            return;
        }

        var (_, runtime) = GetRuntime(match);
        runtime.Process(new GameEffectEvent(eventKey, CreatePowerSource(owner, power)));
    }

    public void ProcessTurnEvent(MatchState match, PlayerState owner, NativeTriggerKey eventKey)
    {
        if (eventKey != NativeTriggerKeys.OnTurnStart && eventKey != NativeTriggerKeys.OnTurnEnd)
        {
            throw new ArgumentException("Preparation turn events must be onTurnStart or onTurnEnd.", nameof(eventKey));
        }

        var (world, runtime) = GetRuntime(match);

        if (TryGetCurrentPower(owner, out var power) && power.FindTrigger(eventKey) is not null)
        {
            runtime.Process(new GameEffectEvent(eventKey, CreatePowerSource(owner, power)));
        }

        var initialUnits = owner.Field.ToArray();
        foreach (var unit in initialUnits)
        {
            if (!unit.IsAlive || !owner.Field.Any(candidate => candidate.Id == unit.Id))
            {
                continue;
            }

            runtime.Process(new GameEffectEvent(eventKey, world.Wrap(unit, owner.Id)));
        }
    }

    private bool TryGetCurrentPower(PlayerState owner, out PowerDefinition power)
    {
        power = null!;
        return _powerCatalog is not null &&
               owner.Leader?.CurrentPowerId is PowerId powerId &&
               _powerCatalog.TryGet(powerId, out power);
    }

    private PowerRuntimeUnit CreatePowerSource(PlayerState owner, PowerDefinition power)
    {
        var (world, _) = GetRuntime(_runtimeMatch ?? throw new InvalidOperationException("Effect runtime has not been initialized."));
        while (_nextSyntheticInstanceId > 0 &&
               world.TryGetUnit(new UnitInstanceId(_nextSyntheticInstanceId), out _))
        {
            _nextSyntheticInstanceId--;
        }
        if (_nextSyntheticInstanceId <= 0)
        {
            throw new InvalidOperationException("Synthetic effect source id space was exhausted.");
        }

        var definition = new UnitDefinition(
            new UnitId("__power__" + power.Id.Value),
            power.Name,
            tier: 1,
            baseAttack: 0,
            baseHealth: 1,
            triggers: power.Triggers);

        return new PowerRuntimeUnit(
            new UnitInstanceId(_nextSyntheticInstanceId--),
            owner.Id,
            power.Id,
            definition);
    }

    private (PreparationEffectWorld World, GameEffectRuntime Runtime) GetRuntime(MatchState match)
    {
        if (!ReferenceEquals(_runtimeMatch, match) || _runtimeRound != match.Round)
        {
            _runtimeMatch = match;
            _runtimeRound = match.Round;
            _runtimeWorld = new PreparationEffectWorld(
                match,
                _rules,
                _unitPool,
                _powerCatalog,
                CreatePowerSource);
            _runtime = new GameEffectRuntime(
                _runtimeWorld,
                _randomSource,
                _unitCatalog,
                _behaviorCatalog);
        }

        return (_runtimeWorld!, _runtime!);
    }

    private sealed class PreparationEffectWorld : IEffectRuntimeWorld, IGenerationChoiceRuntimeWorld
    {
        private readonly MatchState _match;
        private readonly PreparationRules _rules;
        private readonly IUnitPool _unitPool;
        private readonly PowerCatalog? _powerCatalog;
        private readonly Func<PlayerState, PowerDefinition, PowerRuntimeUnit> _powerSourceFactory;
        private readonly Dictionary<UnitInstanceId, PreparationRuntimeUnit> _wrappers = [];
        private readonly Dictionary<UnitInstanceId, (PlayerId OwnerId, int Index)> _deathPositions = [];
        private readonly Dictionary<UnitInstanceId, int> _summonCursors = [];

        public PreparationEffectWorld(
            MatchState match,
            PreparationRules rules,
            IUnitPool unitPool,
            PowerCatalog? powerCatalog,
            Func<PlayerState, PowerDefinition, PowerRuntimeUnit> powerSourceFactory)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _unitPool = unitPool ?? throw new ArgumentNullException(nameof(unitPool));
            _powerCatalog = powerCatalog;
            _powerSourceFactory = powerSourceFactory ?? throw new ArgumentNullException(nameof(powerSourceFactory));
        }

        public IReadOnlyList<IEffectRuntimeUnit> Units =>
            _match.Players
                .SelectMany(player => player.Field.Select(unit => (IEffectRuntimeUnit)Wrap(unit, player.Id)))
                .ToArray();

        public PreparationRuntimeUnit Wrap(UnitInstance unit, PlayerId ownerPlayerId)
        {
            if (_wrappers.TryGetValue(unit.Id, out var existing))
            {
                return existing;
            }

            var wrapper = new PreparationRuntimeUnit(unit, ownerPlayerId);
            _wrappers.Add(unit.Id, wrapper);
            return wrapper;
        }

        public bool TryGetUnit(UnitInstanceId instanceId, out IEffectRuntimeUnit unit)
        {
            foreach (var player in _match.Players)
            {
                if (player.TryGetFieldUnit(instanceId, out var found))
                {
                    unit = Wrap(found, player.Id);
                    return true;
                }
            }

            unit = null!;
            return false;
        }

        public IReadOnlyList<IEffectRuntimeUnit> GetHistoryEventListeners(PlayerId playerId)
        {
            var player = GetPlayer(playerId);
            var listeners = new List<IEffectRuntimeUnit>();
            if (_powerCatalog is not null &&
                player.Leader?.CurrentPowerId is PowerId powerId &&
                _powerCatalog.TryGet(powerId, out var power))
            {
                listeners.Add(_powerSourceFactory(player, power));
            }
            listeners.AddRange(player.Field.Select(unit => (IEffectRuntimeUnit)Wrap(unit, player.Id)));
            return listeners;
        }

        public void ModifyStats(IEffectRuntimeUnit unit, int attackDelta, int healthDelta) =>
            GetUnit(unit).ModifyStats(attackDelta, healthDelta);

        public bool TryConsumeBehavior(IEffectRuntimeUnit unit, NativeBehaviorKey handler) =>
            unit is PreparationRuntimeUnit ? GetUnit(unit).RemoveBehavior(handler) : false;

        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior) =>
            GetUnit(unit).AddBehavior(behavior);

        public bool RemoveBehavior(IEffectRuntimeUnit unit, BehaviorId behaviorId) =>
            GetUnit(unit).RemoveBehavior(behaviorId);

        public void TakeDamage(IEffectRuntimeUnit unit, int amount) =>
            GetUnit(unit).TakeDamage(amount);

        public void Destroy(IEffectRuntimeUnit unit) => GetUnit(unit).Destroy();

        public IReadOnlyList<IEffectRuntimeUnit> Summon(
            IEffectRuntimeUnit source,
            UnitDefinition definition,
            int count)
        {
            if (!_match.TryGetPlayer(source.OwnerPlayerId, out var owner))
            {
                throw new InvalidOperationException("Effect source owner is not part of the match.");
            }

            var summoned = new List<IEffectRuntimeUnit>();
            var insertionIndex = source is PowerRuntimeUnit
                ? owner.Field.Count
                : ResolveSummonIndex(source, owner);

            for (var index = 0; index < count && owner.Field.Count < _rules.FieldCapacity; index++)
            {
                var instance = _match.CreateUnit(definition, UnitInstanceOrigin.Generated);
                insertionIndex = Math.Clamp(insertionIndex, 0, owner.Field.Count);
                owner.InsertIntoField(insertionIndex, instance);
                insertionIndex++;
                if (source is not PowerRuntimeUnit)
                {
                    _summonCursors[source.InstanceId] = insertionIndex;
                }
                summoned.Add(Wrap(instance, owner.Id));
            }

            return summoned;
        }

        public int GenerateUnitToReserve(PlayerId playerId, UnitDefinition definition, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var player = GetPlayer(playerId);
            var available = Math.Max(
                0,
                _rules.ReserveCapacity - player.Reserve.Count - player.PendingChoiceCount);
            var generatedCount = Math.Min(count, available);
            for (var index = 0; index < generatedCount; index++)
            {
                player.AddToReserve(_match.CreateUnit(definition, UnitInstanceOrigin.Generated));
            }

            return generatedCount;
        }

        public bool QueueUnitChoice(PlayerId playerId, IReadOnlyList<UnitDefinition> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var player = GetPlayer(playerId);
            if (options.Count == 0 ||
                player.Reserve.Count + player.PendingChoiceCount >= _rules.ReserveCapacity)
            {
                return false;
            }

            return player.QueueUnitChoice(options);
        }

        public void AdjustResource(PlayerId playerId, int amount)
        {
            GetPlayer(playerId).AdjustResource(amount, _rules.MaximumResource);
        }

        public void SetPower(PlayerId playerId, PowerId powerId)
        {
            if (_powerCatalog is not null && !_powerCatalog.TryGet(powerId, out _))
            {
                throw new InvalidOperationException($"Unknown power '{powerId}'.");
            }
            var player = GetPlayer(playerId);
            if (player.Leader is null)
            {
                throw new InvalidOperationException("Effect source owner has no leader state.");
            }

            player.Leader.SetPower(powerId);
        }

        public EffectHistorySnapshot GetHistory(PlayerId playerId) =>
            GetPlayer(playerId).EffectHistory.Snapshot();

        public void RecordEvent(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit = null) =>
            GetPlayer(playerId).EffectHistory.RecordEvent(@event, unit);

        public int GetTriggerActivationCount(
            PlayerId playerId,
            EffectSourceKey source,
            int triggerIndex,
            EffectHistoryScope scope) =>
            scope == EffectHistoryScope.Combat
                ? int.MaxValue
                : GetPlayer(playerId).EffectHistory.GetTriggerActivationCount(source, triggerIndex, scope);

        public void RecordTriggerActivation(
            PlayerId playerId,
            EffectSourceKey source,
            int triggerIndex,
            EffectHistoryScope scope)
        {
            if (scope == EffectHistoryScope.Combat)
            {
                return;
            }
            GetPlayer(playerId).EffectHistory.RecordTriggerActivation(source, triggerIndex, scope);
        }

        public IReadOnlyList<IEffectRuntimeUnit> ExtractDeadUnits()
        {
            var dead = new List<IEffectRuntimeUnit>();

            foreach (var player in _match.Players)
            {
                var index = 0;
                while (index < player.Field.Count)
                {
                    var unit = player.Field[index];
                    if (unit.IsAlive)
                    {
                        index++;
                        continue;
                    }

                    var wrapper = Wrap(unit, player.Id);
                    _deathPositions[unit.Id] = (player.Id, index);
                    _summonCursors[unit.Id] = index;
                    player.RemoveFromField(index);
                    dead.Add(wrapper);
                }
            }

            return dead;
        }

        public IEffectRuntimeUnit? TryRevive(IEffectRuntimeUnit deadUnit)
        {
            var wrapper = GetWrapper(deadUnit);
            if (!wrapper.Unit.Behaviors.Any(behavior => behavior.Handler == NativeBehaviorKeys.ReviveOnce))
            {
                return null;
            }

            if (!_match.TryGetPlayer(wrapper.OwnerPlayerId, out var owner) ||
                owner.Field.Count >= _rules.FieldCapacity)
            {
                return null;
            }

            var insertionIndex = ResolveSummonIndex(wrapper, owner);
            wrapper.Unit.ResetForReborn();
            owner.InsertIntoField(Math.Clamp(insertionIndex, 0, owner.Field.Count), wrapper.Unit);
            _summonCursors[wrapper.InstanceId] = insertionIndex + 1;
            return wrapper;
        }

        public void FinalizeDeath(IEffectRuntimeUnit deadUnit)
        {
            var unit = GetUnit(deadUnit);
            if (unit.Origin == UnitInstanceOrigin.Pooled)
            {
                _unitPool.ReturnUnit(unit.Definition);
            }
        }

        private PlayerState GetPlayer(PlayerId playerId) =>
            _match.TryGetPlayer(playerId, out var player)
                ? player
                : throw new InvalidOperationException($"Effect history owner '{playerId}' is not part of the match.");

        private int ResolveSummonIndex(IEffectRuntimeUnit source, PlayerState owner)
        {
            if (_summonCursors.TryGetValue(source.InstanceId, out var cursor))
            {
                return cursor;
            }

            var currentIndex = owner.IndexOfFieldUnit(source.InstanceId);
            if (currentIndex >= 0)
            {
                return currentIndex + 1;
            }

            if (_deathPositions.TryGetValue(source.InstanceId, out var death))
            {
                return death.Index;
            }

            return owner.Field.Count;
        }

        private static UnitInstance GetUnit(IEffectRuntimeUnit unit) =>
            GetWrapper(unit).Unit;

        private static PreparationRuntimeUnit GetWrapper(IEffectRuntimeUnit unit) =>
            unit as PreparationRuntimeUnit
            ?? throw new InvalidOperationException("Effect runtime unit does not belong to preparation state.");
    }

    private sealed class PreparationRuntimeUnit : IEffectRuntimeUnit
    {
        public UnitInstance Unit { get; }
        public UnitInstanceId InstanceId => Unit.Id;
        public PlayerId OwnerPlayerId { get; }
        public UnitDefinition Definition => Unit.Definition;
        public int Attack => Unit.Attack;
        public int Health => Unit.Health;
        public bool IsAlive => Unit.IsAlive;

        public PreparationRuntimeUnit(UnitInstance unit, PlayerId ownerPlayerId)
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            OwnerPlayerId = ownerPlayerId;
        }
    }

    private sealed class PowerRuntimeUnit : IEffectRuntimeUnit
    {
        public UnitInstanceId InstanceId { get; }
        public PlayerId OwnerPlayerId { get; }
        public PowerId PowerId { get; }
        public UnitDefinition Definition { get; }
        public EffectSourceKey SourceKey => EffectSourceKey.ForPower(PowerId);
        public int Attack => 0;
        public int Health => 1;
        public bool IsAlive => true;

        public PowerRuntimeUnit(
            UnitInstanceId instanceId,
            PlayerId ownerPlayerId,
            PowerId powerId,
            UnitDefinition definition)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerPlayerId;
            PowerId = powerId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
