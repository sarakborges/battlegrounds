from pathlib import Path
import json


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text()
    if old not in text:
        raise SystemExit(f"anchor not found in {path}: {old[:120]!r}")
    p.write_text(text.replace(old, new, 1))

# Core aura contract: friendly or enemy, signed attack, no enemy health/behaviors yet.
replace_once(
    "src/Battlegrounds.Core/Domain/Units/UnitAuraDefinition.cs",
    '''        if (target.Scope != EffectTargetScope.Friendly)\n            throw new ArgumentException("Unit auras currently support friendly targets only.", nameof(target));\n        if (target.RelativeTo != EffectTargetAnchor.Source)\n            throw new ArgumentException("Unit auras are always source-relative.", nameof(target));\n        if (target.Limit is not null)\n            throw new ArgumentException("Unit auras do not support target limits.", nameof(target));\n        if (target.Selection is not (EffectTargetSelection.All or EffectTargetSelection.Adjacent or EffectTargetSelection.LeftAdjacent or EffectTargetSelection.RightAdjacent))\n            throw new ArgumentException("Unit auras support all or source-relative adjacent selections only.", nameof(target));\n        if (attackDelta < 0 || healthDelta < 0)\n            throw new ArgumentOutOfRangeException(nameof(attackDelta), "The current aura surface supports non-negative stat bonuses only.");\n''',
    '''        if (target.Scope is not (EffectTargetScope.Friendly or EffectTargetScope.Enemy))\n            throw new ArgumentException("Unit auras support friendly or enemy targets only.", nameof(target));\n        if (target.RelativeTo != EffectTargetAnchor.Source)\n            throw new ArgumentException("Unit auras are always source-relative.", nameof(target));\n        if (target.Limit is not null)\n            throw new ArgumentException("Unit auras do not support target limits.", nameof(target));\n        if (target.Scope == EffectTargetScope.Friendly &&\n            target.Selection is not (EffectTargetSelection.All or EffectTargetSelection.Adjacent or EffectTargetSelection.LeftAdjacent or EffectTargetSelection.RightAdjacent))\n            throw new ArgumentException("Friendly Unit auras support all or source-relative adjacent selections only.", nameof(target));\n        if (target.Scope == EffectTargetScope.Enemy && target.Selection != EffectTargetSelection.All)\n            throw new ArgumentException("Enemy Unit auras currently support all targets only.", nameof(target));\n        if (healthDelta < 0)\n            throw new ArgumentOutOfRangeException(nameof(healthDelta), "Aura Health contribution cannot be negative.");\n        if (target.Scope == EffectTargetScope.Enemy && healthDelta != 0)\n            throw new ArgumentException("Enemy Unit auras currently support Attack contribution only.", nameof(healthDelta));\n''')
replace_once(
    "src/Battlegrounds.Core/Domain/Units/UnitAuraDefinition.cs",
    '''        if (behaviors.Any(behavior =>\n            behavior.Handler != NativeBehaviorKeys.TargetPriority &&\n            behavior.Handler != NativeBehaviorKeys.ExtraAttack))\n            throw new ArgumentException("Behavior auras currently support targetPriority and extraAttack only.", nameof(grantedBehaviors));\n''',
    '''        if (behaviors.Any(behavior =>\n            behavior.Handler != NativeBehaviorKeys.TargetPriority &&\n            behavior.Handler != NativeBehaviorKeys.ExtraAttack))\n            throw new ArgumentException("Behavior auras currently support targetPriority and extraAttack only.", nameof(grantedBehaviors));\n        if (target.Scope == EffectTargetScope.Enemy && behaviors.Length > 0)\n            throw new ArgumentException("Enemy Unit auras cannot grant behaviors in the current aura surface.", nameof(grantedBehaviors));\n''')

# Signed Attack aura contribution, while Health remains non-negative.
replace_once(
    "src/Battlegrounds.Core/Domain/Units/UnitInstance.cs",
    '''    internal void SetAuraContribution(int attackDelta, int healthDelta)\n    {\n        if (attackDelta < 0 || healthDelta < 0)\n            throw new ArgumentOutOfRangeException(nameof(attackDelta));\n        _auraAttack = attackDelta;\n        _auraHealth = healthDelta;\n    }\n''',
    '''    internal void SetAuraContribution(int attackDelta, int healthDelta)\n    {\n        if (healthDelta < 0)\n            throw new ArgumentOutOfRangeException(nameof(healthDelta));\n        _auraAttack = attackDelta;\n        _auraHealth = healthDelta;\n    }\n''')

# Preparation sees friendly auras only; enemy auras are pairing-relative and therefore combat-only.
replace_once(
    "src/Battlegrounds.Core/Domain/Players/PlayerState.cs",
    '''  foreach (var aura in source.Definition.Auras)\n  {\n      foreach (var target in ResolveAuraTargets(sourceIndex, aura))\n''',
    '''  foreach (var aura in source.Definition.Auras)\n  {\n      if (aura.Target.Scope != EffectTargetScope.Friendly) continue;\n      foreach (var target in ResolveAuraTargets(sourceIndex, aura))\n''')
replace_once(
    "src/Battlegrounds.Core/Domain/Players/PlayerState.cs",
    '''  unit.SetAuraContribution(\n      (int)Math.Min(int.MaxValue, total.Attack),\n      (int)Math.Min(int.MaxValue, total.Health));\n''',
    '''  unit.SetAuraContribution(\n      (int)Math.Clamp(total.Attack, int.MinValue, int.MaxValue),\n      (int)Math.Clamp(total.Health, 0L, int.MaxValue));\n''')

# Combat: aura recomputation must see both sides, not one SideState in isolation.
combat = Path("src/Battlegrounds.Core/Domain/Combat/CombatEngine.cs")
text = combat.read_text()
text = text.replace("            RecalculateAuras();\n", "", 3)  # SideState ctor + InsertAt + RemoveAt
old_block = '''        private void RecalculateAuras()\n        {\n            foreach (var unit in _units)\n            {\n                unit.SetAuraContribution(0, 0);\n                unit.SetAuraBehaviors([]);\n            }\n            var totals = _units.ToDictionary(unit => unit.InstanceId, _ => (Attack: 0L, Health: 0L));\n            var behaviorTotals = _units.ToDictionary(unit => unit.InstanceId, _ => new List<BehaviorDefinition>());\n            for (var sourceIndex = 0; sourceIndex < _units.Count; sourceIndex++)\n            {\n                var source = _units[sourceIndex];\n                if (!source.IsAlive) continue;\n                foreach (var aura in source.Definition.Auras)\n                {\n                    foreach (var target in ResolveAuraTargets(sourceIndex, aura))\n                    {\n                        var current = totals[target.InstanceId];\n                        totals[target.InstanceId] = (current.Attack + aura.AttackDelta, current.Health + aura.HealthDelta);\n                        behaviorTotals[target.InstanceId].AddRange(aura.GrantedBehaviors);\n                    }\n                }\n            }\n            foreach (var unit in _units)\n            {\n                var total = totals[unit.InstanceId];\n                unit.SetAuraContribution((int)Math.Min(int.MaxValue, total.Attack), (int)Math.Min(int.MaxValue, total.Health));\n                unit.SetAuraBehaviors(behaviorTotals[unit.InstanceId]);\n            }\n        }\n\n        private IEnumerable<CombatRuntimeUnit> ResolveAuraTargets(int sourceIndex, UnitAuraDefinition aura)\n        {\n            IEnumerable<CombatRuntimeUnit> candidates = aura.Target.Selection switch\n            {\n                EffectTargetSelection.All => _units,\n                EffectTargetSelection.Adjacent => AdjacentUnits(sourceIndex),\n                EffectTargetSelection.LeftAdjacent => sourceIndex > 0 ? [_units[sourceIndex - 1]] : [],\n                EffectTargetSelection.RightAdjacent => sourceIndex + 1 < _units.Count ? [_units[sourceIndex + 1]] : [],\n                _ => [],\n            };\n            var source = _units[sourceIndex];\n            return candidates.Where(target =>\n                (!aura.Target.ExcludeSource || target.InstanceId != source.InstanceId) &&\n                (aura.Target.RequiredTypeId is null || target.Definition.Types.Any(type => type.Id == aura.Target.RequiredTypeId.Value)) &&\n                (aura.Target.RequiredTagId is null || target.Definition.Tags.Any(tag => tag.Id == aura.Target.RequiredTagId.Value)));\n        }\n\n        private IEnumerable<CombatRuntimeUnit> AdjacentUnits(int sourceIndex)\n        {\n            if (sourceIndex > 0) yield return _units[sourceIndex - 1];\n            if (sourceIndex + 1 < _units.Count) yield return _units[sourceIndex + 1];\n        }\n\n'''
if old_block not in text:
    raise SystemExit("combat SideState aura block anchor not found")
text = text.replace(old_block, "", 1)

ctor_anchor = '''            _histories = new Dictionary<PlayerId, CombatEffectHistoryState>\n            {\n                [input.Left.PlayerId] = new CombatEffectHistoryState(input.Left.History),\n                [input.Right.PlayerId] = new CombatEffectHistoryState(input.Right.History),\n            };\n            var ids = input.Left.Units.Concat(input.Right.Units).Select(unit => unit.InstanceId.Value).ToArray();\n'''
ctor_repl = '''            _histories = new Dictionary<PlayerId, CombatEffectHistoryState>\n            {\n                [input.Left.PlayerId] = new CombatEffectHistoryState(input.Left.History),\n                [input.Right.PlayerId] = new CombatEffectHistoryState(input.Right.History),\n            };\n            RecalculateAuras();\n            var ids = input.Left.Units.Concat(input.Right.Units).Select(unit => unit.InstanceId.Value).ToArray();\n'''
if ctor_anchor not in text:
    raise SystemExit("combat world ctor anchor not found")
text = text.replace(ctor_anchor, ctor_repl, 1)

# Recompute before summon/revive snapshots, and after each death removal.
text = text.replace(
    '''                side.InsertAt(insertedAt, unit);\n                insertionIndex++;\n''',
    '''                side.InsertAt(insertedAt, unit);\n                RecalculateAuras();\n                insertionIndex++;\n''', 1)
text = text.replace(
    '''            unit.ResetForReborn();\n            side.InsertAt(insertionIndex, unit);\n            unit.RebirthCount++;\n''',
    '''            unit.ResetForReborn();\n            side.InsertAt(insertionIndex, unit);\n            RecalculateAuras();\n            unit.RebirthCount++;\n''', 1)
text = text.replace(
    '''                side.RemoveAt(index);\n                unit.DeathCount++;\n''',
    '''                side.RemoveAt(index);\n                RecalculateAuras();\n                unit.DeathCount++;\n''', 1)

# Add global aura recomputation before ResolveSummonIndex.
anchor = '''        private int ResolveSummonIndex(IEffectRuntimeUnit source, SideState side)\n        {\n'''
method = '''        private void RecalculateAuras()\n        {\n            var units = Left.Units.Concat(Right.Units).ToArray();\n            foreach (var unit in units)\n            {\n                unit.SetAuraContribution(0, 0);\n                unit.SetAuraBehaviors([]);\n            }\n\n            var totals = units.ToDictionary(unit => unit.InstanceId, _ => (Attack: 0L, Health: 0L));\n            var behaviorTotals = units.ToDictionary(unit => unit.InstanceId, _ => new List<BehaviorDefinition>());\n            AccumulateAuras(Left, Right, totals, behaviorTotals);\n            AccumulateAuras(Right, Left, totals, behaviorTotals);\n\n            foreach (var unit in units)\n            {\n                var total = totals[unit.InstanceId];\n                unit.SetAuraContribution(\n                    (int)Math.Clamp(total.Attack, int.MinValue, int.MaxValue),\n                    (int)Math.Clamp(total.Health, 0L, int.MaxValue));\n                unit.SetAuraBehaviors(behaviorTotals[unit.InstanceId]);\n            }\n        }\n\n        private static void AccumulateAuras(\n            SideState sourceSide,\n            SideState enemySide,\n            Dictionary<UnitInstanceId, (long Attack, long Health)> totals,\n            Dictionary<UnitInstanceId, List<BehaviorDefinition>> behaviorTotals)\n        {\n            for (var sourceIndex = 0; sourceIndex < sourceSide.UnitCount; sourceIndex++)\n            {\n                var source = sourceSide.Units[sourceIndex];\n                if (!source.IsAlive) continue;\n                foreach (var aura in source.Definition.Auras)\n                {\n                    foreach (var target in ResolveAuraTargets(sourceSide, enemySide, sourceIndex, aura))\n                    {\n                        var current = totals[target.InstanceId];\n                        totals[target.InstanceId] = (current.Attack + aura.AttackDelta, current.Health + aura.HealthDelta);\n                        behaviorTotals[target.InstanceId].AddRange(aura.GrantedBehaviors);\n                    }\n                }\n            }\n        }\n\n        private static IEnumerable<CombatRuntimeUnit> ResolveAuraTargets(\n            SideState sourceSide,\n            SideState enemySide,\n            int sourceIndex,\n            UnitAuraDefinition aura)\n        {\n            var source = sourceSide.Units[sourceIndex];\n            IEnumerable<CombatRuntimeUnit> candidates;\n            if (aura.Target.Scope == EffectTargetScope.Enemy)\n            {\n                candidates = enemySide.Units;\n            }\n            else\n            {\n                candidates = aura.Target.Selection switch\n                {\n                    EffectTargetSelection.All => sourceSide.Units,\n                    EffectTargetSelection.Adjacent => AdjacentUnits(sourceSide, sourceIndex),\n                    EffectTargetSelection.LeftAdjacent => sourceIndex > 0 ? [sourceSide.Units[sourceIndex - 1]] : [],\n                    EffectTargetSelection.RightAdjacent => sourceIndex + 1 < sourceSide.UnitCount ? [sourceSide.Units[sourceIndex + 1]] : [],\n                    _ => [],\n                };\n            }\n\n            return candidates.Where(target =>\n                (!aura.Target.ExcludeSource || target.InstanceId != source.InstanceId) &&\n                (aura.Target.RequiredTypeId is null || target.Definition.Types.Any(type => type.Id == aura.Target.RequiredTypeId.Value)) &&\n                (aura.Target.RequiredTagId is null || target.Definition.Tags.Any(tag => tag.Id == aura.Target.RequiredTagId.Value)));\n        }\n\n        private static IEnumerable<CombatRuntimeUnit> AdjacentUnits(SideState side, int sourceIndex)\n        {\n            if (sourceIndex > 0) yield return side.Units[sourceIndex - 1];\n            if (sourceIndex + 1 < side.UnitCount) yield return side.Units[sourceIndex + 1];\n        }\n\n        private int ResolveSummonIndex(IEffectRuntimeUnit source, SideState side)\n        {\n'''
if anchor not in text:
    raise SystemExit("combat ResolveSummonIndex anchor not found")
text = text.replace(anchor, method, 1)
combat.write_text(text)

# Content validator: signed attack, enemy/all target, no enemy health/behavior yet.
validator = Path("src/Battlegrounds.Content/UnitAuraModValidator.cs")
text = validator.read_text()
text = text.replace(
    '                var attack = ReadNonNegativeInt(aura, "attack", file, basePath, issues);\n',
    '                var attack = ReadInt(aura, "attack", file, basePath, issues);\n', 1)
text = text.replace(
    '                    issues.Add(new("INVALID_VALUE", file, basePath, "Aura requires a positive stat bonus or at least one behaviorId."));\n',
    '                    issues.Add(new("INVALID_VALUE", file, basePath, "Aura requires a non-zero stat contribution or at least one behaviorId."));\n', 1)
text = text.replace(
    '''                ValidateTarget(target, file, basePath + ".target", typeIds, tagIds, issues);\n                index++;\n''',
    '''                var scope = ValidateTarget(target, file, basePath + ".target", typeIds, tagIds, issues);\n                if (scope == "enemy" && health != 0)\n                    issues.Add(new("INVALID_VALUE", file, basePath + ".health", "Enemy auras currently support Attack contribution only."));\n                if (scope == "enemy" && behaviorCount > 0)\n                    issues.Add(new("INVALID_VALUE", file, basePath + ".behaviorIds", "Enemy auras cannot grant behaviors in the current aura surface."));\n                index++;\n''', 1)
text = text.replace(
    '''    private static void ValidateTarget(JsonElement target, string file, string path, IReadOnlySet<string> typeIds, IReadOnlySet<string> tagIds, List<ModValidationIssue> issues)\n    {\n''',
    '''    private static string? ValidateTarget(JsonElement target, string file, string path, IReadOnlySet<string> typeIds, IReadOnlySet<string> tagIds, List<ModValidationIssue> issues)\n    {\n''', 1)
text = text.replace(
    '''        if (!target.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.String || scope.GetString() != "friendly")\n            issues.Add(new("INVALID_VALUE", file, path + ".scope", "Aura target scope must be 'friendly'."));\n        if (target.TryGetProperty("selection", out var selection) && (selection.ValueKind != JsonValueKind.String || !Selections.Contains(selection.GetString()!)))\n            issues.Add(new("INVALID_VALUE", file, path + ".selection", "Aura selection must be all or source-relative adjacent."));\n''',
    '''        string? scopeValue = null;\n        if (!target.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.String || scope.GetString() is not ("friendly" or "enemy"))\n            issues.Add(new("INVALID_VALUE", file, path + ".scope", "Aura target scope must be 'friendly' or 'enemy'."));\n        else\n            scopeValue = scope.GetString();\n\n        if (target.TryGetProperty("selection", out var selection) && (selection.ValueKind != JsonValueKind.String || !Selections.Contains(selection.GetString()!)))\n            issues.Add(new("INVALID_VALUE", file, path + ".selection", "Aura selection must be all or source-relative adjacent."));\n        if (scopeValue == "enemy" && target.TryGetProperty("selection", out selection) && selection.ValueKind == JsonValueKind.String && selection.GetString() != "all")\n            issues.Add(new("INVALID_VALUE", file, path + ".selection", "Enemy auras currently support selection 'all' only."));\n''', 1)
text = text.replace(
    '''        ValidateReference(target, "typeId", typeIds, "unit type", file, path, issues);\n        ValidateReference(target, "tagId", tagIds, "tag", file, path, issues);\n    }\n\n    private static int ReadNonNegativeInt''',
    '''        ValidateReference(target, "typeId", typeIds, "unit type", file, path, issues);\n        ValidateReference(target, "tagId", tagIds, "tag", file, path, issues);\n        return scopeValue;\n    }\n\n    private static int ReadInt(JsonElement aura, string name, string file, string path, List<ModValidationIssue> issues)\n    {\n        if (!aura.TryGetProperty(name, out var value)) return 0;\n        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))\n        {\n            issues.Add(new("INVALID_TYPE", file, path + "." + name, $"{name} must be an integer."));\n            return 0;\n        }\n        return result;\n    }\n\n    private static int ReadNonNegativeInt''', 1)
validator.write_text(text)

# Real Warbands consumer.
p = Path("mods/warbands/content/units/siege-colossus.json")
data = json.loads(p.read_text())
data["auras"] = [{"target": {"scope": "enemy", "selection": "all"}, "attack": -1}]
p.write_text(json.dumps(data, indent=2) + "\n")

# Core regression: enemy aura applies before combat and clamps at zero attack.
tests = Path("tests/Battlegrounds.Core.Tests/UnitAuraTests.cs")
text = tests.read_text()
anchor = '''    [Fact]\n    public void PreparationBehaviorAuraIsDerivedAndNotBakedIntoCombatSnapshot()\n'''
new_test = '''    [Fact]\n    public void EnemyAttackAuraAppliesBeforeCombatAndClampsAttackAtZero()\n    {\n        var aura = new UnitAuraDefinition(\n            new EffectTargetSelector(EffectTargetScope.Enemy),\n            attackDelta: -3);\n        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 0, 10, auras: [aura]);\n        var enemy = new UnitDefinition(new UnitId("enemy"), "Enemy", 1, 2, 10);\n        var input = new CombatInput(\n            new CombatParticipant(new PlayerId(0), [Snap(1, source)]),\n            new CombatParticipant(new PlayerId(1), [Snap(2, enemy)]));\n\n        var result = new CombatEngine().Resolve(input, new CombatRules(StartingSidePolicy.Random), new SeededRandomSource(1));\n\n        Assert.True(result.IsDraw);\n        Assert.Empty(result.Attacks);\n    }\n\n    [Fact]\n    public void PreparationBehaviorAuraIsDerivedAndNotBakedIntoCombatSnapshot()\n'''
if anchor not in text:
    raise SystemExit("unit aura test anchor not found")
text = text.replace(anchor, new_test, 1)
tests.write_text(text)

# Content regression: Warbands consumer loads as enemy -1 Attack aura.
p = Path("tests/Battlegrounds.Content.Tests/UnitAuraValidationTests.cs")
text = p.read_text()
old = '''        var moonfang = package.Units.GetRequired(new UnitId("moonfang-alpha"));\n        var aura = Assert.Single(moonfang.Auras);\n        Assert.Contains(aura.GrantedBehaviors, behavior => behavior.Handler == NativeBehaviorKeys.ExtraAttack);\n'''
new = '''        var moonfang = package.Units.GetRequired(new UnitId("moonfang-alpha"));\n        var aura = Assert.Single(moonfang.Auras);\n        Assert.Contains(aura.GrantedBehaviors, behavior => behavior.Handler == NativeBehaviorKeys.ExtraAttack);\n\n        var siegeColossus = package.Units.GetRequired(new UnitId("siege-colossus"));\n        var enemyAura = Assert.Single(siegeColossus.Auras);\n        Assert.Equal(EffectTargetScope.Enemy, enemyAura.Target.Scope);\n        Assert.Equal(-1, enemyAura.AttackDelta);\n'''
if old not in text:
    raise SystemExit("content aura test anchor not found")
text = text.replace(old, new, 1)
if "using Battlegrounds.Core.Domain.Effects;" not in text:
    text = text.replace("using Battlegrounds.Core.Domain.Behaviors;\n", "using Battlegrounds.Core.Domain.Behaviors;\nusing Battlegrounds.Core.Domain.Effects;\n", 1)
p.write_text(text)
