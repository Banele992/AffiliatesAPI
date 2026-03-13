using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AffiliatesAPI.Models;
using System.Collections.Concurrent;

namespace AffiliatesAPI.Orchestration;

/// <summary>
/// Contract for the orchestration layer.
/// </summary>
public interface IChatOrchestrator
{
    Task<ChatResponse> ProcessAsync(string message, string conversationId, string language = "en");
    IAsyncEnumerable<string> ProcessStreamAsync(string message, string conversationId, string language = "en");
}

/// <summary>
/// Drives the Semantic Kernel pipeline for a single chat turn.
/// Responsibility: receive raw text → run kernel with tool-calling → return structured result.
/// </summary>
public class ChatOrchestrator : IChatOrchestrator
{
    private readonly Kernel _kernel;
    private readonly ILogger<ChatOrchestrator> _logger;

    // In-memory lock-free conversation store keyed by ConversationId.
    private static readonly ConcurrentDictionary<string, ChatHistory> _sessions = new();

    public ChatOrchestrator(Kernel kernel, ILogger<ChatOrchestrator> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessAsync(string message, string conversationId, string language = "en")
    {
        _logger.LogInformation("Processing message | ConversationId={ConvId} Language={Lang} Message={Message}",
            conversationId, language, message);

        var history = GetOrCreateHistory(conversationId, language);

        history.AddUserMessage(message);

        // Keep history trim (system prompt = 2 messages, plus max 10 user/assistant turns)
        if (history.Count > 12)
        {
            var trimmedHistory = new ChatHistory();
            // Preserve the system prompts
            trimmedHistory.AddRange(history.Take(2));
            // Keep the last 10 messages
            trimmedHistory.AddRange(history.Skip(history.Count - 10));
            history = trimmedHistory;
        }

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            // Core mechanism: the LLM emits function calls,
            // Semantic Kernel intercepts and executes the C# skill methods automatically.
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            ExtensionData = new Dictionary<string, object> { { "max_completion_tokens", 150 } }
        };

        var result = await chatCompletion.GetChatMessageContentAsync(
            history,
            executionSettings,
            _kernel);

        var reply = result.Content ?? "I'm sorry, I was unable to process your request. Please try again.";

        history.AddAssistantMessage(reply);
        _sessions[conversationId] = history;

        _logger.LogInformation("Response generated | ConversationId={ConvId} Reply={Reply}",
            conversationId, reply);

        return new ChatResponse
        {
            Reply          = reply,
            ConversationId = conversationId
        };
    }

    // ─── Streaming Implementation ─────────────────────────────────────────────

    public async IAsyncEnumerable<string> ProcessStreamAsync(string message, string conversationId, string language = "en")
    {
        _logger.LogInformation("Processing message stream | ConversationId={ConvId} Language={Lang} Message={Message}",
            conversationId, language, message);

        var history = GetOrCreateHistory(conversationId, language);

        history.AddUserMessage(message);

        // Keep history trim (system prompt = 2 messages, plus max 10 user/assistant turns)
        if (history.Count > 12)
        {
            var trimmedHistory = new ChatHistory();
            // Preserve the system prompts
            trimmedHistory.AddRange(history.Take(2));
            // Keep the last 10 messages
            trimmedHistory.AddRange(history.Skip(history.Count - 10));
            history = trimmedHistory;
        }

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            ExtensionData = new Dictionary<string, object> { { "max_completion_tokens", 150 } }
        };

        var result = chatCompletion.GetStreamingChatMessageContentsAsync(
            history,
            executionSettings,
            _kernel);

        var replyBuilder = new System.Text.StringBuilder();

        await foreach (var chunk in result)
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                replyBuilder.Append(chunk.Content);
                yield return chunk.Content;
            }
        }

        var reply = replyBuilder.ToString();
        if (string.IsNullOrWhiteSpace(reply))
        {
            reply = "I'm sorry, I was unable to process your request. Please try again.";
            yield return reply;
        }

        history.AddAssistantMessage(reply);
        _sessions[conversationId] = history;

        _logger.LogInformation("Streaming response generated | ConversationId={ConvId}", conversationId);
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private ChatHistory GetOrCreateHistory(string conversationId, string language)
    {
        return _sessions.GetOrAdd(conversationId, _ => 
        {
            var newHistory = new ChatHistory();
            newHistory.AddSystemMessage(SystemPrompts.Main);
            newHistory.AddSystemMessage(SystemPrompts.GetLanguageInstruction(language));
            return newHistory;
        });
    }
}
