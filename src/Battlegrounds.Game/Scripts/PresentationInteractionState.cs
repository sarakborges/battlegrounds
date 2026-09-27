using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Game;

internal enum PresentationInteractionKind
{
    None,
    ActionTarget,
    PowerTarget,
    CombineRecipe,
    CombineComponents,
}

internal sealed class PresentationInteractionState
{
    private readonly HashSet<UnitInstanceId> _selectedUnits = [];

    public PresentationInteractionKind Kind { get; private set; }
    public int? ActionReserveSlot { get; private set; }
    public UnitCombineId? CombineId { get; private set; }
    public IReadOnlyCollection<UnitInstanceId> SelectedUnits => _selectedUnits;

    public bool IsActive => Kind != PresentationInteractionKind.None;

    public void BeginActionTarget(int reserveSlot)
    {
        Reset();
        Kind = PresentationInteractionKind.ActionTarget;
        ActionReserveSlot = reserveSlot;
    }

    public void BeginPowerTarget()
    {
        Reset();
        Kind = PresentationInteractionKind.PowerTarget;
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
        ActionReserveSlot = null;
        CombineId = null;
        _selectedUnits.Clear();
    }
}
