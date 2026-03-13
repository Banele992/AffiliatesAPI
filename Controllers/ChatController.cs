using Microsoft.AspNetCore.Mvc;
using AffiliatesAPI.Models;
using AffiliatesAPI.Orchestration;

namespace AffiliatesAPI.Controllers;

/// <summary>
/// Chat endpoint — the only public surface of the API.
/// Single responsibility: accept message → delegate to orchestrator → return result.
/// </summary>
[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatOrchestrator _orchestrator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatOrchestrator orchestrator, ILogger<ChatController> logger)
    {
        _orchestrator = orchestrator;
        _logger       = logger;
    }

    /// <summary>
    /// Send a message to the Affiliates FAQ chatbot and receive a natural-language response.
    /// Pass an optional <c>conversationId</c> to continue an existing conversation.
    /// If omitted, a new conversation is created and the ID is returned in the response.
    /// The assistant reply and the conversation ID for continuation.
    /// <response code="500">An internal server error occurred.</response>
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message cannot be empty." });
        }

        // Generate a new conversation ID if the client has not supplied one
        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId;

        try
        {
            var response = await _orchestrator.ProcessAsync(request.Message, conversationId, request.Language);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in Chat endpoint | ConversationId={ConvId}", conversationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Stream a message to the Affiliates FAQ chatbot and receive partial tokens using Server-Sent Events.
    /// </summary>
    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("N")
            : request.ConversationId;

        // Set headers correctly for Server-Sent Events
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            var initData = System.Text.Json.JsonSerializer.Serialize(new { conversationId });
            await Response.WriteAsync($"data: {initData}\n\n");
            await Response.Body.FlushAsync();

            var chunks = _orchestrator.ProcessStreamAsync(request.Message, conversationId, request.Language);

            await foreach (var chunk in chunks)
            {
                // Micro-opt: Avoid anonymous object instantiation and JSON Serialize on every character.
                var escapedChunk = System.Text.Json.JsonEncodedText.Encode(chunk);
                await Response.WriteAsync($"data: {{\"text\":\"{escapedChunk}\"}}\n\n");
                await Response.Body.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in Stream endpoint | ConversationId={ConvId}", conversationId);
            var errorData = System.Text.Json.JsonSerializer.Serialize(new { error = $"An error occurred: {ex.Message}" });
            await Response.WriteAsync($"data: {errorData}\n\n");
            await Response.Body.FlushAsync();
        }
    }
}
