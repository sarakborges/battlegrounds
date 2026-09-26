namespace Battlegrounds.Core.Domain.Match;

public sealed class MatchRules
{
    public int MinimumPlayers { get; }
    public int MaximumPlayers { get; }
    public int StartingHealth { get; }

    public MatchRules(int minimumPlayers, int maximumPlayers, int startingHealth = 30)
    {
        if (minimumPlayers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumPlayers));
        }

        if (maximumPlayers < minimumPlayers)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPlayers));
        }

        if (startingHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startingHealth));
        }

        MinimumPlayers = minimumPlayers;
        MaximumPlayers = maximumPlayers;
        StartingHealth = startingHealth;
    }
}
