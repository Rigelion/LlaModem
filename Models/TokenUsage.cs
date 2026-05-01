namespace LlaModem.Models;

/// <summary>
/// Token usage from an OpenAI/Ollama API response.
/// </summary>
public record TokenUsage(int PromptTokens, int CompletionTokens, int TotalTokens);
