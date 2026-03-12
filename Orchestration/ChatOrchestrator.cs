using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AffiliatesAPI.Models;

namespace AffiliatesAPI.Orchestration;

/// <summary>
/// Contract for the orchestration layer.
/// </summary>
public interface IChatOrchestrator
{
    Task<ChatResponse> ProcessAsync(string message, string conversationId, string language = "en");
}

/// <summary>
/// Drives the Semantic Kernel pipeline for a single chat turn.
/// Responsibility: receive raw text → run kernel with tool-calling → return structured result.
/// </summary>
public class ChatOrchestrator : IChatOrchestrator
{
    private readonly Kernel _kernel;
    private readonly ILogger<ChatOrchestrator> _logger;

    // In-memory conversation store keyed by ConversationId.
    private static readonly Dictionary<string, ChatHistory> _sessions = new();
    private static readonly SemaphoreSlim _sessionLock = new(1, 1);

    public ChatOrchestrator(Kernel kernel, ILogger<ChatOrchestrator> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(string message, string conversationId, string language = "en")
    {
        _logger.LogInformation("Processing message | ConversationId={ConvId} Language={Lang} Message={Message}",
            conversationId, language, message);

        var history = await GetOrCreateHistoryAsync(conversationId, language);

        history.AddUserMessage(message);

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            // Core mechanism: the LLM emits function calls,
            // Semantic Kernel intercepts and executes the C# skill methods automatically.
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var result = await chatCompletion.GetChatMessageContentAsync(
            history,
            executionSettings,
            _kernel);

        var reply = result.Content ?? "I'm sorry, I was unable to process your request. Please try again.";

        history.AddAssistantMessage(reply);
        await SaveHistoryAsync(conversationId, history);

        _logger.LogInformation("Response generated | ConversationId={ConvId} Reply={Reply}",
            conversationId, reply);

        return new ChatResponse
        {
            Reply          = reply,
            ConversationId = conversationId
        };
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<ChatHistory> GetOrCreateHistoryAsync(string conversationId, string language)
    {
        await _sessionLock.WaitAsync();
        try
        {
            if (!_sessions.TryGetValue(conversationId, out var history))
            {
                history = new ChatHistory();
                history.AddSystemMessage(SystemPrompts.Main);
                history.AddSystemMessage(SystemPrompts.GetLanguageInstruction(language));
                _sessions[conversationId] = history;
            }
            return history;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task SaveHistoryAsync(string conversationId, ChatHistory history)
    {
        await _sessionLock.WaitAsync();
        try
        {
            _sessions[conversationId] = history;
        }
        finally
        {
            _sessionLock.Release();
        }
    }
}
