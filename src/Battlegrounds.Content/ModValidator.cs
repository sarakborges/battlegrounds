namespace Battlegrounds.Content;

public sealed class ModValidator
{
    public ModValidationReport Validate(string modDirectory)
    {
        var baseIssues = new DirectoryModValidator().Validate(modDirectory).Issues;
        var powerIssues = new PowerLifecycleModValidator().Validate(modDirectory);

        var advancedIssues = new AdvancedEffectModValidator().Validate(modDirectory);
        var dynamicIssues = new DynamicEffectValueModValidator().Validate(modDirectory);
        var statefulIssues = new StatefulEffectModValidator().Validate(modDirectory);
        var generationIssues = new GenerationChoiceModValidator().Validate(modDirectory);
        var actionIssues = new ActionModValidator().Validate(modDirectory);
        var themeIssues = new ModThemeValidator().Validate(modDirectory)
            .Concat(new ModThemeMetricsValidator().Validate(modDirectory))
            .Concat(new ModThemeRoleValidator().Validate(modDirectory));

        var issues = baseIssues
            .Concat(powerIssues)
            .Concat(advancedIssues)
            .Concat(dynamicIssues)
            .Concat(statefulIssues)
            .Concat(new UnitAuraModValidator().Validate(modDirectory))
            .Concat(generationIssues)
            .Concat(actionIssues)
            .Concat(new PreparationEconomyEffectModValidator().Validate(modDirectory))
            .Concat(new PersistentUnitMutationModValidator().Validate(modDirectory))
            .Concat(new UnitCombineModValidator().Validate(modDirectory))
            .Concat(new PresentationModValidator().Validate(modDirectory))
            .Concat(new PresentationAssetModValidator().Validate(modDirectory))
            .Concat(new ModInteractionSettingsValidator().Validate(modDirectory))
            .Concat(themeIssues)
            .ToArray();

        var preliminaryReport = new ModValidationReport(issues);
        return new ModValidationReport(issues.Concat(new LeaderSelectionModValidator().Validate(modDirectory, preliminaryReport)));
    }
}
