namespace Battlegrounds.Core.Domain.Combat;

public enum StartingSidePolicy
{
    LargerFieldThenRandom,
    Random,
}

public sealed class CombatRules
{
    public int MaximumAttacks { get; }
    public StartingSidePolicy StartingSidePolicy { get; }

    public CombatRules(int maximumAttacks, StartingSidePolicy startingSidePolicy)
    {
        if (maximumAttacks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttacks));
        }

        MaximumAttacks = maximumAttacks;
        StartingSidePolicy = startingSidePolicy;
    }
}
