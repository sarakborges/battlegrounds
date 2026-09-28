from pathlib import Path
import json


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected one replacement, found {count}")
    p.write_text(text.replace(old, new, 1))

# Core combine contract.
replace_once(
    'src/Battlegrounds.Core/Domain/Combines/UnitCombineDefinition.cs',
    '''    public UnitId ResultUnitId { get; }\n\n    public UnitCombineDefinition(''',
    '''    public UnitId ResultUnitId { get; }\n    public bool InheritPersistentModifiers { get; }\n\n    public UnitCombineDefinition(''')
replace_once(
    'src/Battlegrounds.Core/Domain/Combines/UnitCombineDefinition.cs',
    '''        UnitId sourceUnitId,\n        int requiredCopies,\n        UnitId resultUnitId)''',
    '''        UnitId sourceUnitId,\n        int requiredCopies,\n        UnitId resultUnitId,\n        bool inheritPersistentModifiers = false)''')
replace_once(
    'src/Battlegrounds.Core/Domain/Combines/UnitCombineDefinition.cs',
    '''        RequiredCopies = requiredCopies;\n        ResultUnitId = resultUnitId;''',
    '''        RequiredCopies = requiredCopies;\n        ResultUnitId = resultUnitId;\n        InheritPersistentModifiers = inheritPersistentModifiers;''')

# Gather named persistent modifiers before inputs disappear, aggregate by key, and apply before onCombine.
replace_once(
    'src/Battlegrounds.Core/Domain/Preparation/PreparationEngine.cs',
    '''        var resultDefinition = _unitCatalog.GetRequired(combine.ResultUnitId);\n        foreach (var value in selected)''',
    '''        var resultDefinition = _unitCatalog.GetRequired(combine.ResultUnitId);\n        var inheritedModifiers = combine.InheritPersistentModifiers\n            ? selected\n                .SelectMany(value => value.Unit.Modifiers)\n                .Where(modifier => modifier.Duration == UnitModifierDuration.Persistent)\n                .GroupBy(modifier => modifier.Key, StringComparer.Ordinal)\n                .Select(group => new\n                {\n                    Key = group.Key,\n                    Attack = group.Sum(modifier => modifier.AttackDelta),\n                    Health = group.Sum(modifier => modifier.HealthDelta),\n                })\n                .Where(modifier => modifier.Attack != 0 || modifier.Health != 0)\n                .OrderBy(modifier => modifier.Key, StringComparer.Ordinal)\n                .ToArray()\n            : [];\n        foreach (var value in selected)''')
replace_once(
    'src/Battlegrounds.Core/Domain/Preparation/PreparationEngine.cs',
    '''        var result = match.CreateUnit(resultDefinition, UnitInstanceOrigin.Generated);\n        player.AddToReserve(result);\n        _effectEngine.ProcessCombinedUnit(match, player, result);''',
    '''        var result = match.CreateUnit(resultDefinition, UnitInstanceOrigin.Generated);\n        foreach (var modifier in inheritedModifiers)\n            result.ApplyModifier(modifier.Key, modifier.Attack, modifier.Health, UnitModifierDuration.Persistent);\n        player.AddToReserve(result);\n        _effectEngine.ProcessCombinedUnit(match, player, result);''')

# Content loading/schema.
replace_once(
    'src/Battlegrounds.Content/ModLoader.cs',
    '''                new UnitId(data.SourceUnitId),\n                data.RequiredCopies,\n                new UnitId(data.ResultUnitId))));''',
    '''                new UnitId(data.SourceUnitId),\n                data.RequiredCopies,\n                new UnitId(data.ResultUnitId),\n                data.InheritPersistentModifiers ?? false)));''')
replace_once(
    'src/Battlegrounds.Content/ModLoader.cs',
    '''    private sealed record UnitCombineData(string Id, string Name, string SourceUnitId, int RequiredCopies, string ResultUnitId);''',
    '''    private sealed record UnitCombineData(string Id, string Name, string SourceUnitId, int RequiredCopies, string ResultUnitId, bool? InheritPersistentModifiers);''')
replace_once(
    'src/Battlegrounds.Content/UnitCombineModValidator.cs',
    '''        "id", "name", "sourceUnitId", "requiredCopies", "resultUnitId",''',
    '''        "id", "name", "sourceUnitId", "requiredCopies", "resultUnitId", "inheritPersistentModifiers",''')
replace_once(
    'src/Battlegrounds.Content/UnitCombineModValidator.cs',
    '''                if (sourceUnitId is not null && !unitIds.Contains(sourceUnitId))''',
    '''                if (root.TryGetProperty("inheritPersistentModifiers", out var inherit) &&\n                    inherit.ValueKind is not (JsonValueKind.True or JsonValueKind.False))\n                    issues.Add(new("INVALID_TYPE", file, "$.inheritPersistentModifiers", "inheritPersistentModifiers must be a boolean."));\n\n                if (sourceUnitId is not null && !unitIds.Contains(sourceUnitId))''')

# Regression: persistent modifiers aggregate by key; untilCombatEnd is deliberately not inherited.
test_path = Path('tests/Battlegrounds.Core.Tests/Preparation/UnitCombineTests.cs')
text = test_path.read_text()
marker = '    [Fact]\n    public void Combine_RejectsDuplicateOrWrongSourceInstances()\n'
if text.count(marker) != 1:
    raise SystemExit('UnitCombine test marker mismatch')
test = r'''    [Fact]
    public void Combine_CanAggregatePersistentModifiersWithoutCarryingTemporaryOnes()
    {
        var source = new UnitDefinition(new UnitId("source"), "Source", 1, 2, 2);
        var result = new UnitDefinition(new UnitId("result"), "Result", 1, 5, 5);
        var combine = new UnitCombineDefinition(
            new UnitCombineId("inherit"),
            "Inherited Combine",
            source.Id,
            3,
            result.Id,
            inheritPersistentModifiers: true);
        var combines = new UnitCombineCatalog([combine]);
        var units = new UnitCatalog([source, result], combines);
        var pool = new UnitPool(units, [new UnitPoolEntry(source.Id, 6)]);
        var rules = new PreparationRules(10, 0, 10, 0, 1, 1, 7, 10, 2, [3, 3], [5]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, null, combines);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new AcquireUnitCommand(player.Id, 0)).Succeeded);
        player.Reserve[0].ApplyModifier("legacy", 1, 2, UnitModifierDuration.Persistent);
        player.Reserve[1].ApplyModifier("legacy", 3, 4, UnitModifierDuration.Persistent);
        player.Reserve[2].ApplyModifier("temporary", 9, 9, UnitModifierDuration.UntilCombatEnd);
        var ids = player.Reserve.Select(unit => unit.Id).ToArray();

        Assert.True(engine.Execute(match, new CombineUnitsCommand(player.Id, combine.Id, ids)).Succeeded);

        var merged = Assert.Single(player.Reserve);
        Assert.Equal(9, merged.Attack);
        Assert.Equal(11, merged.Health);
        var inherited = Assert.Single(merged.Modifiers);
        Assert.Equal("legacy", inherited.Key);
        Assert.Equal(4, inherited.AttackDelta);
        Assert.Equal(6, inherited.HealthDelta);
        Assert.Equal(UnitModifierDuration.Persistent, inherited.Duration);
    }

'''
test_path.write_text(text.replace(marker, test + marker, 1))

# Warbands: Wolf Pack preserves authored persistent investments across the combine.
combine_path = Path('mods/warbands/content/combines/wolf-pack.json')
combine = json.loads(combine_path.read_text())
combine['inheritPersistentModifiers'] = True
combine_path.write_text(json.dumps(combine, indent=2) + '\n')

# Add one persistent, non-stacking investment Action that makes the carryover visible in real play.
action = {
    'id': 'pack-bond',
    'name': 'Pack Bond',
    'tier': 2,
    'cost': 2,
    'effects': [{
        'kind': 'applyUnitModifier',
        'target': {'scope': 'selected', 'typeId': 'beast'},
        'modifierKey': 'pack-bond',
        'attack': 2,
        'health': 2
    }]
}
Path('mods/warbands/content/actions/pack-bond.json').write_text(json.dumps(action, indent=2) + '\n')
