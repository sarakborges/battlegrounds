using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Recruitment;

public interface IRecruitmentCommand
{
    PlayerId PlayerId { get; }
}

public sealed record BuyMinionCommand(PlayerId PlayerId, int TavernSlot) : IRecruitmentCommand;
public sealed record SellMinionCommand(PlayerId PlayerId, int BoardSlot) : IRecruitmentCommand;
public sealed record PlayMinionCommand(PlayerId PlayerId, int HandSlot) : IRecruitmentCommand;
public sealed record RefreshTavernCommand(PlayerId PlayerId) : IRecruitmentCommand;
public sealed record UpgradeTavernCommand(PlayerId PlayerId) : IRecruitmentCommand;
public sealed record FreezeTavernCommand(PlayerId PlayerId) : IRecruitmentCommand;
public sealed record UnfreezeTavernCommand(PlayerId PlayerId) : IRecruitmentCommand;
public sealed record EndRecruitmentCommand(PlayerId PlayerId) : IRecruitmentCommand;
