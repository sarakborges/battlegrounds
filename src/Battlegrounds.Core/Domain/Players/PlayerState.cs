using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Players;

public sealed class PlayerState
{
    private readonly List<UnitInstance> _reserve = [];
    private readonly List<UnitInstance> _field = [];
    private readonly List<UnitDefinition> _offer = [];
    private readonly ReadOnlyCollection<UnitInstance> _reserveView;
    private readonly ReadOnlyCollection<UnitInstance> _fieldView;
    private readonly ReadOnlyCollection<UnitDefinition> _offerView;

    public PlayerId Id { get; }
    public int Resource { get; private set; }
    public int Tier { get; private set; } = 1;
    public int? UpgradeCost { get; private set; }
    public bool IsReadyForCombat { get; private set; }
    public bool IsOfferFrozen { get; private set; }
    public IReadOnlyList<UnitInstance> Reserve => _reserveView;
    public IReadOnlyList<UnitInstance> Field => _fieldView;
    public IReadOnlyList<UnitDefinition> Offer => _offerView;

    internal PlayerState(PlayerId id)
    {
        Id = id;
        _reserveView = _reserve.AsReadOnly();
        _fieldView = _field.AsReadOnly();
        _offerView = _offer.AsReadOnly();
    }

    internal void BeginPreparation(int round, PreparationRules rules)
    {
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
        return unit;
    }

    internal void AddToField(UnitInstance unit) => _field.Add(unit);

    internal void InsertIntoField(int fieldSlot, UnitInstance unit)
    {
        if (fieldSlot < 0 || fieldSlot > _field.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldSlot));
        }

        _field.Insert(fieldSlot, unit);
    }

    internal int IndexOfFieldUnit(UnitInstanceId instanceId) =>
        _field.FindIndex(unit => unit.Id == instanceId);

    internal UnitInstance RemoveFromField(int fieldSlot)
    {
        var unit = _field[fieldSlot];
        _field.RemoveAt(fieldSlot);
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

    internal void MarkReadyForCombat() => IsReadyForCombat = true;
}
