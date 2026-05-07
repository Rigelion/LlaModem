namespace LlaModem.Services;

/// <summary>
/// Standard API pricing per 1M tokens (input / output) for major cloud models.
/// Source: costgoat.com/compare/llm-api — May 2026.
/// </summary>
public static class ModelPricing
{
    public record ModelPrice(string Name, double InputPerMillion, double OutputPerMillion);

    public static readonly ModelPrice[] All =
    [
        new("Claude Opus 4.6",    5.00, 25.00),
        new("Claude Sonnet 4.5",  3.00, 15.00),
        new("GPT-5.1 Codex Max",  1.25, 10.00),
        new("Gemini 3 Pro Image", 2.00, 12.00),
        new("Gemini 3 Flash",     0.50,  3.00),
        new("Qwen 3 Max",         1.20,  6.00),
    ];

    public static double CalculateCost(ModelPrice price, long promptTokens, long completionTokens)
    {
        return (promptTokens * price.InputPerMillion + completionTokens * price.OutputPerMillion) / 1_000_000.0;
    }
}
