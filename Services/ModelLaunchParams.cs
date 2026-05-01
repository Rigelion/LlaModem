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
    double? RepetitionPenalty);
