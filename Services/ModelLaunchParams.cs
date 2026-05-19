namespace LlaModem.Services;

/// <summary>
/// Optional parameters passed via HTTP headers to override model launch settings.
/// </summary>
public record ModelLaunchParams(
    decimal? Temperature,
    decimal? TopP,
    decimal? TopK,
    decimal? MinP,
    decimal? PresencePenalty,
    decimal? RepetitionPenalty)
{
    public static readonly ModelLaunchParams Defaults = new(
        Temperature: 0.6m,
        TopP: 0.95m,
        TopK: 20m,
        MinP: 0.0m,
        PresencePenalty: 0.0m,
        RepetitionPenalty: 1.05m);
}
