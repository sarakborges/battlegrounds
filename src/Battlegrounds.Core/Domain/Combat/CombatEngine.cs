using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Combat;

public sealed class CombatEngine
{
    private readonly int? _fieldCapacity;
    private readonly UnitCatalog? _unitCatalog;
    private readonly BehaviorCatalog? _behaviorCatalog;
    private readonly PowerCatalog? _powerCatalog;

    public CombatEngine()
    {
    }

    public CombatEngine(
        int fieldCapacity,
        UnitCatalog unitCatalog,
        BehaviorCatalog behaviorCatalog,
        PowerCatalog? powerCatalog = null)
    {
        if (fieldCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(fieldCapacity));
        _fieldCapacity = fieldCapacity;
        _unitCatalog = unitCatalog ?? throw new ArgumentNullException(nameof(unitCatalog));
        _behaviorCatalog = behaviorCatalog ?? throw new ArgumentNullException(nameof(behaviorCatalog));
        _powerCatalog = powerCatalog;
    }

    public CombatResult Resolve(CombatInput input, CombatRules rules, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(randomSource);

        var world = new CombatEffectWorld(input, _fieldCapacity ?? int.MaxValue, _powerCatalog);
        var runtime = new GameEffectRuntime(world, randomSource, _unitCatalog, _behaviorCatalog);

        ProcessPhaseEvent(world, runtime, NativeTriggerKeys.OnCombatStart);

        if (TryGetTerminal(world, out var initialReason, out var initialWinner))
        {
            return Complete(world, runtime, initialReason, initialWinner, []);
        }

        var attackingLeft = ChooseStartingSide(
            world.Left,
            world.Right,
            rules.StartingSidePolicy,
            randomSource);
        var attacks = new List<CombatAttack>();
        var attackSequence = 0;

        while (true)
        {
            var attackerSide = attackingLeft ? world.Left : world.Right;
            var targetSide = attackingLeft ? world.Right : world.Left;
            var attacker = attackerSide.TakeNextAttacker();

            if (attacker is null)
            {
                attackingLeft = !attackingLeft;
                continue;
            }

            var strikeCount = attacker.Has(NativeBehaviorKeys.ExtraAttack) ? 2 : 1;

            for (var strike = 0; strike < strikeCount; strike++)
            {
                var deathsBeforeAttackEvent = attacker.DeathCount;
                runtime.RecordGameEvent(
                    attacker.OwnerPlayerId,
                    NativeGameEventKeys.UnitAttacked,
                    attacker.Definition);
                runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnAttack, attacker));

                if (attacker.DeathCount > deathsBeforeAttackEvent ||
                    !attackerSide.Contains(attacker.InstanceId) ||
                    !attacker.IsAlive)
                {
                    break;
                }

                if (TryGetTerminal(world, out var beforeStrikeReason, out var beforeStrikeWinner))
                {
                    return Complete(world, runtime, beforeStrikeReason, beforeStrikeWinner, attacks);
                }

                var targets = targetSide.GetValidTargets();
                if (targets.Count == 0)
                {
                    return Complete(world, runtime, CombatEndReason.Elimination, attackerSide.PlayerId, attacks);
                }

                var target = targets[randomSource.NextInt(0, targets.Count)];
                var attackerDeathsBefore = attacker.DeathCount;
                var targetDeathsBefore = target.DeathCount;
                var attackerRebirthsBefore = attacker.RebirthCount;
                var targetRebirthsBefore = target.RebirthCount;

                world.RecordAttackStarted(attacker, target);
                var damageToTarget = ApplyAttackDamage(world, attacker, target);
                var damageToAttacker = ApplyAttackDamage(world, target, attacker);

                var damageEvents = new List<GameEffectEvent>();
                if (damageToTarget.DamageDealt > 0)
                {
                    runtime.RecordGameEvent(
                        target.OwnerPlayerId,
                        NativeGameEventKeys.UnitDamaged,
                        target.Definition,
                        resolveDeaths: false);
                    damageEvents.Add(new GameEffectEvent(NativeTriggerKeys.OnDamage, target));
                }
                if (damageToAttacker.DamageDealt > 0)
                {
                    runtime.RecordGameEvent(
                        attacker.OwnerPlayerId,
                        NativeGameEventKeys.UnitDamaged,
                        attacker.Definition,
                        resolveDeaths: false);
                    damageEvents.Add(new GameEffectEvent(NativeTriggerKeys.OnDamage, attacker));
                }
                if (damageEvents.Count > 0)
                {
                    runtime.Process(damageEvents);
                }

                attackSequence++;
                attacks.Add(new CombatAttack(
                    attackSequence,
                    attackerSide.PlayerId,
                    attacker.InstanceId,
                    targetSide.PlayerId,
                    target.InstanceId,
                    damageToAttacker.DamageDealt,
                    damageToTarget.DamageDealt,
                    damageToAttacker.BarrierLost,
                    damageToTarget.BarrierLost,
                    damageToTarget.LethalTriggered,
                    damageToAttacker.LethalTriggered,
                    attacker.IsAlive ? attacker.Health : 0,
                    target.IsAlive ? target.Health : 0,
                    attacker.DeathCount > attackerDeathsBefore,
                    target.DeathCount > targetDeathsBefore,
                    attacker.RebirthCount > attackerRebirthsBefore,
                    target.RebirthCount > targetRebirthsBefore));

                if (TryGetTerminal(world, out var reason, out var winner))
                {
                    return Complete(world, runtime, reason, winner, attacks);
                }

                if (attacker.DeathCount > attackerDeathsBefore)
                {
                    break;
                }
            }

            attackingLeft = !attackingLeft;
        }
    }

    private CombatResult Complete(
        CombatEffectWorld world,
        GameEffectRuntime runtime,
        CombatEndReason reason,
        PlayerId? winner,
        IEnumerable<CombatAttack> attacks)
    {
        ProcessPhaseEvent(world, runtime, NativeTriggerKeys.OnCombatEnd);
        return new CombatResult(
            winner,
            reason,
            attacks,
            world.Left.GetSurvivors(),
            world.Right.GetSurvivors(),
            world.ResourceDeltas,
            world.PowerChanges,
            world.HistoryDeltas,
            world.Timeline);
    }

    private void ProcessPhaseEvent(
        CombatEffectWorld world,
        GameEffectRuntime runtime,
        NativeTriggerKey eventKey)
    {
        var leftInitialUnits = world.Left.Units.ToArray();
        var rightInitialUnits = world.Right.Units.ToArray();

        ProcessPowerPhaseEvent(world, runtime, world.Left, eventKey);
        ProcessInitialUnitPhaseEvent(world, runtime, leftInitialUnits, eventKey);
        ProcessPowerPhaseEvent(world, runtime, world.Right, eventKey);
        ProcessInitialUnitPhaseEvent(world, runtime, rightInitialUnits, eventKey);
    }

    private void ProcessPowerPhaseEvent(
        CombatEffectWorld world,
        GameEffectRuntime runtime,
        SideState side,
        NativeTriggerKey eventKey)
    {
        if (_powerCatalog is null ||
            side.CurrentPowerId is not PowerId powerId ||
            !_powerCatalog.TryGet(powerId, out var power) ||
            power.FindTrigger(eventKey) is null)
        {
            return;
        }

        world.RecordTrigger(side.PlayerId, eventKey, null, powerId);
        runtime.Process(new GameEffectEvent(eventKey, world.CreatePowerSource(side, power)));
    }

    private static void ProcessInitialUnitPhaseEvent(
        CombatEffectWorld world,
        GameEffectRuntime runtime,
        IReadOnlyList<CombatRuntimeUnit> initialUnits,
        NativeTriggerKey eventKey)
    {
        foreach (var unit in initialUnits)
        {
            if (!unit.IsAlive || !world.TryGetUnit(unit.InstanceId, out _))
            {
                continue;
            }

            if (unit.Definition.Triggers.Any(trigger => trigger.Event == eventKey))
            {
                world.RecordTrigger(unit.OwnerPlayerId, eventKey, unit.InstanceId, null);
            }
            runtime.Process(new GameEffectEvent(eventKey, unit));
        }
    }

    private static DamageResult ApplyAttackDamage(
        CombatEffectWorld world,
        CombatRuntimeUnit source,
        CombatRuntimeUnit target)
    {
        if (source.Attack <= 0)
        {
            return default;
        }

        if (world.TryConsumeBehavior(target, NativeBehaviorKeys.DamageBarrier))
        {
            return new DamageResult(0, true, false);
        }

        world.TakeDamage(target, source.Attack);
        var lethalTriggered = world.TryConsumeBehavior(source, NativeBehaviorKeys.LethalFirstDamagePerCombat);
        if (lethalTriggered)
        {
            world.Destroy(target);
        }

        return new DamageResult(source.Attack, false, lethalTriggered);
    }

    private static bool TryGetTerminal(
        CombatEffectWorld world,
        out CombatEndReason reason,
        out PlayerId? winner)
    {
        if (world.Left.UnitCount == 0 || world.Right.UnitCount == 0)
        {
            reason = CombatEndReason.Elimination;
            winner = world.Left.UnitCount > 0
                ? world.Left.PlayerId
                : world.Right.UnitCount > 0
                    ? world.Right.PlayerId
                    : null;
            return true;
        }

        if (!world.Left.HasAttackPower && !world.Right.HasAttackPower)
        {
            reason = CombatEndReason.NoAttackPower;
            winner = null;
            return true;
        }

        reason = default;
        winner = null;
        return false;
    }

    private static bool ChooseStartingSide(
        SideState left,
        SideState right,
        StartingSidePolicy policy,
        IRandomSource randomSource)
    {
        return policy switch
        {
            StartingSidePolicy.Random => randomSource.NextInt(0, 2) == 0,
            StartingSidePolicy.LargerFieldThenRandom =>
                left.UnitCount > right.UnitCount
                    ? true
                    : left.UnitCount < right.UnitCount
                        ? false
                        : randomSource.NextInt(0, 2) == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported starting side policy."),
        };
    }

    private sealed class CombatEffectWorld : IEffectRuntimeWorld
    {
        private readonly int _fieldCapacity;
        private readonly PowerCatalog? _powerCatalog;
        private readonly Dictionary<UnitInstanceId, (SideState Side, int Index)> _deathPositions = [];
        private readonly Dictionary<UnitInstanceId, int> _summonCursors = [];
        private readonly Dictionary<PlayerId, int> _resourceDeltas = [];
        private readonly Dictionary<PlayerId, PowerId> _powerChanges = [];
        private readonly Dictionary<PlayerId, CombatEffectHistoryState> _histories;
        private readonly List<CombatTimelineEvent> _timeline = [];
        private long _nextInstanceId;
        private long _nextSyntheticInstanceId = long.MaxValue;
        private int _nextTimelineSequence;

        public SideState Left { get; }
        public SideState Right { get; }
        public IReadOnlyDictionary<PlayerId, int> ResourceDeltas => _resourceDeltas;
        public IReadOnlyDictionary<PlayerId, PowerId> PowerChanges => _powerChanges;
        public IReadOnlyList<CombatTimelineEvent> Timeline => _timeline;
        public IReadOnlyDictionary<PlayerId, EffectHistoryDelta> HistoryDeltas =>
            _histories.ToDictionary(pair => pair.Key, pair => pair.Value.CreateDelta());

        public IReadOnlyList<IEffectRuntimeUnit> Units =>
            Left.Units.Cast<IEffectRuntimeUnit>()
                .Concat(Right.Units)
                .ToArray();

        public CombatEffectWorld(CombatInput input, int fieldCapacity, PowerCatalog? powerCatalog)
        {
            _fieldCapacity = fieldCapacity;
            _powerCatalog = powerCatalog;
            Left = new SideState(input.Left);
            Right = new SideState(input.Right);
            _histories = new Dictionary<PlayerId, CombatEffectHistoryState>
            {
                [input.Left.PlayerId] = new CombatEffectHistoryState(input.Left.History),
                [input.Right.PlayerId] = new CombatEffectHistoryState(input.Right.History),
            };
            var ids = input.Left.Units.Concat(input.Right.Units).Select(unit => unit.InstanceId.Value).ToArray();
            _nextInstanceId = ids.Length == 0 ? 1 : ids.Max() + 1;
        }

        public void RecordAttackStarted(CombatRuntimeUnit attacker, CombatRuntimeUnit target) =>
            _timeline.Add(new CombatAttackStartedTimelineEvent(
                NextTimelineSequence(),
                attacker.OwnerPlayerId,
                attacker.InstanceId,
                target.OwnerPlayerId,
                target.InstanceId));

        public void RecordTrigger(
            PlayerId sourcePlayerId,
            NativeTriggerKey trigger,
            UnitInstanceId? sourceUnitInstanceId,
            PowerId? sourcePowerId) =>
            _timeline.Add(new CombatTriggerTimelineEvent(
                NextTimelineSequence(),
                sourcePlayerId,
                trigger,
                sourceUnitInstanceId,
                sourcePowerId));

        public CombatPowerRuntimeUnit CreatePowerSource(SideState side, PowerDefinition power)
        {
            while (_nextSyntheticInstanceId > 0 && TryGetUnit(new UnitInstanceId(_nextSyntheticInstanceId), out _))
            {
                _nextSyntheticInstanceId--;
            }
            if (_nextSyntheticInstanceId <= 0)
            {
                throw new InvalidOperationException("Synthetic combat effect source id space was exhausted.");
            }

            var definition = new UnitDefinition(
                new UnitId("__power__" + power.Id.Value),
                power.Name,
                tier: 1,
                baseAttack: 0,
                baseHealth: 1,
                triggers: power.Triggers);

            return new CombatPowerRuntimeUnit(
                new UnitInstanceId(_nextSyntheticInstanceId--),
                side.PlayerId,
                power.Id,
                definition);
        }

        public bool TryGetUnit(UnitInstanceId instanceId, out IEffectRuntimeUnit unit)
        {
            if (Left.TryGet(instanceId, out var left))
            {
                unit = left;
                return true;
            }
            if (Right.TryGet(instanceId, out var right))
            {
                unit = right;
                return true;
            }

            unit = null!;
            return false;
        }

        public IReadOnlyList<IEffectRuntimeUnit> GetHistoryEventListeners(PlayerId playerId)
        {
            var side = GetSide(playerId);
            var listeners = new List<IEffectRuntimeUnit>();
            if (_powerCatalog is not null &&
                side.CurrentPowerId is PowerId powerId &&
                _powerCatalog.TryGet(powerId, out var power))
            {
                listeners.Add(CreatePowerSource(side, power));
            }
            listeners.AddRange(side.Units.Where(unit => unit.IsAlive));
            return listeners;
        }

        public void ModifyStats(IEffectRuntimeUnit unit, int attackDelta, int healthDelta)
        {
            var runtimeUnit = GetUnit(unit);
            var attackBefore = runtimeUnit.Attack;
            var healthBefore = runtimeUnit.Health;
            runtimeUnit.ModifyStats(attackDelta, healthDelta);
            if (runtimeUnit.Attack == attackBefore && runtimeUnit.Health == healthBefore) return;
            _timeline.Add(new CombatUnitStatsChangedTimelineEvent(
                NextTimelineSequence(),
                runtimeUnit.OwnerPlayerId,
                runtimeUnit.InstanceId,
                attackBefore,
                runtimeUnit.Attack,
                healthBefore,
                runtimeUnit.Health));
        }

        public bool TryConsumeBehavior(IEffectRuntimeUnit unit, NativeBehaviorKey handler)
        {
            if (unit is not CombatRuntimeUnit runtimeUnit) return false;
            var behavior = runtimeUnit.FindBehavior(handler);
            if (behavior is null || !runtimeUnit.RemoveBehavior(handler)) return false;
            _timeline.Add(new CombatBehaviorChangedTimelineEvent(
                NextTimelineSequence(),
                runtimeUnit.OwnerPlayerId,
                runtimeUnit.InstanceId,
                behavior.Id,
                behavior.Handler,
                CombatBehaviorChangeKind.Consumed));
            return true;
        }

        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior)
        {
            var runtimeUnit = GetUnit(unit);
            if (!runtimeUnit.AddBehavior(behavior)) return false;
            _timeline.Add(new CombatBehaviorChangedTimelineEvent(
                NextTimelineSequence(),
                runtimeUnit.OwnerPlayerId,
                runtimeUnit.InstanceId,
                behavior.Id,
                behavior.Handler,
                CombatBehaviorChangeKind.Added));
            return true;
        }

        public bool RemoveBehavior(IEffectRuntimeUnit unit, BehaviorId behaviorId)
        {
            var runtimeUnit = GetUnit(unit);
            var behavior = runtimeUnit.FindBehavior(behaviorId);
            if (behavior is null || !runtimeUnit.RemoveBehavior(behaviorId)) return false;
            _timeline.Add(new CombatBehaviorChangedTimelineEvent(
                NextTimelineSequence(),
                runtimeUnit.OwnerPlayerId,
                runtimeUnit.InstanceId,
                behavior.Id,
                behavior.Handler,
                CombatBehaviorChangeKind.Removed));
            return true;
        }

        public void TakeDamage(IEffectRuntimeUnit unit, int amount)
        {
            var runtimeUnit = GetUnit(unit);
            var healthBefore = runtimeUnit.Health;
            runtimeUnit.TakeDamage(amount);
            _timeline.Add(new CombatUnitDamagedTimelineEvent(
                NextTimelineSequence(),
                runtimeUnit.OwnerPlayerId,
                runtimeUnit.InstanceId,
                amount,
                healthBefore,
                runtimeUnit.Health));
        }

        public void Destroy(IEffectRuntimeUnit unit)
        {
            var runtimeUnit = GetUnit(unit);
            var healthBefore = runtimeUnit.Health;
            runtimeUnit.Destroy();
            if (runtimeUnit.Health == healthBefore) return;
            _timeline.Add(new CombatUnitDestroyedTimelineEvent(
                NextTimelineSequence(),
                runtimeUnit.OwnerPlayerId,
                runtimeUnit.InstanceId,
                healthBefore,
                runtimeUnit.Health));
        }

        public IReadOnlyList<IEffectRuntimeUnit> Summon(
            IEffectRuntimeUnit source,
            UnitDefinition definition,
            int count)
        {
            var side = GetSide(source.OwnerPlayerId);
            var sourcePowerId = source is CombatPowerRuntimeUnit powerSource ? powerSource.PowerId : null;
            var sourceUnitInstanceId = source is CombatPowerRuntimeUnit ? null : source.InstanceId;
            var insertionIndex = source is CombatPowerRuntimeUnit
                ? side.UnitCount
                : ResolveSummonIndex(source, side);
            var summoned = new List<IEffectRuntimeUnit>();

            for (var index = 0; index < count && side.UnitCount < _fieldCapacity; index++)
            {
                var unit = CombatRuntimeUnit.FromDefinition(
                    new UnitInstanceId(_nextInstanceId++),
                    side.PlayerId,
                    definition);
                insertionIndex = Math.Clamp(insertionIndex, 0, side.UnitCount);
                var insertedAt = insertionIndex;
                side.InsertAt(insertedAt, unit);
                insertionIndex++;
                if (source is not CombatPowerRuntimeUnit)
                {
                    _summonCursors[source.InstanceId] = insertionIndex;
                }
                summoned.Add(unit);
                _timeline.Add(new CombatUnitSummonedTimelineEvent(
                    NextTimelineSequence(),
                    unit.Snapshot(),
                    insertedAt,
                    sourceUnitInstanceId,
                    sourcePowerId));
            }

            return summoned;
        }

        public void AdjustResource(PlayerId playerId, int amount)
        {
            _resourceDeltas[playerId] = _resourceDeltas.GetValueOrDefault(playerId) + amount;
            _timeline.Add(new CombatResourceChangedTimelineEvent(NextTimelineSequence(), playerId, amount));
        }

        public void SetPower(PlayerId playerId, PowerId powerId)
        {
            if (_powerCatalog is not null && !_powerCatalog.TryGet(powerId, out _))
            {
                throw new InvalidOperationException($"Unknown power '{powerId}'.");
            }

            var side = GetSide(playerId);
            var previousPowerId = side.CurrentPowerId;
            side.SetPower(powerId);
            _powerChanges[playerId] = powerId;
            _timeline.Add(new CombatPowerChangedTimelineEvent(
                NextTimelineSequence(),
                playerId,
                previousPowerId,
                powerId));
        }

        public EffectHistorySnapshot GetHistory(PlayerId playerId) =>
            GetHistoryState(playerId).Snapshot();

        public void RecordEvent(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit = null) =>
            GetHistoryState(playerId).RecordEvent(@event, unit);

        public int GetTriggerActivationCount(
            PlayerId playerId,
            EffectSourceKey source,
            int triggerIndex,
            EffectHistoryScope scope) =>
            GetHistoryState(playerId).GetTriggerActivationCount(source, triggerIndex, scope);

        public void RecordTriggerActivation(
            PlayerId playerId,
            EffectSourceKey source,
            int triggerIndex,
            EffectHistoryScope scope) =>
            GetHistoryState(playerId).RecordTriggerActivation(source, triggerIndex, scope);

        public IReadOnlyList<IEffectRuntimeUnit> ExtractDeadUnits()
        {
            var result = new List<IEffectRuntimeUnit>();
            ExtractDead(Left, result);
            ExtractDead(Right, result);
            return result;
        }

        public IEffectRuntimeUnit? TryRevive(IEffectRuntimeUnit deadUnit)
        {
            var unit = GetUnit(deadUnit);
            if (!unit.Has(NativeBehaviorKeys.ReviveOnce))
            {
                return null;
            }

            var side = GetSide(unit.OwnerPlayerId);
            if (side.UnitCount >= _fieldCapacity)
            {
                return null;
            }

            var insertionIndex = Math.Clamp(ResolveSummonIndex(unit, side), 0, side.UnitCount);
            unit.ResetForReborn();
            side.InsertAt(insertionIndex, unit);
            unit.RebirthCount++;
            _summonCursors[unit.InstanceId] = insertionIndex + 1;
            _timeline.Add(new CombatUnitRevivedTimelineEvent(
                NextTimelineSequence(),
                unit.Snapshot(),
                insertionIndex));
            return unit;
        }

        public void FinalizeDeath(IEffectRuntimeUnit deadUnit)
        {
        }

        private void ExtractDead(SideState side, List<IEffectRuntimeUnit> result)
        {
            var index = 0;
            while (index < side.UnitCount)
            {
                var unit = side.Units[index];
                if (unit.IsAlive)
                {
                    index++;
                    continue;
                }

                _deathPositions[unit.InstanceId] = (side, index);
                _summonCursors[unit.InstanceId] = index;
                side.RemoveAt(index);
                unit.DeathCount++;
                _timeline.Add(new CombatUnitDiedTimelineEvent(
                    NextTimelineSequence(),
                    unit.OwnerPlayerId,
                    unit.InstanceId,
                    unit.Definition.Id,
                    index));
                result.Add(unit);
            }
        }

        private int ResolveSummonIndex(IEffectRuntimeUnit source, SideState side)
        {
            if (_summonCursors.TryGetValue(source.InstanceId, out var cursor))
            {
                return cursor;
            }

            var currentIndex = side.IndexOf(source.InstanceId);
            if (currentIndex >= 0)
            {
                return currentIndex + 1;
            }

            if (_deathPositions.TryGetValue(source.InstanceId, out var death) && death.Side == side)
            {
                return death.Index;
            }

            return side.UnitCount;
        }

        private int NextTimelineSequence() => ++_nextTimelineSequence;

        private SideState GetSide(PlayerId playerId)
        {
            if (Left.PlayerId == playerId) return Left;
            if (Right.PlayerId == playerId) return Right;
            throw new InvalidOperationException("Effect owner is not a combat participant.");
        }

        private CombatEffectHistoryState GetHistoryState(PlayerId playerId) =>
            _histories.TryGetValue(playerId, out var history)
                ? history
                : throw new InvalidOperationException("Effect history owner is not a combat participant.");

        private static CombatRuntimeUnit GetUnit(IEffectRuntimeUnit unit) =>
            unit as CombatRuntimeUnit
            ?? throw new InvalidOperationException("Effect runtime unit does not belong to combat state.");
    }

    private sealed class SideState
    {
        private readonly List<CombatRuntimeUnit> _units;
        private int _nextAttackerIndex;

        public PlayerId PlayerId { get; }
        public PowerId? CurrentPowerId { get; private set; }
        public IReadOnlyList<CombatRuntimeUnit> Units => _units;
        public int UnitCount => _units.Count;
        public bool HasAttackPower => _units.Any(unit => unit.IsAlive && unit.Attack > 0);

        public SideState(CombatParticipant participant)
        {
            PlayerId = participant.PlayerId;
            CurrentPowerId = participant.CurrentPowerId;
            _units = participant.Units
                .Select(snapshot => CombatRuntimeUnit.FromSnapshot(participant.PlayerId, snapshot))
                .ToList();
        }

        public void SetPower(PowerId powerId) => CurrentPowerId = powerId;

        public CombatRuntimeUnit? TakeNextAttacker()
        {
            if (_units.Count == 0)
            {
                return null;
            }

            for (var offset = 0; offset < _units.Count; offset++)
            {
                var index = (_nextAttackerIndex + offset) % _units.Count;
                var candidate = _units[index];
                if (candidate.Attack <= 0)
                {
                    continue;
                }

                _nextAttackerIndex = (index + 1) % _units.Count;
                return candidate;
            }

            return null;
        }

        public IReadOnlyList<CombatRuntimeUnit> GetValidTargets()
        {
            var priority = _units
                .Where(unit => unit.Has(NativeBehaviorKeys.TargetPriority))
                .ToArray();
            return priority.Length > 0 ? priority : _units.ToArray();
        }

        public bool Contains(UnitInstanceId instanceId) => _units.Any(unit => unit.InstanceId == instanceId);

        public bool TryGet(UnitInstanceId instanceId, out CombatRuntimeUnit unit)
        {
            var found = _units.FirstOrDefault(candidate => candidate.InstanceId == instanceId);
            if (found is null)
            {
                unit = null!;
                return false;
            }

            unit = found;
            return true;
        }

        public int IndexOf(UnitInstanceId instanceId) =>
            _units.FindIndex(unit => unit.InstanceId == instanceId);

        public void InsertAt(int index, CombatRuntimeUnit unit)
        {
            if (index <= _nextAttackerIndex && _units.Count > 0)
            {
                _nextAttackerIndex++;
            }
            _units.Insert(index, unit);
            if (_units.Count > 0)
            {
                _nextAttackerIndex %= _units.Count;
            }
        }

        public CombatRuntimeUnit RemoveAt(int index)
        {
            var unit = _units[index];
            _units.RemoveAt(index);
            if (_units.Count == 0)
            {
                _nextAttackerIndex = 0;
            }
            else
            {
                if (index < _nextAttackerIndex)
                {
                    _nextAttackerIndex--;
                }
                _nextAttackerIndex %= _units.Count;
            }
            return unit;
        }

        public IReadOnlyList<CombatSurvivor> GetSurvivors() =>
            _units.Select(unit => new CombatSurvivor(unit.InstanceId, unit.Health)).ToArray();
    }

    private sealed class CombatRuntimeUnit : IEffectRuntimeUnit
    {
        private readonly List<BehaviorDefinition> _initialBehaviors;
        private readonly List<BehaviorDefinition> _behaviors;
        private readonly int _initialAttack;

        public UnitInstanceId InstanceId { get; }
        public PlayerId OwnerPlayerId { get; }
        public UnitDefinition Definition { get; }
        public int Attack { get; private set; }
        public int Health { get; set; }
        public bool IsAlive => Health > 0;
        public int DeathCount { get; set; }
        public int RebirthCount { get; set; }

        private CombatRuntimeUnit(
            UnitInstanceId instanceId,
            PlayerId ownerPlayerId,
            UnitDefinition definition,
            int attack,
            int health,
            IEnumerable<BehaviorDefinition> behaviors)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerPlayerId;
            Definition = definition;
            Attack = attack;
            Health = health;
            _initialAttack = attack;
            _initialBehaviors = behaviors.ToList();
            _behaviors = _initialBehaviors.ToList();
        }

        public static CombatRuntimeUnit FromSnapshot(PlayerId ownerPlayerId, CombatUnitSnapshot snapshot)
        {
            var behaviors = snapshot.Behaviors
                .Select(behavior =>
                    snapshot.Definition?.Behaviors.FirstOrDefault(candidate => candidate.Id == behavior.Id)
                    ?? new BehaviorDefinition(behavior.Id, behavior.Id.ToString(), behavior.Handler))
                .ToArray();
            var definition = snapshot.Definition
                ?? new UnitDefinition(
                    snapshot.UnitId,
                    snapshot.UnitId.ToString(),
                    snapshot.Tier,
                    snapshot.Attack,
                    snapshot.Health,
                    behaviors);

            return new CombatRuntimeUnit(
                snapshot.InstanceId,
                ownerPlayerId,
                definition,
                snapshot.Attack,
                snapshot.Health,
                behaviors);
        }

        public static CombatRuntimeUnit FromDefinition(
            UnitInstanceId instanceId,
            PlayerId ownerPlayerId,
            UnitDefinition definition) =>
            new(
                instanceId,
                ownerPlayerId,
                definition,
                definition.BaseAttack,
                definition.BaseHealth,
                definition.Behaviors);

        public bool Has(NativeBehaviorKey handler) => _behaviors.Any(behavior => behavior.Handler == handler);

        public BehaviorDefinition? FindBehavior(NativeBehaviorKey handler) =>
            _behaviors.FirstOrDefault(behavior => behavior.Handler == handler);

        public BehaviorDefinition? FindBehavior(BehaviorId behaviorId) =>
            _behaviors.FirstOrDefault(behavior => behavior.Id == behaviorId);

        public bool RemoveBehavior(NativeBehaviorKey handler)
        {
            var index = _behaviors.FindIndex(behavior => behavior.Handler == handler);
            if (index < 0) return false;
            _behaviors.RemoveAt(index);
            return true;
        }

        public bool RemoveBehavior(BehaviorId behaviorId)
        {
            var index = _behaviors.FindIndex(behavior => behavior.Id == behaviorId);
            if (index < 0) return false;
            _behaviors.RemoveAt(index);
            return true;
        }

        public bool AddBehavior(BehaviorDefinition behavior)
        {
            if (_behaviors.Any(existing => existing.Id == behavior.Id || existing.Handler == behavior.Handler))
            {
                return false;
            }
            _behaviors.Add(behavior);
            return true;
        }

        public void ModifyStats(int attackDelta, int healthDelta)
        {
            Attack = Math.Max(0, Attack + attackDelta);
            Health += healthDelta;
        }

        public void TakeDamage(int amount) => Health -= amount;

        public void Destroy() => Health = Math.Min(Health, 0);

        public void ResetForReborn()
        {
            Attack = _initialAttack;
            Health = 1;
            _behaviors.Clear();
            _behaviors.AddRange(
                _initialBehaviors.Where(behavior => behavior.Handler != NativeBehaviorKeys.ReviveOnce));
        }

        public CombatUnitSnapshot Snapshot() =>
            new(
                InstanceId,
                Definition.Id,
                Definition.Tier,
                Attack,
                Health,
                _behaviors.Select(behavior => new CombatBehaviorSnapshot(behavior.Id, behavior.Handler)),
                Definition);
    }

    private sealed class CombatPowerRuntimeUnit : IEffectRuntimeUnit
    {
        public UnitInstanceId InstanceId { get; }
        public PlayerId OwnerPlayerId { get; }
        public PowerId PowerId { get; }
        public UnitDefinition Definition { get; }
        public EffectSourceKey SourceKey => EffectSourceKey.ForPower(PowerId);
        public bool IsAlive => false;

        public CombatPowerRuntimeUnit(
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

    private readonly record struct DamageResult(int DamageDealt, bool BarrierLost, bool LethalTriggered);
}
