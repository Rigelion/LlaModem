using LlaModem.Services;

namespace LlaModem.Models;

/// <summary>
/// Dashboard item for a model (used in API responses).
/// </summary>
public readonly record struct ModelDashboardItem(
    string Name,
    string Status,
    double CurrentTokensPerSecond,
    double? AverageTokensPerSession,
    ModelLaunchParams Parameters,
    string ScriptPath,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastRequestAt,
    bool? IsHealthy,
    string? LastError);

/// <summary>
/// Request to start a model with optional parameter overrides.
/// </summary>
public sealed record StartModelRequest(
    decimal? Temperature = null,
    decimal? TopP = null,
    decimal? TopK = null,
    decimal? MinP = null,
    decimal? PresencePenalty = null,
    decimal? RepetitionPenalty = null)
{
    public ModelLaunchParams ToLaunchParams() => new(
        Temperature ?? Services.ModelLaunchParams.Defaults.Temperature,
        TopP ?? Services.ModelLaunchParams.Defaults.TopP,
        TopK ?? Services.ModelLaunchParams.Defaults.TopK,
        MinP ?? Services.ModelLaunchParams.Defaults.MinP,
        PresencePenalty ?? Services.ModelLaunchParams.Defaults.PresencePenalty,
        RepetitionPenalty ?? Services.ModelLaunchParams.Defaults.RepetitionPenalty);
}

/// <summary>
/// Request to update model parameters.
/// </summary>
public sealed record UpdateModelParamsRequest(
    decimal? Temperature = null,
    decimal? TopP = null,
    decimal? TopK = null,
    decimal? MinP = null,
    decimal? PresencePenalty = null,
    decimal? RepetitionPenalty = null)
{
    public ModelLaunchParams ToLaunchParams() => new(
        Temperature ?? ModelLaunchParams.Defaults.Temperature,
        TopP ?? ModelLaunchParams.Defaults.TopP,
        TopK ?? ModelLaunchParams.Defaults.TopK,
        MinP ?? ModelLaunchParams.Defaults.MinP,
        PresencePenalty ?? ModelLaunchParams.Defaults.PresencePenalty,
        RepetitionPenalty ?? ModelLaunchParams.Defaults.RepetitionPenalty);
}

/// <summary>
/// Response from health check endpoint.
/// </summary>
public readonly record struct HealthCheckResponse(
    bool IsHealthy,
    string HealthUrl,
    DateTimeOffset LastCheckedAt,
    double? ResponseTimeMs);

/// <summary>
/// Response indicating restart is required after parameter update.
/// </summary>
public readonly record struct ParameterUpdateResponse(
    string Message,
    bool RestartRequired,
    ModelLaunchParams UpdatedParams);
