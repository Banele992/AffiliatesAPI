namespace AffiliatesAPI.Models;

/// <summary>
/// The incoming payload from the frontend for every chat turn.
/// </summary>
public class ChatRequest
{
    /// <summary>The natural-language message from the user.</summary>
    public required string Message { get; set; }

    /// <summary>
    /// Optional conversation ID for multi-turn chat history tracking.
    /// If null a new conversation is started.
    /// </summary>
    public string? ConversationId { get; set; }

    /// <summary>
    /// BCP-47 language code selected by the user (e.g. "en", "zu", "af", "pt", "fr").
    /// Defaults to "en" if not supplied.
    /// </summary>
    public string Language { get; set; } = "en";
}
