from pathlib import Path

def rep(path, old, new):
    p = Path(path)
    text = p.read_text()
    n = text.count(old)
    if n != 1:
        raise SystemExit(f"{path}: expected one match, got {n}\n--- old ---\n{old}")
    p.write_text(text.replace(old, new))

rep("src/Battlegrounds.Core/Domain/Effects/EffectDefinitions.cs",
"""public enum EffectTargetSelection
{
    All,
    Random,
    LowestAttack,
    HighestAttack,
    LowestHealth,
    HighestHealth,
    Leftmost,
    Rightmost,
    Adjacent,
    LeftAdjacent,
    RightAdjacent,
}

public sealed record EffectUnitQuery""",
"""public enum EffectTargetSelection
{
    All,
    Random,
    LowestAttack,
    HighestAttack,
    LowestHealth,
    HighestHealth,
    Leftmost,
    Rightmost,
    Adjacent,
    LeftAdjacent,
    RightAdjacent,
}

public enum EffectTargetAnchor
{
    Source,
    Selected,
}

public sealed record EffectUnitQuery""")

rep("src/Battlegrounds.Core/Domain/Effects/EffectDefinitions.cs",
"""public sealed record EffectTargetSelector
{
    public EffectUnitQuery Query { get; }
    public EffectTargetSelection Selection { get; }
    public int? Limit { get; }
""",
"""public sealed record EffectTargetSelector
{
    public EffectUnitQuery Query { get; }
    public EffectTargetSelection Selection { get; }
    public int? Limit { get; }
    public EffectTargetAnchor RelativeTo { get; }
""")

rep("src/Battlegrounds.Core/Domain/Effects/EffectDefinitions.cs",
"""        int? limit = null,
        UnitTypeId? requiredTypeId = null,
        TagId? requiredTagId = null)
        : this(new EffectUnitQuery(scope, excludeSource, requiredTypeId, requiredTagId), selection, limit)
""",
"""        int? limit = null,
        UnitTypeId? requiredTypeId = null,
        TagId? requiredTagId = null,
        EffectTargetAnchor relativeTo = EffectTargetAnchor.Source)
        : this(new EffectUnitQuery(scope, excludeSource, requiredTypeId, requiredTagId), selection, limit, relativeTo)
""")

rep("src/Battlegrounds.Core/Domain/Effects/EffectDefinitions.cs",
"""    public EffectTargetSelector(
        EffectUnitQuery query,
        EffectTargetSelection selection = EffectTargetSelection.All,
        int? limit = null)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (limit is not null && limit.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Target limit must be positive.");
        if (query.Scope is EffectTargetScope.Self or EffectTargetScope.Selected &&
            (selection != EffectTargetSelection.All || limit is not null))
        {
            throw new ArgumentException("Self and selected targets cannot use selection or limit.");
        }
        if (selection is EffectTargetSelection.Adjacent or EffectTargetSelection.LeftAdjacent or EffectTargetSelection.RightAdjacent &&
            query.Scope != EffectTargetScope.Friendly)
        {
            throw new ArgumentException("Adjacent target selection is only valid for friendly targets.", nameof(selection));
        }

        Selection = selection;
        Limit = limit;
    }
""",
"""    public EffectTargetSelector(
        EffectUnitQuery query,
        EffectTargetSelection selection = EffectTargetSelection.All,
        int? limit = null,
        EffectTargetAnchor relativeTo = EffectTargetAnchor.Source)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (limit is not null && limit.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Target limit must be positive.");
        if (query.Scope is EffectTargetScope.Self or EffectTargetScope.Selected &&
            (selection != EffectTargetSelection.All || limit is not null))
        {
            throw new ArgumentException("Self and selected targets cannot use selection or limit.");
        }
        var isAdjacentSelection = selection is EffectTargetSelection.Adjacent or EffectTargetSelection.LeftAdjacent or EffectTargetSelection.RightAdjacent;
        if (relativeTo == EffectTargetAnchor.Selected && !isAdjacentSelection)
            throw new ArgumentException("Selected-relative targeting is only valid for adjacent selections.", nameof(relativeTo));
        if (isAdjacentSelection && relativeTo == EffectTargetAnchor.Source && query.Scope != EffectTargetScope.Friendly)
            throw new ArgumentException("Source-relative adjacent target selection is only valid for friendly targets.", nameof(selection));

        Selection = selection;
        Limit = limit;
        RelativeTo = relativeTo;
    }
""")

rep("src/Battlegrounds.Core/Domain/Effects/EffectPipeline.cs",
"""            EffectTargetSelection.Adjacent => SelectAdjacent(candidates, context, true, true),
            EffectTargetSelection.LeftAdjacent => SelectAdjacent(candidates, context, true, false),
            EffectTargetSelection.RightAdjacent => SelectAdjacent(candidates, context, false, true),
""",
"""            EffectTargetSelection.Adjacent => SelectAdjacent(candidates, context, selector.RelativeTo, true, true),
            EffectTargetSelection.LeftAdjacent => SelectAdjacent(candidates, context, selector.RelativeTo, true, false),
            EffectTargetSelection.RightAdjacent => SelectAdjacent(candidates, context, selector.RelativeTo, false, true),
""")

rep("src/Battlegrounds.Core/Domain/Effects/EffectPipeline.cs",
"""    private static IReadOnlyList<EffectUnitSnapshot> SelectAdjacent(
        IReadOnlyList<EffectUnitSnapshot> candidates,
        EffectResolutionContext context,
        bool includeLeft,
        bool includeRight)
    {
        var source = context.Units.Single(unit => unit.InstanceId == context.SourceInstanceId);
        if (source.Position < 0) return [];
        var result = new List<EffectUnitSnapshot>(2);
        if (includeLeft)
        {
            var left = candidates.FirstOrDefault(unit => unit.OwnerPlayerId == source.OwnerPlayerId && unit.Position == source.Position - 1);
            if (left is not null) result.Add(left);
        }
        if (includeRight)
        {
            var right = candidates.FirstOrDefault(unit => unit.OwnerPlayerId == source.OwnerPlayerId && unit.Position == source.Position + 1);
            if (right is not null) result.Add(right);
        }
        return result;
    }
""",
"""    private static IReadOnlyList<EffectUnitSnapshot> SelectAdjacent(
        IReadOnlyList<EffectUnitSnapshot> candidates,
        EffectResolutionContext context,
        EffectTargetAnchor relativeTo,
        bool includeLeft,
        bool includeRight)
    {
        EffectUnitSnapshot? anchor = relativeTo switch
        {
            EffectTargetAnchor.Source => context.Units.Single(unit => unit.InstanceId == context.SourceInstanceId),
            EffectTargetAnchor.Selected when context.SelectedTargetInstanceId is UnitInstanceId selectedId =>
                context.Units.SingleOrDefault(unit => unit.InstanceId == selectedId),
            EffectTargetAnchor.Selected => null,
            _ => throw new ArgumentOutOfRangeException(nameof(relativeTo), relativeTo, "Unsupported target anchor."),
        };
        if (anchor is null || anchor.Position < 0) return [];

        var result = new List<EffectUnitSnapshot>(2);
        if (includeLeft)
        {
            var left = candidates.FirstOrDefault(unit => unit.OwnerPlayerId == anchor.OwnerPlayerId && unit.Position == anchor.Position - 1);
            if (left is not null) result.Add(left);
        }
        if (includeRight)
        {
            var right = candidates.FirstOrDefault(unit => unit.OwnerPlayerId == anchor.OwnerPlayerId && unit.Position == anchor.Position + 1);
            if (right is not null) result.Add(right);
        }
        return result;
    }
""")

rep("src/Battlegrounds.Content/ModLoader.cs",
"""        return new EffectTargetSelector(BuildQuery(data), data.Selection ?? EffectTargetSelection.All, data.Limit);
""",
"""        return new EffectTargetSelector(
            BuildQuery(data),
            data.Selection ?? EffectTargetSelection.All,
            data.Limit,
            data.RelativeTo ?? EffectTargetAnchor.Source);
""")

rep("src/Battlegrounds.Content/ModLoader.cs",
"""    private sealed record TargetData(EffectTargetScope Scope, bool? ExcludeSource, string? TypeId, string? TagId, EffectTargetSelection? Selection, int? Limit)
        : QueryData(Scope, ExcludeSource, TypeId, TagId);
""",
"""    private sealed record TargetData(
        EffectTargetScope Scope,
        bool? ExcludeSource,
        string? TypeId,
        string? TagId,
        EffectTargetSelection? Selection,
        int? Limit,
        EffectTargetAnchor? RelativeTo)
        : QueryData(Scope, ExcludeSource, TypeId, TagId);
""")

for path in [
    "src/Battlegrounds.Content/DirectoryModValidator.cs",
    "src/Battlegrounds.Content/PowerLifecycleModValidator.cs",
]:
    rep(path,
"""            ["scope", "selection", "excludeSource", "limit", "typeId", "tagId"],
""",
"""            ["scope", "selection", "excludeSource", "limit", "typeId", "tagId", "relativeTo"],
""")

rep("src/Battlegrounds.Content/PersistentUnitMutationModValidator.cs",
"""            ["scope", "excludeSource", "typeId", "tagId", "selection", "limit"],
""",
"""            ["scope", "excludeSource", "typeId", "tagId", "selection", "limit", "relativeTo"],
""")

rep("src/Battlegrounds.Content/PersistentUnitMutationModValidator.cs",
"""            else if (scope is "self" or "selected")
                issues.Add(new("INVALID_VALUE", file, path + ".selection", "Self and selected targets cannot use selection."));
            else if (selectionValue.GetString() is "adjacent" or "leftAdjacent" or "rightAdjacent" && scope != "friendly")
                issues.Add(new("INVALID_VALUE", file, path + ".selection", "Adjacent selection requires friendly scope."));
""",
"""            else if (scope is "self" or "selected")
                issues.Add(new("INVALID_VALUE", file, path + ".selection", "Self and selected targets cannot use selection."));
""")

rep("src/Battlegrounds.Content/AdvancedEffectModValidator.cs",
"""    private static readonly HashSet<string> Comparisons =
""",
"""    private static readonly HashSet<string> RelativeTargets = ["source", "selected"];
    private static readonly HashSet<string> Comparisons =
""")

rep("src/Battlegrounds.Content/AdvancedEffectModValidator.cs",
"""        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, typeIds, tagIds, powerMode: true, issues);

        return issues;
    }

    private static void ValidateTriggers(
""",
"""        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, typeIds, tagIds, powerMode: true, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/actions"))
            ValidateActionTargets(file, typeIds, tagIds, issues);

        return issues;
    }

    private static void ValidateActionTargets(
        EntityFile file,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Array) return;
        var effectIndex = 0;
        foreach (var effect in effects.EnumerateArray())
        {
            if (effect.ValueKind == JsonValueKind.Object && effect.TryGetProperty("target", out var target))
            {
                ValidateTarget(
                    file.Path,
                    target,
                    $"$.effects[{effectIndex}].target",
                    allowSelected: true,
                    typeIds,
                    tagIds,
                    issues);
            }
            effectIndex++;
        }
    }

    private static void ValidateTriggers(
""")

rep("src/Battlegrounds.Content/AdvancedEffectModValidator.cs",
"""            ["scope", "selection", "excludeSource", "limit", "typeId", "tagId"],
""",
"""            ["scope", "selection", "excludeSource", "limit", "typeId", "tagId", "relativeTo"],
""")

rep("src/Battlegrounds.Content/AdvancedEffectModValidator.cs",
"""        var excludeSource = false;
        if (target.TryGetProperty("excludeSource", out var excludeElement))
""",
"""        var hasRelativeTo = target.TryGetProperty("relativeTo", out _);
        var relativeTo = "source";
        if (hasRelativeTo &&
            TryRequiredString(target, "relativeTo", file, path + ".relativeTo", issues, out var parsedRelativeTo))
        {
            relativeTo = parsedRelativeTo!;
            if (!RelativeTargets.Contains(relativeTo))
                issues.Add(new("INVALID_VALUE", file, path + ".relativeTo", $"Unknown target anchor '{relativeTo}'."));
        }

        var excludeSource = false;
        if (target.TryGetProperty("excludeSource", out var excludeElement))
""")

rep("src/Battlegrounds.Content/AdvancedEffectModValidator.cs",
"""        ValidateSelectorCombination(file, path, scope, selection, excludeSource, hasLimit, issues);
""",
"""        ValidateSelectorCombination(file, path, scope, selection, relativeTo, hasRelativeTo, allowSelected, excludeSource, hasLimit, issues);
""")

rep("src/Battlegrounds.Content/AdvancedEffectModValidator.cs",
"""    private static void ValidateSelectorCombination(
        string file,
        string path,
        string? scope,
        string selection,
        bool excludeSource,
        bool hasLimit,
        List<ModValidationIssue> issues)
    {
        if (excludeSource && scope != "friendly")
            issues.Add(new("INVALID_PARAMETER", file, path + ".excludeSource", "excludeSource is only valid for friendly targets."));

        if (scope is "self" or "selected")
        {
            if (selection != "all")
                issues.Add(new("INVALID_PARAMETER", file, path + ".selection", "self/selected targets cannot use selection."));
            if (hasLimit)
                issues.Add(new("INVALID_PARAMETER", file, path + ".limit", "self/selected targets cannot use limit."));
        }

        if (selection is "adjacent" or "leftAdjacent" or "rightAdjacent" && scope != "friendly")
            issues.Add(new("INVALID_PARAMETER", file, path + ".selection", "Adjacent selection is only valid for friendly targets."));
    }
""",
"""    private static void ValidateSelectorCombination(
        string file,
        string path,
        string? scope,
        string selection,
        string relativeTo,
        bool hasRelativeTo,
        bool allowSelected,
        bool excludeSource,
        bool hasLimit,
        List<ModValidationIssue> issues)
    {
        if (excludeSource && scope != "friendly")
            issues.Add(new("INVALID_PARAMETER", file, path + ".excludeSource", "excludeSource is only valid for friendly targets."));

        if (scope is "self" or "selected")
        {
            if (selection != "all")
                issues.Add(new("INVALID_PARAMETER", file, path + ".selection", "self/selected targets cannot use selection."));
            if (hasLimit)
                issues.Add(new("INVALID_PARAMETER", file, path + ".limit", "self/selected targets cannot use limit."));
        }

        var isAdjacentSelection = selection is "adjacent" or "leftAdjacent" or "rightAdjacent";
        if (hasRelativeTo && !isAdjacentSelection)
            issues.Add(new("INVALID_PARAMETER", file, path + ".relativeTo", "relativeTo is only valid for adjacent selections."));
        if (relativeTo == "selected" && !allowSelected)
            issues.Add(new("INVALID_VALUE", file, path + ".relativeTo", "selected relative targeting requires a context target."));
        if (isAdjacentSelection && relativeTo == "source" && scope != "friendly")
            issues.Add(new("INVALID_PARAMETER", file, path + ".selection", "Source-relative adjacent selection is only valid for friendly targets."));
    }
""")

rep("tests/Battlegrounds.Core.Tests/Effects/EffectPipelineTests.cs",
"""    [Fact]
    public void ResolveEvent_ConditionsGateWholeTrigger()
""",
"""    [Fact]
    public void ResolveEvent_AdjacentCanUseSelectedTargetAsAnchor()
    {
        var target = new EffectTargetSelector(
            EffectTargetScope.Enemy,
            EffectTargetSelection.Adjacent,
            relativeTo: EffectTargetAnchor.Selected);
        var definition = Unit(new TriggerDefinition(
            NativeTriggerKeys.OnAttack,
            [new DealDamageEffectDefinition(target, 2)]));
        var units = new[]
        {
            UnitSnapshot(1, 0, true, position: 0),
            UnitSnapshot(2, 1, true, position: 0),
            UnitSnapshot(3, 1, true, position: 1),
            UnitSnapshot(4, 1, true, position: 2),
        };
        var context = new EffectResolutionContext(
            new UnitInstanceId(1),
            new PlayerId(0),
            units,
            new UnitInstanceId(3));

        var resolved = Assert.Single(new EffectPipeline().ResolveEvent(
            definition,
            NativeTriggerKeys.OnAttack,
            context,
            new MinimumRandomSource()));

        Assert.Equal([new UnitInstanceId(2), new UnitInstanceId(4)], resolved.TargetInstanceIds);
    }

    [Fact]
    public void ResolveEvent_ConditionsGateWholeTrigger()
""")

rep("tests/Battlegrounds.Content.Tests/AdvancedEffectValidationTests.cs",
"""    private static string CreateTempMod()
""",
"""    [Fact]
    public void Validate_SelectedRelativeAdjacencyRequiresContextAndAllowsEnemyNeighbors()
    {
        var path = CreateTempMod();
        try
        {
            var unitPath = Path.Combine(path, "content", "units", "guard.json");
            var attackJson =
                """
                {
                  "id": "guard",
                  "name": "Guard",
                  "tier": 1,
                  "attack": 2,
                  "health": 2,
                  "triggers": [
                    {
                      "event": "onAttack",
                      "effects": [
                        {
                          "kind": "dealDamage",
                          "target": {
                            "scope": "enemy",
                            "selection": "adjacent",
                            "relativeTo": "selected"
                          },
                          "amount": 1
                        }
                      ]
                    }
                  ]
                }
                """;

            File.WriteAllText(unitPath, attackJson);
            var validReport = new ModValidator().Validate(path);
            Assert.True(validReport.IsValid, string.Join(Environment.NewLine, validReport.Issues.Select(issue => $"{issue.Code}: {issue.File} {issue.Path} {issue.Message}")));

            File.WriteAllText(unitPath, attackJson.Replace("onAttack", "onCombatStart", StringComparison.Ordinal));
            var invalidReport = new ModValidator().Validate(path);
            Assert.Contains(invalidReport.Issues, issue =>
                issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".target.relativeTo"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
""")

rep("mods/warbands/content/units/elder-hydra.json",
"""          "target": { "scope": "selected" },
          "amount": 2
""",
"""          "target": {
            "scope": "enemy",
            "selection": "adjacent",
            "relativeTo": "selected"
          },
          "amount": 2
""")
