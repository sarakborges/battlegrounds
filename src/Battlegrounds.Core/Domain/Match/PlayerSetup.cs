using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Match;

public readonly record struct PlayerSetup(PlayerId PlayerId, LeaderId LeaderId);
