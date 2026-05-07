namespace LlaModem.Models;

/// <summary>
/// Token usage from an OpenAI/Ollama API response.
/// </summary>
public record TokenUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    double? PromptMs = null,
    double? CompletionMs = null,
    double? PromptPerTokenMs = null,
    double? CompletionPerTokenMs = null,
    int? CacheHits = null)
{
    public Timings? Timings => (PromptMs.HasValue || CompletionMs.HasValue || PromptPerTokenMs.HasValue || CompletionPerTokenMs.HasValue || CacheHits.HasValue)
        ? new(PromptMs, CompletionMs, PromptPerTokenMs, CompletionPerTokenMs, CacheHits)
        : null;
}

/// <summary>
/// Per-request timing data from llama-server.
/// </summary>
public readonly record struct Timings(
    double? PromptMs,
    double? CompletionMs,
    double? PromptPerTokenMs,
    double? CompletionPerTokenMs,
    int? CacheHits);

