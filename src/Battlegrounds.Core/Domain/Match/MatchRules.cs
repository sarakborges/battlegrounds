namespace Battlegrounds.Core.Domain.Match;

public sealed class MatchRules
{
    public int MinimumPlayers { get; }
    public int MaximumPlayers { get; }

    public MatchRules(int minimumPlayers, int maximumPlayers)
    {
        if (minimumPlayers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumPlayers));
        }

        if (maximumPlayers < minimumPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPlayers));
        }

        MinimumPlayers = minimumPlayers;
        MaximumPlayers = maximumPlayers;
    }
}
