using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Effects;

public sealed record UnitDefinitionQuery
{
    public int? MinimumTier { get; }
    public int? MaximumTier { get; }
    public UnitTypeId? RequiredTypeId { get; }
    public TagId? RequiredTagId { get; }
    public bool ExcludeSource { get; }

    public UnitDefinitionQuery(
        int? minimumTier = null,
        int? maximumTier = null,
        UnitTypeId? requiredTypeId = null,
        TagId? requiredTagId = null,
        bool excludeSource = false)
    {
        if (minimumTier is not null && minimumTier.Value <= 0) throw new ArgumentOutOfRangeException(nameof(minimumTier));
        if (maximumTier is not null && maximumTier.Value <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTier));
        if (minimumTier is not null && maximumTier is not null && minimumTier.Value > maximumTier.Value)
            throw new ArgumentException("minimumTier cannot exceed maximumTier.");
        MinimumTier = minimumTier;
        MaximumTier = maximumTier;
        RequiredTypeId = requiredTypeId;
        RequiredTagId = requiredTagId;
        ExcludeSource = excludeSource;
    }

    public bool Matches(UnitDefinition candidate, UnitDefinition source)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(source);
        return (!ExcludeSource || candidate.Id != source.Id) &&
               (MinimumTier is null || candidate.Tier >= MinimumTier.Value) &&
               (MaximumTier is null || candidate.Tier <= MaximumTier.Value) &&
               (RequiredTypeId is null || candidate.Types.Any(type => type.Id == RequiredTypeId.Value)) &&
               (RequiredTagId is null || candidate.Tags.Any(tag => tag.Id == RequiredTagId.Value));
    }
}

public sealed record ActionDefinitionQuery
{
    public int? MinimumTier { get; }
    public int? MaximumTier { get; }
    public ActionId? ExcludedActionId { get; }

    public ActionDefinitionQuery(int? minimumTier = null, int? maximumTier = null, ActionId? excludedActionId = null)
    {
        if (minimumTier is not null && minimumTier.Value <= 0) throw new ArgumentOutOfRangeException(nameof(minimumTier));
        if (maximumTier is not null && maximumTier.Value <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTier));
        if (minimumTier is not null && maximumTier is not null && minimumTier.Value > maximumTier.Value)
            throw new ArgumentException("minimumTier cannot exceed maximumTier.");
        MinimumTier = minimumTier;
        MaximumTier = maximumTier;
        ExcludedActionId = excludedActionId;
    }

    public bool Matches(ActionDefinition candidate) =>
        (ExcludedActionId is null || candidate.Id != ExcludedActionId.Value) &&
        (MinimumTier is null || candidate.Tier >= MinimumTier.Value) &&
        (MaximumTier is null || candidate.Tier <= MaximumTier.Value);
}

public sealed record GenerateUnitToReserveEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.GenerateUnitToReserve;
    public UnitId UnitId { get; }
    public EffectValueExpression Count { get; }

    public GenerateUnitToReserveEffectDefinition(UnitId unitId, int count = 1)
        : this(unitId, new ConstantEffectValueExpression(count))
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
    }

    public GenerateUnitToReserveEffectDefinition(UnitId unitId, EffectValueExpression count)
    {
        UnitId = unitId;
        Count = count ?? throw new ArgumentNullException(nameof(count));
    }
}

public sealed record GenerateUnitChoiceEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.GenerateUnitChoice;
    public UnitDefinitionQuery Query { get; }
    public int OptionCount { get; }

    public GenerateUnitChoiceEffectDefinition(UnitDefinitionQuery query, int optionCount = 3)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (optionCount <= 0) throw new ArgumentOutOfRangeException(nameof(optionCount));
        OptionCount = optionCount;
    }
}

public sealed record GenerateActionToReserveEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.GenerateActionToReserve;
    public ActionId ActionId { get; }
    public EffectValueExpression Count { get; }

    public GenerateActionToReserveEffectDefinition(ActionId actionId, int count = 1)
        : this(actionId, new ConstantEffectValueExpression(count))
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
    }

    public GenerateActionToReserveEffectDefinition(ActionId actionId, EffectValueExpression count)
    {
        ActionId = actionId;
        Count = count ?? throw new ArgumentNullException(nameof(count));
    }
}

public sealed record GenerateActionChoiceEffectDefinition : EffectDefinition
{
    public override NativeEffectKey Kind => NativeEffectKeys.GenerateActionChoice;
    public ActionDefinitionQuery Query { get; }
    public int OptionCount { get; }

    public GenerateActionChoiceEffectDefinition(ActionDefinitionQuery query, int optionCount = 3)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (optionCount <= 0) throw new ArgumentOutOfRangeException(nameof(optionCount));
        OptionCount = optionCount;
    }
}

internal interface IGenerationChoiceRuntimeWorld
{
    int GenerateUnitToReserve(PlayerId playerId, UnitDefinition definition, int count);
    bool QueueUnitChoice(PlayerId playerId, IReadOnlyList<UnitDefinition> options);
    int GenerateActionToReserve(PlayerId playerId, ActionDefinition definition, int count);
    bool QueueActionChoice(PlayerId playerId, IReadOnlyList<ActionDefinition> options);
}
