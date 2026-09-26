using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Preparation;

public readonly record struct UnitPoolEntry
{
    public UnitId UnitId { get; }
    public int Copies { get; }

    public UnitPoolEntry(UnitId unitId, int copies)
    {
        if (copies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(copies), "Pool copies must be positive.");
        }

        UnitId = unitId;
        Copies = copies;
    }
}
