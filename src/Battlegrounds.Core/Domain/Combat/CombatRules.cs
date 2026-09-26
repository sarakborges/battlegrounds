namespace Battlegrounds.Core.Domain.Combat;

public enum StartingSidePolicy
{
    Random,
    LargerFieldThenRandom,
}

public enum PostCombatDamagePolicy
{
    WinnerTierPlusSurvivorTiers,
}

public sealed class CombatRules
{
    public StartingSidePolicy StartingSidePolicy { get; }
    public PostCombatDamagePolicy PostCombatDamagePolicy { get; }

    public CombatRules(
        StartingSidePolicy startingSidePolicy,
        PostCombatDamagePolicy postCombatDamagePolicy = PostCombatDamagePolicy.WinnerTierPlusSurvivorTiers)
    {
        StartingSidePolicy = startingSidePolicy;
        PostCombatDamagePolicy = postCombatDamagePolicy;
    }
}
