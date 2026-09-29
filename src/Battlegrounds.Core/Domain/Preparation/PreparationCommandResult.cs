namespace Battlegrounds.Core.Domain.Preparation;

public enum PreparationFailureCode
{
    MatchNotInPreparation,
    PlayerNotFound,
    PlayerEliminated,
    PlayerAlreadyReady,
    PendingChoiceMustBeResolved,
    NoPendingChoice,
    InvalidChoice,
    InsufficientResource,
    ReserveFull,
    FieldFull,
    InvalidOfferSlot,
    InvalidReserveSlot,
    InvalidFieldSlot,
    InvalidFieldOrder,
    InvalidDeployTarget,
    InvalidActionTarget,
    CombineUnavailable,
    InvalidCombineUnits,
    MaximumTier,
    PowerUnavailable,
    PowerNotActivatable,
    PowerUsageLimitReached,
    InvalidPowerTarget,
    OfferAlreadyFrozen,
    OfferNotFrozen,
    OfferSlotAlreadyFrozen,
    OfferSlotNotFrozen,
}

public readonly record struct PreparationCommandResult
{
    public bool Succeeded { get; }
    public PreparationFailureCode? FailureCode { get; }

    private PreparationCommandResult(bool succeeded, PreparationFailureCode? failureCode)
    {
        Succeeded = succeeded;
        FailureCode = failureCode;
    }

    public static PreparationCommandResult Success() => new(true, null);
    public static PreparationCommandResult Failure(PreparationFailureCode failureCode) => new(false, failureCode);
}
