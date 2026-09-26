namespace Battlegrounds.Core.Domain.Behaviors;

public readonly record struct NativeBehaviorKey
{
    public string Value { get; }

    public NativeBehaviorKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Native behavior key cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}

public static class NativeBehaviorKeys
{
    public static NativeBehaviorKey DamageBarrier { get; } = new("damageBarrier");
    public static NativeBehaviorKey TargetPriority { get; } = new("targetPriority");
    public static NativeBehaviorKey ReviveOnce { get; } = new("reviveOnce");
    public static NativeBehaviorKey LethalFirstDamagePerCombat { get; } = new("lethalFirstDamagePerCombat");
    public static NativeBehaviorKey ExtraAttack { get; } = new("extraAttack");

    private static readonly HashSet<NativeBehaviorKey> Supported =
    [
        DamageBarrier,
        TargetPriority,
        ReviveOnce,
        LethalFirstDamagePerCombat,
        ExtraAttack,
    ];

    public static bool IsSupported(NativeBehaviorKey key) => Supported.Contains(key);
}
