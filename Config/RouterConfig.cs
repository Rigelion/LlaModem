namespace LlaModem.Config;

public record RouterConfig
{
    public string ListenUrl { get; init; } = "http://localhost:9000";
    public string AuthUsername { get; init; } = string.Empty;
    public string AuthPassword { get; init; } = string.Empty;
    public bool EnableBodyHeaderInjection { get; init; } = true;
    public Dictionary<string, string> BodyHeaderMappings { get; init; } = new();

    /// <summary>
    /// Configurable thresholds and timeouts for model management.
    /// </summary>
    public TimeoutConfig Timeouts { get; init; } = new();

    public record TimeoutConfig
    {
        /// <summary>Minimum GPU VRAM in GB required to start a model (default: 12).</summary>
        public int VramThresholdGb { get; init; } = 12;

        /// <summary>Timeout in seconds for nvidia-smi queries (default: 5).</summary>
        public int NvidiaSmiTimeoutSeconds { get; init; } = 5;

        /// <summary>Graceful shutdown timeout in seconds before force kill (default: 5).</summary>
        public int GracefulShutdownTimeoutSeconds { get; init; } = 5;

        /// <summary>Timeout in seconds for a single health check request (default: 3).</summary>
        public int HealthCheckTimeoutSeconds { get; init; } = 3;

        /// <summary>Maximum time in minutes to poll for model health (default: 5).</summary>
        public int HealthCheckPollTimeoutMinutes { get; init; } = 5;

        /// <summary>Delay in milliseconds between health check polls (default: 500).</summary>
        public int HealthCheckPollDelayMs { get; init; } = 500;

        /// <summary>Seconds of inactivity before stopping the active model (default: 600).</summary>
        public int IdleTimeoutSeconds { get; init; } = 600;
    }
}
