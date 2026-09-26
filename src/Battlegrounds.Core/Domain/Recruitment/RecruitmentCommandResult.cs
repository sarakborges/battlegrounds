namespace Battlegrounds.Core.Domain.Recruitment;

public enum RecruitmentFailureCode
{
    MatchNotInRecruitment,
    PlayerNotFound,
    PlayerAlreadyReady,
    InsufficientGold,
    HandFull,
    BoardFull,
    InvalidTavernSlot,
    InvalidHandSlot,
    InvalidBoardSlot,
    MaximumTavernTier,
    TavernAlreadyFrozen,
    TavernNotFrozen,
}

public readonly record struct RecruitmentCommandResult
{
    public bool Succeeded { get; }
    public RecruitmentFailureCode? FailureCode { get; }

    private RecruitmentCommandResult(bool succeeded, RecruitmentFailureCode? failureCode)
    {
        Succeeded = succeeded;
        FailureCode = failureCode;
    }

    public static RecruitmentCommandResult Success() => new(true, null);

    public static RecruitmentCommandResult Failure(RecruitmentFailureCode failureCode) =>
        new(false, failureCode);
}
