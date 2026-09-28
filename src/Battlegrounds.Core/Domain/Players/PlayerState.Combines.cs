using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Players;

public sealed partial class PlayerState
{
    internal bool TryGetOwnedUnit(UnitInstanceId instanceId, out UnitInstance unit, out bool isReserve)
    {
        var reserveIndex = _reserve.FindIndex(candidate => candidate.Id == instanceId);
        if (reserveIndex >= 0)
        {
            unit = _reserve[reserveIndex];
            isReserve = true;
            return true;
        }

        var fieldIndex = _field.FindIndex(candidate => candidate.Id == instanceId);
        if (fieldIndex >= 0)
        {
            unit = _field[fieldIndex];
            isReserve = false;
            return true;
        }

        unit = null!;
        isReserve = false;
        return false;
    }

    internal UnitInstance RemoveOwnedUnit(UnitInstanceId instanceId)
    {
        var reserveIndex = _reserve.FindIndex(candidate => candidate.Id == instanceId);
        if (reserveIndex >= 0)
        {
            var reserveUnit = _reserve[reserveIndex];
            _reserve.RemoveAt(reserveIndex);
            return reserveUnit;
        }

        var fieldIndex = _field.FindIndex(candidate => candidate.Id == instanceId);
        if (fieldIndex >= 0)
        {
            var fieldUnit = _field[fieldIndex];
            _field.RemoveAt(fieldIndex);
            RecalculateFieldAuras();
            return fieldUnit;
        }

        throw new InvalidOperationException($"Player does not own unit instance '{instanceId}'.");
    }
}
