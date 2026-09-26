namespace Battlegrounds.Core.Domain.Effects;

public readonly record struct NativeTriggerKey
{
    public string Value { get; }

    public NativeTriggerKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Trigger key cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}

public static class NativeTriggerKeys
{
    public static NativeTriggerKey OnPlay { get; } = new("onPlay");
    public static NativeTriggerKey OnDeath { get; } = new("onDeath");
    public static NativeTriggerKey AfterFriendlyDeaths { get; } = new("afterFriendlyDeaths");
    public static NativeTriggerKey OnSummon { get; } = new("onSummon");
    public static NativeTriggerKey OnAttack { get; } = new("onAttack");
    public static NativeTriggerKey OnDamage { get; } = new("onDamage");
    public static NativeTriggerKey OnCombatStart { get; } = new("onCombatStart");
    public static NativeTriggerKey OnCombatEnd { get; } = new("onCombatEnd");
    public static NativeTriggerKey OnTurnStart { get; } = new("onTurnStart");
    public static NativeTriggerKey OnTurnEnd { get; } = new("onTurnEnd");

    private static readonly HashSet<NativeTriggerKey> Supported =
    [
        OnPlay, OnDeath, AfterFriendlyDeaths, OnSummon, OnAttack, OnDamage,
        OnCombatStart, OnCombatEnd, OnTurnStart, OnTurnEnd,
    ];

    public static bool IsSupported(NativeTriggerKey key) => Supported.Contains(key);
}

public readonly record struct NativeEffectKey
{
    public string Value { get; }

    public NativeEffectKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Effect key cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
}

public static class NativeEffectKeys
{
    public static NativeEffectKey ModifyStats { get; } = new("modifyStats");
    public static NativeEffectKey DealDamage { get; } = new("dealDamage");
    public static NativeEffectKey DestroyUnit { get; } = new("destroyUnit");
    public static NativeEffectKey TriggerEvent { get; } = new("triggerEvent");
    public static NativeEffectKey SummonUnit { get; } = new("summonUnit");
    public static NativeEffectKey AddBehavior { get; } = new("addBehavior");
    public static NativeEffectKey RemoveBehavior { get; } = new("removeBehavior");
    public static NativeEffectKey AddResource { get; } = new("addResource");
    public static NativeEffectKey SetPower { get; } = new("setPower");

    private static readonly HashSet<NativeEffectKey> Supported =
    [
        ModifyStats,
        DealDamage,
        DestroyUnit,
        TriggerEvent,
        SummonUnit,
        AddBehavior,
        RemoveBehavior,
        AddResource,
        SetPower,
    ];

    public static bool IsSupported(NativeEffectKey key) => Supported.Contains(key);
}
