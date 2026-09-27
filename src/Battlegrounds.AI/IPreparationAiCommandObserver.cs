using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.AI;

/// <summary>
/// Optional read-only observation hook around AI-emitted Preparation commands.
/// Observers must not participate in decision-making or consume gameplay RNG.
/// </summary>
public interface IPreparationAiCommandObserver
{
    void BeforeCommand(PlayerState player, IPreparationCommand command);
    void AfterAcceptedCommand(PlayerState player, IPreparationCommand command);
}
