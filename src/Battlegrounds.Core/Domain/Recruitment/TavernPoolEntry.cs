using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Recruitment;

public readonly record struct TavernPoolEntry
{
    public CardId CardId { get; }
    public int Copies { get; }

    public TavernPoolEntry(CardId cardId, int copies)
    {
        if (copies <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(copies), "Pool copies must be positive.");
        }

        CardId = cardId;
        Copies = copies;
    }
}
