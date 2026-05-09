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
    double? Temperature = null,
    double? TopP = null,
    double? TopK = null,
    double? MinP = null,
    double? PresencePenalty = null,
    double? RepetitionPenalty = null)
{
    public ModelLaunchParams ToLaunchParams() => new(
        Temperature, TopP, TopK, MinP, PresencePenalty, RepetitionPenalty);
}

/// <summary>
/// Request to update model parameters.
/// </summary>
public sealed record UpdateModelParamsRequest(
    double? Temperature = null,
    double? TopP = null,
    double? TopK = null,
    double? MinP = null,
    double? PresencePenalty = null,
    double? RepetitionPenalty = null);

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
