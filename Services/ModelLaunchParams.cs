namespace LlaModem.Services;

/// <summary>
/// Optional parameters passed via HTTP headers to override model launch settings.
/// </summary>
public record ModelLaunchParams(
    double? Temperature,
    double? TopP,
    double? TopK,
    double? MinP,
    double? PresencePenalty,
    double? RepetitionPenalty)
{
    public static readonly ModelLaunchParams Defaults = new(
        Temperature: 0.6,
        TopP: 0.95,
        TopK: 20,
        MinP: 0.0,
        PresencePenalty: 0.0,
        RepetitionPenalty: 1.05);
}
