using System.Collections.Concurrent;
using LlaModem.Models;

namespace LlaModem.Services;

/// <summary>
/// In-memory service for tracking model performance metrics.
/// Provides current t/s (rolling 30s window) and average t/s (per session).
/// </summary>
public sealed class ModelMetricsService
{
    private readonly int _windowSeconds = 30;
    private readonly int _sessionIdleTimeoutSeconds = 600; // 10 minutes
    private readonly ConcurrentDictionary<string, TokenTimeWindow> _windows = new();
    private readonly ConcurrentDictionary<string, ModelSession> _sessions = new();
    private readonly ILogger<ModelMetricsService> _logger;

    public ModelMetricsService(ILogger<ModelMetricsService> logger) => _logger = logger;

    /// <summary>
    /// Records a token usage event for a model.
    /// </summary>
    public void RecordUsage(string modelName, int totalTokens, DateTimeOffset requestTime)
    {
        if (totalTokens <= 0) return;

        // Update rolling window
        var window = _windows.GetOrAdd(modelName, _ => new TokenTimeWindow(_windowSeconds));
        window.AddTokenEvent(requestTime, totalTokens);

        // Update or create session
        var session = _sessions.GetOrAdd(modelName, _ => new ModelSession(modelName, requestTime));
        session.AddTokens(totalTokens);

        // Check for session timeout
        if (session.IsRunning && (requestTime - session.LastRequestAt).TotalSeconds > _sessionIdleTimeoutSeconds)
        {
            EndSession(session.ModelName);
        }
    }

    /// <summary>
    /// Starts a new session for a model (called when model starts).
    /// </summary>
    public void StartSession(string modelName, DateTimeOffset startTime)
    {
        var session = new ModelSession(modelName, startTime);
        _sessions[modelName] = session;
        _logger.LogDebug("Started session for model '{Model}'", modelName);
    }

    /// <summary>
    /// Ends the current session for a model.
    /// </summary>
    public void EndSession(string modelName)
    {
        if (_sessions.TryGetValue(modelName, out var session))
        {
            session.End();
            _logger.LogDebug("Ended session for model '{Model}' — {Tokens} tokens, {Duration}s duration",
                modelName, session.TotalTokens, session.Duration.TotalSeconds);
        }
    }

    /// <summary>
    /// Gets the current tokens per second (rolling 30s window).
    /// </summary>
    public double GetCurrentTokensPerSecond(string modelName)
    {
        if (_windows.TryGetValue(modelName, out var window))
        {
            return window.GetTokensPerSecond();
        }

        return 0;
    }

    /// <summary>
    /// Gets the average tokens per second for the last completed session.
    /// </summary>
    public double? GetAverageTokensPerSession(string modelName)
    {
        if (_sessions.TryGetValue(modelName, out var session))
        {
            if (session.IsCompleted)
            {
                return session.AverageTokensPerSecond;
            }

            // Session still running — calculate current average
            return session.GetCurrentAverageTokensPerSecond();
        }

        return null;
    }

    /// <summary>
    /// Gets the session state for a model.
    /// </summary>
    public ModelSession? GetSession(string modelName)
    {
        return _sessions.TryGetValue(modelName, out var session) ? session : null;
    }

    /// <summary>
    /// Gets the last request timestamp for a model.
    /// </summary>
    public DateTimeOffset? GetLastRequestAt(string modelName)
    {
        if (_sessions.TryGetValue(modelName, out var session))
        {
            return session.LastRequestAt;
        }

        return null;
    }

    /// <summary>
    /// Clears all metrics (useful for testing or reset).
    /// </summary>
    public void Clear()
    {
        _windows.Clear();
        _sessions.Clear();
    }
}

/// <summary>
/// Rolling window for tracking token events.
/// </summary>
public sealed class TokenTimeWindow
{
    private readonly Queue<(DateTimeOffset Time, int Tokens)> _events = new();
    private readonly int _windowSeconds;
    private readonly int _windowMs;

    public TokenTimeWindow(int windowSeconds)
    {
        _windowSeconds = windowSeconds;
        _windowMs = windowSeconds * 1000;
    }

    public void AddTokenEvent(DateTimeOffset time, int tokens)
    {
        _events.Enqueue((time, tokens));
        PruneOldEvents(time);
    }

    public double GetTokensPerSecond()
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMilliseconds(-_windowMs);

        var totalTokens = 0;
        foreach (var (time, tokens) in _events)
        {
            if (time >= windowStart)
            {
                totalTokens += tokens;
            }
        }

        return totalTokens / _windowSeconds;
    }

    private void PruneOldEvents(DateTimeOffset now)
    {
        var windowStart = now.AddMilliseconds(-_windowMs);
        while (_events.Count > 0 && _events.Peek().Time < windowStart)
        {
            _events.Dequeue();
        }
    }
}

/// <summary>
/// Represents a model session (from start to stop/idle timeout).
/// </summary>
public sealed class ModelSession
{
    public string ModelName { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset? EndTime { get; set; }
    public int TotalTokens { get; private set; }
    public DateTimeOffset LastRequestAt { get; private set; }
    public bool IsRunning => EndTime is null;
    public bool IsCompleted => EndTime is not null;
    public TimeSpan Duration
    {
        get
        {
            if (EndTime is { } end)
                return end - StartTime;
            return DateTimeOffset.UtcNow - StartTime;
        }
    }
    public double AverageTokensPerSecond => TotalTokens > 0 ? TotalTokens / Duration.TotalSeconds : 0;

    public ModelSession(string modelName, DateTimeOffset startTime)
    {
        ModelName = modelName;
        StartTime = startTime;
        LastRequestAt = startTime;
    }

    public void AddTokens(int tokens)
    {
        TotalTokens += tokens;
        LastRequestAt = DateTimeOffset.UtcNow;
    }

    public double GetCurrentAverageTokensPerSecond()
    {
        return TotalTokens > 0 ? TotalTokens / Duration.TotalSeconds : 0;
    }

    public void End()
    {
        EndTime = DateTimeOffset.UtcNow;
    }
}
