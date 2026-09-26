using Battlegrounds.Core.Domain.Leaders;

namespace Battlegrounds.Core.Domain.Match;

public sealed class MatchRules
{
    public int MinimumPlayers { get; }
    public int MaximumPlayers { get; }
    public int StartingHealth { get; }
    public int LeaderOfferSize { get; }
    public LeaderOfferPolicy LeaderOfferPolicy { get; }

    public MatchRules(
        int minimumPlayers,
        int maximumPlayers,
        int startingHealth = 30,
        int leaderOfferSize = 1,
        LeaderOfferPolicy leaderOfferPolicy = LeaderOfferPolicy.IndependentPerPlayer)
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

        if (leaderOfferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leaderOfferSize));
        }

        MinimumPlayers = minimumPlayers;
        MaximumPlayers = maximumPlayers;
        StartingHealth = startingHealth;
        LeaderOfferSize = leaderOfferSize;
        LeaderOfferPolicy = leaderOfferPolicy;
    }
}
