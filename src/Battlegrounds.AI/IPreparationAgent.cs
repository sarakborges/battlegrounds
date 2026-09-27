using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.AI;

public interface IPreparationAgent
{
    IPreparationCommand ChooseCommand(
        PreparationAgentContext context,
        MatchState match,
        PlayerState player,
        PreparationAgentTurnMemory memory);
}

public sealed class PreparationAgentTurnMemory
{
    private readonly HashSet<string> _failedCommandKinds = new(StringComparer.Ordinal);

    public bool PowerAttempted { get; private set; }
    public bool RefreshAttempted { get; private set; }
    public bool FreezeStateChanged { get; private set; }

    public bool Failed(string commandKind) => _failedCommandKinds.Contains(commandKind);

    internal void Observe(IPreparationCommand command, PreparationCommandResult result)
    {
        switch (command)
        {
            case UsePowerCommand:
                PowerAttempted = true;
                break;
            case RefreshOfferCommand:
                RefreshAttempted = true;
                break;
            case FreezeOfferCommand or UnfreezeOfferCommand:
                FreezeStateChanged = true;
                break;
        }

        if (!result.Succeeded)
            _failedCommandKinds.Add(command.GetType().Name);
    }
}
