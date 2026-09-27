namespace Battlegrounds.AI;

/// <summary>
/// High-level economic temperament for an AI-controlled Preparation turn.
/// Personalities only influence policy ordering; they never bypass Core commands or legality.
/// </summary>
public enum PreparationAiPersonality
{
    /// <summary>
    /// Prefers immediate board development and usable effects before longer-term economy.
    /// </summary>
    Tempo,

    /// <summary>
    /// Prefers tier progression and long-term economy before spending on the current offer.
    /// </summary>
    Greedy,

    /// <summary>
    /// Values searching for a better offer and is willing to spend more resources refreshing.
    /// </summary>
    Roller,
}
