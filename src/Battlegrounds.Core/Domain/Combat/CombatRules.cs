namespace Battlegrounds.Core.Domain.Combat;

public enum StartingSidePolicy
{
    LargerFieldThenRandom,
    Random,
}

public sealed class CombatRules
{
    public StartingSidePolicy StartingSidePolicy { get; }

    public CombatRules(StartingSidePolicy startingSidePolicy)
    {
        StartingSidePolicy = startingSidePolicy;
    }
}
