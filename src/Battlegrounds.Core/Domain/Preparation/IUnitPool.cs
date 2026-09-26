using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Preparation;

public interface IUnitPool
{
    IReadOnlyList<UnitDefinition> DrawOffer(int maximumTier, int count, IRandomSource randomSource);

    IReadOnlyList<UnitDefinition> ExchangeOffer(
        IReadOnlyCollection<UnitDefinition> returnedUnits,
        int maximumTier,
        int count,
        IRandomSource randomSource);

    void ReturnUnit(UnitDefinition definition);

    int GetAvailableCopies(UnitId unitId);
}
