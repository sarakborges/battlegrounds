using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Game;

internal enum PresentationInteractionKind
{
    None,
    DeployTarget,
    ActionTarget,
    PowerTarget,
    CombineRecipe,
    CombineComponents,
}

internal sealed class PresentationInteractionState
{
    private readonly HashSet<UnitInstanceId> _selectedUnits = [];

    public PresentationInteractionKind Kind { get; private set; }
    public int? UnitReserveSlot { get; private set; }
    public int? ActionReserveSlot { get; private set; }
    public UnitCombineId? CombineId { get; private set; }
    public EffectTargetZone TargetZone { get; private set; } = EffectTargetZone.Field;
    public IReadOnlyCollection<UnitInstanceId> SelectedUnits => _selectedUnits;

    public bool IsActive => Kind != PresentationInteractionKind.None;

    public void BeginDeployTarget(int reserveSlot, EffectTargetZone targetZone = EffectTargetZone.Field)
    {
        Reset();
        Kind = PresentationInteractionKind.DeployTarget;
        UnitReserveSlot = reserveSlot;
        TargetZone = targetZone;
    }

    public void BeginActionTarget(int reserveSlot, EffectTargetZone targetZone = EffectTargetZone.Field)
    {
        Reset();
        Kind = PresentationInteractionKind.ActionTarget;
        ActionReserveSlot = reserveSlot;
        TargetZone = targetZone;
    }

    public void BeginPowerTarget(EffectTargetZone targetZone = EffectTargetZone.Field)
    {
        Reset();
        Kind = PresentationInteractionKind.PowerTarget;
        TargetZone = targetZone;
    }

    public void BeginCombineRecipeSelection()
    {
        Reset();
        Kind = PresentationInteractionKind.CombineRecipe;
    }

    public void BeginCombineComponents(UnitCombineId combineId)
    {
        Reset();
        Kind = PresentationInteractionKind.CombineComponents;
        CombineId = combineId;
    }

    public bool ToggleUnit(UnitInstanceId unitId)
    {
        if (Kind != PresentationInteractionKind.CombineComponents)
            throw new InvalidOperationException("Unit component selection is only valid while choosing combine components.");

        if (_selectedUnits.Remove(unitId)) return false;
        _selectedUnits.Add(unitId);
        return true;
    }

    public bool IsSelected(UnitInstanceId unitId) => _selectedUnits.Contains(unitId);

    public void Reset()
    {
        Kind = PresentationInteractionKind.None;
        UnitReserveSlot = null;
        ActionReserveSlot = null;
        CombineId = null;
        TargetZone = EffectTargetZone.Field;
        _selectedUnits.Clear();
    }
}
