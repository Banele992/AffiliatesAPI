namespace AffiliatesAPI.Models;

/// <summary>
/// The structured payload returned to the frontend after every chat turn.
/// </summary>
public class ChatResponse
{
    /// <summary>The assistant reply in plain text / markdown.</summary>
    public required string Reply { get; set; }

    /// <summary>Echo of the conversation ID so the client can continue the thread.</summary>
    public required string ConversationId { get; set; }
}
