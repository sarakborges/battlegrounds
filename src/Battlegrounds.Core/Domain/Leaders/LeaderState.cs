using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Powers;

namespace Battlegrounds.Core.Domain.Leaders;

public sealed class LeaderState
{
    private readonly Dictionary<PowerId, int> _usesThisTurn = [];
    private readonly Dictionary<PowerId, int> _usesThisMatch = [];

    public LeaderDefinition Definition { get; }
    public int Armor { get; private set; }
    public PowerId? CurrentPowerId { get; private set; }

    internal LeaderState(LeaderDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Armor = definition.StartingArmor;
        CurrentPowerId = definition.InitialPowerId;
    }

    internal int AbsorbDamage(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        var absorbed = Math.Min(Armor, amount);
        Armor -= absorbed;
        return absorbed;
    }

    internal void BeginTurn() => _usesThisTurn.Clear();

    internal bool CanUse(PowerDefinition power)
    {
        ArgumentNullException.ThrowIfNull(power);
        if (CurrentPowerId != power.Id || power.Activation is null) return false;

        var activation = power.Activation;
        if (_usesThisTurn.GetValueOrDefault(power.Id) >= activation.MaxUsesPerTurn) return false;
        if (activation.MaxUsesPerMatch is not null &&
            _usesThisMatch.GetValueOrDefault(power.Id) >= activation.MaxUsesPerMatch.Value)
        {
            return false;
        }

        return true;
    }

    internal void RecordUse(PowerId powerId)
    {
        _usesThisTurn[powerId] = _usesThisTurn.GetValueOrDefault(powerId) + 1;
        _usesThisMatch[powerId] = _usesThisMatch.GetValueOrDefault(powerId) + 1;
    }

    internal int GetUsesThisTurn(PowerId powerId) => _usesThisTurn.GetValueOrDefault(powerId);
    internal int GetUsesThisMatch(PowerId powerId) => _usesThisMatch.GetValueOrDefault(powerId);

    internal void SetPower(PowerId powerId) => CurrentPowerId = powerId;
}
