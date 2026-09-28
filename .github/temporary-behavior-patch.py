from pathlib import Path
import json


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one replacement, found {count}")
    p.write_text(text.replace(old, new, 1))

# Effect definition carries the same lifetime vocabulary as named stat modifiers.
replace_once(
    'src/Battlegrounds.Core/Domain/Effects/EffectDefinitions.cs',
    'using Battlegrounds.Core.Domain.Ids;\n',
    'using Battlegrounds.Core.Domain.Ids;\nusing Battlegrounds.Core.Domain.Units;\n')
replace_once(
    'src/Battlegrounds.Core/Domain/Effects/EffectDefinitions.cs',
    '''    public BehaviorId BehaviorId { get; }\n\n    public AddBehaviorEffectDefinition(EffectTargetSelector target, BehaviorId behaviorId)\n    {\n        Target = target ?? throw new ArgumentNullException(nameof(target));\n        BehaviorId = behaviorId;\n    }''',
    '''    public BehaviorId BehaviorId { get; }\n    public UnitModifierDuration Duration { get; }\n\n    public AddBehaviorEffectDefinition(\n        EffectTargetSelector target,\n        BehaviorId behaviorId,\n        UnitModifierDuration duration = UnitModifierDuration.Persistent)\n    {\n        Target = target ?? throw new ArgumentNullException(nameof(target));\n        BehaviorId = behaviorId;\n        Duration = duration;\n    }''')

# Runtime passes duration in Preparation; Combat is isolated so the duration is observational there.
replace_once(
    'src/Battlegrounds.Core/Domain/Effects/GameEffectRuntime.cs',
    '    bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior);',
    '    bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior, UnitModifierDuration duration);')
replace_once(
    'src/Battlegrounds.Core/Domain/Effects/GameEffectRuntime.cs',
    '                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) _world.AddBehavior(target, behavior);',
    '                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) _world.AddBehavior(target, behavior, addBehavior.Duration);')
replace_once(
    'src/Battlegrounds.Core/Domain/Preparation/PreparationEffectEngine.cs',
    '        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior) => GetUnit(unit).AddBehavior(behavior);',
    '        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior, UnitModifierDuration duration) => GetUnit(unit).AddBehavior(behavior, duration);')
replace_once(
    'src/Battlegrounds.Core/Domain/Combat/CombatEngine.cs',
    '        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior)\n        {',
    '        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior, UnitModifierDuration duration)\n        {')

# UnitInstance tracks only behaviors added at runtime; authored base behaviors never expire.
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '    private readonly ReadOnlyCollection<BehaviorDefinition> _behaviorsView;\n    private readonly List<UnitModifierState> _modifiers = [];',
    '    private readonly ReadOnlyCollection<BehaviorDefinition> _behaviorsView;\n    private readonly Dictionary<BehaviorId, UnitModifierDuration> _addedBehaviorDurations = [];\n    private readonly List<UnitModifierState> _modifiers = [];')
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '''        _behaviors.Clear();\n        _behaviors.AddRange(definition.Behaviors);\n        _modifiers.Clear();''',
    '''        _behaviors.Clear();\n        _behaviors.AddRange(definition.Behaviors);\n        _addedBehaviorDurations.Clear();\n        _modifiers.Clear();''')
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '''        _behaviors.Clear();\n        _behaviors.AddRange(source.Behaviors);\n        _modifiers.Clear();''',
    '''        _behaviors.Clear();\n        _behaviors.AddRange(source.Behaviors);\n        _addedBehaviorDurations.Clear();\n        foreach (var pair in source._addedBehaviorDurations) _addedBehaviorDurations.Add(pair.Key, pair.Value);\n        _modifiers.Clear();''')
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '''        _behaviors.Clear();\n        _behaviors.AddRange(\n            Definition.Behaviors.Where(behavior => behavior.Handler != NativeBehaviorKeys.ReviveOnce));\n        _modifiers.Clear();''',
    '''        _behaviors.Clear();\n        _behaviors.AddRange(\n            Definition.Behaviors.Where(behavior => behavior.Handler != NativeBehaviorKeys.ReviveOnce));\n        _addedBehaviorDurations.Clear();\n        _modifiers.Clear();''')
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '''    internal bool AddBehavior(BehaviorDefinition behavior)\n    {\n        ArgumentNullException.ThrowIfNull(behavior);\n        if (_behaviors.Any(existing => existing.Id == behavior.Id || existing.Handler == behavior.Handler)) return false;\n        _behaviors.Add(behavior);\n        return true;\n    }''',
    '''    internal bool AddBehavior(BehaviorDefinition behavior, UnitModifierDuration duration = UnitModifierDuration.Persistent)\n    {\n        ArgumentNullException.ThrowIfNull(behavior);\n        if (_behaviors.Any(existing => existing.Id == behavior.Id || existing.Handler == behavior.Handler)) return false;\n        _behaviors.Add(behavior);\n        _addedBehaviorDurations[behavior.Id] = duration;\n        return true;\n    }\n\n    internal void ExpireBehaviors(UnitModifierDuration duration)\n    {\n        var ids = _addedBehaviorDurations\n            .Where(pair => pair.Value == duration)\n            .Select(pair => pair.Key)\n            .ToArray();\n        foreach (var id in ids) RemoveBehavior(id);\n    }''')
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '''        var modifier = _modifiers[index];\n        _modifiers.RemoveAt(index);''',
    '''        var modifier = _modifiers[index];\n        _modifiers.RemoveAt(index);''')
# remove-behavior paths also clear lifetime bookkeeping.
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '''        if (index < 0) return false;\n        _behaviors.RemoveAt(index);\n        return true;\n    }\n\n    internal bool RemoveBehavior(NativeBehaviorKey handler)''',
    '''        if (index < 0) return false;\n        var behavior = _behaviors[index];\n        _behaviors.RemoveAt(index);\n        _addedBehaviorDurations.Remove(behavior.Id);\n        return true;\n    }\n\n    internal bool RemoveBehavior(NativeBehaviorKey handler)''')
replace_once(
    'src/Battlegrounds.Core/Domain/Units/UnitInstance.cs',
    '''        if (index < 0) return false;\n        _behaviors.RemoveAt(index);\n        return true;\n    }\n}''',
    '''        if (index < 0) return false;\n        var behavior = _behaviors[index];\n        _behaviors.RemoveAt(index);\n        _addedBehaviorDurations.Remove(behavior.Id);\n        return true;\n    }\n}''')

# Settlement consumes temporary behaviors only for players who actually fought.
replace_once(
    'src/Battlegrounds.Core/Domain/Players/PlayerState.cs',
    '''    internal void ExpireUnitModifiers(UnitModifierDuration duration)\n    {\n        foreach (var unit in _reserve.Concat(_field)) unit.ExpireModifiers(duration);\n    }''',
    '''    internal void ExpireUnitModifiers(UnitModifierDuration duration)\n    {\n        foreach (var unit in _reserve.Concat(_field)) unit.ExpireModifiers(duration);\n    }\n\n    internal void ExpireUnitBehaviors(UnitModifierDuration duration)\n    {\n        foreach (var unit in _reserve.Concat(_field)) unit.ExpireBehaviors(duration);\n    }''')
replace_once(
    'src/Battlegrounds.Core/Domain/Match/MatchEngine.cs',
    '''            if (match.TryGetPlayer(playerId, out var player))\n                player.ExpireUnitModifiers(UnitModifierDuration.UntilCombatEnd);''',
    '''            if (match.TryGetPlayer(playerId, out var player))\n            {\n                player.ExpireUnitModifiers(UnitModifierDuration.UntilCombatEnd);\n                player.ExpireUnitBehaviors(UnitModifierDuration.UntilCombatEnd);\n            }''')

# Loader/schema.
replace_once(
    'src/Battlegrounds.Content/ModLoader.cs',
    '''            "addBehavior" => new AddBehaviorEffectDefinition(BuildTarget(data.Target), new BehaviorId(data.BehaviorId ?? throw new InvalidDataException("Validated addBehavior effect is missing behaviorId."))),''',
    '''            "addBehavior" => new AddBehaviorEffectDefinition(\n                BuildTarget(data.Target),\n                new BehaviorId(data.BehaviorId ?? throw new InvalidDataException("Validated addBehavior effect is missing behaviorId.")),\n                data.Duration ?? UnitModifierDuration.Persistent),''')
replace_once(
    'src/Battlegrounds.Content/DirectoryModValidator.cs',
    '''            case "addBehavior":\n            case "removeBehavior":\n                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind"], issues);''',
    '''            case "addBehavior":\n                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId", "duration"], ["kind"], issues);\n                ValidateRequiredTarget(file, effect, path, typeIds, tagIds, issues);\n                if (RequireParameterString(file, effect, "behaviorId", path, issues, out var addBehaviorId) && !behaviorIds.Contains(addBehaviorId!))\n                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".behaviorId", $"Unknown behavior '{addBehaviorId}'."));\n                break;\n            case "removeBehavior":\n                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind"], issues);''')
replace_once(
    'src/Battlegrounds.Content/PowerLifecycleModValidator.cs',
    '''            case "addBehavior":\n            case "removeBehavior":\n                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind", "target", "behaviorId"], issues);''',
    '''            case "addBehavior":\n                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId", "duration"], ["kind", "target", "behaviorId"], issues);\n                ValidateTarget(file, effect, path, triggerEvent, typeIds, tagIds, issues);\n                if (TryRequiredString(effect, "behaviorId", file, path + ".behaviorId", issues, out var addedBehaviorId) &&\n                    !behaviorIds.Contains(addedBehaviorId!))\n                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".behaviorId", $"Unknown behavior '{addedBehaviorId}'."));\n                break;\n            case "removeBehavior":\n                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind", "target", "behaviorId"], issues);''')

# Match regressions reuse the existing modifier lifecycle tests.
test_path = Path('tests/Battlegrounds.Core.Tests/Match/MatchEngineTests.cs')
text = test_path.read_text()
text = text.replace(
    '        unit.ApplyModifier("next-combat", 4, 3, UnitModifierDuration.UntilCombatEnd);\n        Assert.Equal(6, unit.Attack);',
    '        unit.ApplyModifier("next-combat", 4, 3, UnitModifierDuration.UntilCombatEnd);\n        var temporaryBehavior = new BehaviorDefinition(new BehaviorId("temporary-ward"), "Temporary Ward", NativeBehaviorKeys.DamageBarrier);\n        Assert.True(unit.AddBehavior(temporaryBehavior, UnitModifierDuration.UntilCombatEnd));\n        Assert.Equal(6, unit.Attack);', 1)
text = text.replace(
    '        Assert.DoesNotContain(unit.Modifiers, modifier => modifier.Key == "next-combat");',
    '        Assert.DoesNotContain(unit.Modifiers, modifier => modifier.Key == "next-combat");\n        Assert.DoesNotContain(unit.Behaviors, behavior => behavior.Id == temporaryBehavior.Id);', 1)
text = text.replace(
    '        byeUnit.ApplyModifier("next-combat", 3, 0, UnitModifierDuration.UntilCombatEnd);\n        ReadyActive(engine, match);',
    '        byeUnit.ApplyModifier("next-combat", 3, 0, UnitModifierDuration.UntilCombatEnd);\n        var temporaryBehavior = new BehaviorDefinition(new BehaviorId("temporary-guard"), "Temporary Guard", NativeBehaviorKeys.TargetPriority);\n        Assert.True(byeUnit.AddBehavior(temporaryBehavior, UnitModifierDuration.UntilCombatEnd));\n        ReadyActive(engine, match);', 1)
text = text.replace(
    '        Assert.Contains(byeUnit.Modifiers, modifier => modifier.Key == "next-combat");',
    '        Assert.Contains(byeUnit.Modifiers, modifier => modifier.Key == "next-combat");\n        Assert.Contains(byeUnit.Behaviors, behavior => behavior.Id == temporaryBehavior.Id);', 1)
test_path.write_text(text)

# Warbands gameplay: two tactical Actions and a distinct Paladin Hero power.
def write_json(path, data):
    Path(path).write_text(json.dumps(data, indent=2) + '\n')
write_json('mods/warbands/content/actions/battle-ward.json', {
    'id': 'battle-ward', 'name': 'Battle Ward', 'tier': 2, 'cost': 1,
    'effects': [{'kind': 'addBehavior', 'target': {'scope': 'selected'}, 'behaviorId': 'warded', 'duration': 'untilCombatEnd'}]
})
write_json('mods/warbands/content/actions/battle-fury.json', {
    'id': 'battle-fury', 'name': 'Battle Fury', 'tier': 3, 'cost': 2,
    'effects': [{'kind': 'addBehavior', 'target': {'scope': 'selected'}, 'behaviorId': 'double-strike', 'duration': 'untilCombatEnd'}]
})
write_json('mods/warbands/content/powers/holy-aegis.json', {
    'id': 'holy-aegis', 'name': 'Holy Aegis',
    'activation': {'cost': 1, 'maxUsesPerTurn': 1},
    'triggers': [{'event': 'onActivate', 'effects': [{'kind': 'addBehavior', 'target': {'scope': 'selected'}, 'behaviorId': 'warded', 'duration': 'untilCombatEnd'}]}]
})
leader_path = Path('mods/warbands/content/leaders/lady-liadrin.json')
leader = json.loads(leader_path.read_text())
leader['initialPowerId'] = 'holy-aegis'
write_json(leader_path, leader)
roster = Path('mods/warbands/ROSTER.md')
r = roster.read_text()
needle = 'Distinct current exceptions are '
start = r.index(needle)
end = r.index('. Other Heroes currently use the class baseline mapping:', start)
segment = r[start:end]
if 'Lady Liadrin (`holy-aegis`)' not in segment:
    segment = segment.replace('Genn Greymane (`royal-contract`),', 'Genn Greymane (`royal-contract`), Lady Liadrin (`holy-aegis`),')
    r = r[:start] + segment + r[end:]
roster.write_text(r)
