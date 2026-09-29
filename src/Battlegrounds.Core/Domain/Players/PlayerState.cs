using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Players;

public sealed partial class PlayerState
{
    private readonly List<UnitInstance> _reserve = [];
    private readonly List<UnitInstance> _field = [];
    private readonly List<UnitDefinition> _offer = [];
    private readonly ReadOnlyCollection<UnitInstance> _reserveView;
    private readonly ReadOnlyCollection<UnitInstance> _fieldView;
    private readonly ReadOnlyCollection<UnitDefinition> _offerView;

    public PlayerId Id { get; }
    public LeaderState? Leader { get; }
    public int Health { get; private set; }
    public bool IsEliminated => Health <= 0;
    public int Resource { get; private set; }
    public int Tier { get; private set; } = 1;
    public int? UpgradeCost { get; private set; }
    public int NextAcquireDiscount { get; private set; }
    public CombatOutcome? LastCombatOutcome { get; private set; }
    public bool IsReadyForCombat { get; private set; }
    public bool IsOfferFrozen { get; private set; }
    public IReadOnlyList<UnitInstance> Reserve => _reserveView;
    public IReadOnlyList<UnitInstance> Field => _fieldView;
    public IReadOnlyList<UnitDefinition> Offer => _offerView;
    internal EffectHistoryState EffectHistory { get; } = new();

    internal PlayerState(PlayerId id, int startingHealth, LeaderDefinition? leaderDefinition = null)
    {
        if (startingHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startingHealth));
        }

        var initialHealth = startingHealth + (leaderDefinition?.HealthModifier ?? 0);
        if (initialHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaderDefinition),
                "Leader health modifier must leave the player with positive starting Health.");
        }

        Id = id;
        Leader = leaderDefinition is null ? null : new LeaderState(leaderDefinition);
        Health = initialHealth;
        _reserveView = _reserve.AsReadOnly();
        _fieldView = _field.AsReadOnly();
        _offerView = _offer.AsReadOnly();
    }

    internal void BeginPreparation(int round, PreparationRules rules)
    {
        if (IsEliminated)
        {
            throw new InvalidOperationException("Eliminated players cannot begin preparation.");
        }

        EffectHistory.BeginTurn();
        Resource = rules.GetResourceForRound(round);
        IsReadyForCombat = false;

        if (UpgradeCost is null)
        {
            UpgradeCost = rules.GetInitialUpgradeCost(Tier);
        }
        else if (round > 1)
        {
            UpgradeCost = Math.Max(0, UpgradeCost.Value - 1);
        }
    }

    internal PlayerDamageResult TakeDamage(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount == 0 || IsEliminated)
        {
            return new PlayerDamageResult(
                amount,
                ArmorAbsorbed: 0,
                HealthDamage: 0,
                Leader?.Armor ?? 0,
                Health);
        }

        var armorAbsorbed = Leader?.AbsorbDamage(amount) ?? 0;
        var healthDamage = amount - armorAbsorbed;
        Health = Math.Max(0, Health - healthDamage);

        if (IsEliminated)
        {
            IsReadyForCombat = false;
        }

        return new PlayerDamageResult(
            amount,
            armorAbsorbed,
            healthDamage,
            Leader?.Armor ?? 0,
            Health);
    }

    internal IReadOnlyList<UnitDefinition> ReleasePoolClaimsAfterElimination()
    {
        if (!IsEliminated)
            throw new InvalidOperationException("Pool claims may only be released after player elimination.");

        var released = new List<UnitDefinition>(_offer);
        _offer.Clear();
        IsOfferFrozen = false;

        foreach (var unit in _reserve.Concat(_field))
        {
            if (unit.ReleasePoolReturnDefinition() is UnitDefinition definition)
                released.Add(definition);
        }

        return released.ToArray();
    }

    public int GetAcquireCost(PlayableOfferEntry entry, PreparationRules rules)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(rules);
        var baseCost = entry.Action?.Cost ?? rules.AcquireCost;
        return Math.Max(0, baseCost - NextAcquireDiscount);
    }

    internal int GetUnitAcquireCost(PreparationRules rules) =>
        Math.Max(0, rules.AcquireCost - NextAcquireDiscount);

    internal int GetActionAcquireCost(ActionDefinition definition) =>
        Math.Max(0, definition.Cost - NextAcquireDiscount);

    internal void AddAcquireDiscount(int amount)
    {
        if (amount <= 0) return;
        NextAcquireDiscount = (int)Math.Min(int.MaxValue, (long)NextAcquireDiscount + amount);
    }

    internal void ConsumeAcquireDiscount() => NextAcquireDiscount = 0;

    internal bool CanAfford(int amount) => Resource >= amount;

    internal void SpendResource(int amount)
    {
        if (amount < 0 || amount > Resource)
        {
            throw new InvalidOperationException("Resource spend violates player state invariants.");
        }

        Resource -= amount;
    }

    internal void GainResource(int amount, int maximumResource)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Resource = Math.Min(maximumResource, Resource + amount);
    }

    internal void AdjustResource(int amount, int maximumResource)
    {
        var next = (long)Resource + amount;
        Resource = (int)Math.Clamp(next, 0, maximumResource);
    }

    internal UnitDefinition TakeOfferedUnit(int slot)
    {
        var unit = _offer[slot];
        _offer.RemoveAt(slot);
        return unit;
    }

    internal void AddOfferedUnit(UnitDefinition definition) =>
        _offer.Add(definition ?? throw new ArgumentNullException(nameof(definition)));

    internal void ReplaceOfferedUnit(int slot, UnitDefinition definition) =>
        _offer[slot] = definition ?? throw new ArgumentNullException(nameof(definition));

    internal void ReplaceOffer(IEnumerable<UnitDefinition> units)
    {
        _offer.Clear();
        _offer.AddRange(units);
    }

    internal void AddToReserve(UnitInstance unit) => _reserve.Add(unit);

    internal UnitInstance DeployFromReserve(int reserveSlot)
    {
        var unit = _reserve[reserveSlot];
        _reserve.RemoveAt(reserveSlot);
        _field.Add(unit);
        RecalculateFieldAuras();
        return unit;
    }

    internal void AddToField(UnitInstance unit)
    {
        _field.Add(unit);
        RecalculateFieldAuras();
    }

    internal void InsertIntoField(int fieldSlot, UnitInstance unit)
    {
        if (fieldSlot < 0 || fieldSlot > _field.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldSlot));
        }

        _field.Insert(fieldSlot, unit);
        RecalculateFieldAuras();
    }

    internal void ReorderField(IReadOnlyList<UnitInstanceId> orderedUnitIds)
    {
        ArgumentNullException.ThrowIfNull(orderedUnitIds);
        if (orderedUnitIds.Count != _field.Count || orderedUnitIds.Distinct().Count() != _field.Count)
            throw new InvalidOperationException("Field reorder must contain every field Unit exactly once.");

        var byId = _field.ToDictionary(unit => unit.Id);
        if (orderedUnitIds.Any(id => !byId.ContainsKey(id)))
            throw new InvalidOperationException("Field reorder may only contain Units currently on this player's Field.");

        _field.Clear();
        foreach (var id in orderedUnitIds)
            _field.Add(byId[id]);
        RecalculateFieldAuras();
    }

    internal int IndexOfFieldUnit(UnitInstanceId instanceId) =>
        _field.FindIndex(unit => unit.Id == instanceId);

    internal UnitInstance RemoveFromField(int fieldSlot)
    {
        var unit = _field[fieldSlot];
        _field.RemoveAt(fieldSlot);
        RecalculateFieldAuras();
        return unit;
    }

    internal UnitInstance? RemoveFromField(UnitInstanceId instanceId)
    {
        var index = _field.FindIndex(unit => unit.Id == instanceId);
        if (index < 0)
        {
            return null;
        }

        var unit = _field[index];
        _field.RemoveAt(index);
        RecalculateFieldAuras();
        return unit;
    }

    internal bool TryGetFieldUnit(UnitInstanceId instanceId, out UnitInstance unit)
    {
        var found = _field.FirstOrDefault(candidate => candidate.Id == instanceId);
        if (found is null)
        {
            unit = null!;
            return false;
        }

        unit = found;
        return true;
    }

    internal void AdjustUpgradeCost(int amount)
    {
        if (UpgradeCost is null || amount == 0) return;
        var next = (long)UpgradeCost.Value + amount;
        UpgradeCost = (int)Math.Clamp(next, 0, int.MaxValue);
    }

    internal void UpgradeTier(PreparationRules rules)
    {
        if (Tier >= rules.MaximumTier || UpgradeCost is null)
        {
            throw new InvalidOperationException("Cannot upgrade beyond the maximum tier.");
        }

        SpendResource(UpgradeCost.Value);
        Tier++;
        UpgradeCost = rules.GetInitialUpgradeCost(Tier);
    }

    internal void SetOfferFrozen(bool isFrozen)
    {
        if (IsOfferFrozen == isFrozen)
        {
            throw new InvalidOperationException("Offer freeze state must actually change.");
        }

        IsOfferFrozen = isFrozen;
    }

    internal void ClearOfferFrozen() => IsOfferFrozen = false;

    internal void RecordCombatOutcome(CombatOutcome outcome) => LastCombatOutcome = outcome;

    internal void ClearLastCombatOutcome() => LastCombatOutcome = null;

    internal void ExpireUnitModifiers(UnitModifierDuration duration)
    {
        foreach (var unit in _reserve.Concat(_field)) unit.ExpireModifiers(duration);
    }

    internal void RecalculateFieldAuras()
    {
        foreach (var unit in _field) unit.SetAuraContribution(0, 0);
        if (_field.Count == 0) return;

        var totals = _field.ToDictionary(unit => unit.Id, _ => (Attack: 0L, Health: 0L));
        for (var sourceIndex = 0; sourceIndex < _field.Count; sourceIndex++)
        {
  var source = _field[sourceIndex];
  if (!source.IsAlive) continue;
  foreach (var aura in source.Definition.Auras)
  {
      foreach (var target in ResolveAuraTargets(sourceIndex, aura))
      {
          var current = totals[target.Id];
          totals[target.Id] = (current.Attack + aura.AttackDelta, current.Health + aura.HealthDelta);
      }
  }
        }

        foreach (var unit in _field)
        {
  var total = totals[unit.Id];
  unit.SetAuraContribution(
      (int)Math.Min(int.MaxValue, total.Attack),
      (int)Math.Min(int.MaxValue, total.Health));
        }
    }

    private IEnumerable<UnitInstance> ResolveAuraTargets(int sourceIndex, UnitAuraDefinition aura)
    {
        IEnumerable<UnitInstance> candidates = aura.Target.Selection switch
        {
  EffectTargetSelection.All => _field,
  EffectTargetSelection.Adjacent => AdjacentUnits(sourceIndex),
  EffectTargetSelection.LeftAdjacent => sourceIndex > 0 ? [_field[sourceIndex - 1]] : [],
  EffectTargetSelection.RightAdjacent => sourceIndex + 1 < _field.Count ? [_field[sourceIndex + 1]] : [],
  _ => [],
        };
        var source = _field[sourceIndex];
        return candidates.Where(target =>
  (!aura.Target.ExcludeSource || target.Id != source.Id) &&
  (aura.Target.RequiredTypeId is null || target.Definition.Types.Any(type => type.Id == aura.Target.RequiredTypeId.Value)) &&
  (aura.Target.RequiredTagId is null || target.Definition.Tags.Any(tag => tag.Id == aura.Target.RequiredTagId.Value)));
    }

    private IEnumerable<UnitInstance> AdjacentUnits(int sourceIndex)
    {
        if (sourceIndex > 0) yield return _field[sourceIndex - 1];
        if (sourceIndex + 1 < _field.Count) yield return _field[sourceIndex + 1];
    }

    internal void MarkReadyForCombat()
    {
        if (PendingChoice is not null)
            throw new InvalidOperationException("Players with a pending choice cannot be marked ready for combat.");
        IsReadyForCombat = true;
    }
}
