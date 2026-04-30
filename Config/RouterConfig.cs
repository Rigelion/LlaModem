using System.Collections.Generic;

namespace LlaModem.Config;

public class RouterConfig
{
    public string ListenUrl { get; set; } = "http://localhost:9000";
    public string AuthUsername { get; set; } = string.Empty;
    public string AuthPassword { get; set; } = string.Empty;
    public bool EnableBodyHeaderInjection { get; set; } = true;
    public Dictionary<string, string> BodyHeaderMappings { get; set; } = new();

    /// <summary>
    /// Configurable thresholds and timeouts for model management.
    /// </summary>
    public TimeoutConfig Timeouts { get; set; } = new();

    public class TimeoutConfig
    {
        /// <summary>Minimum GPU VRAM in GB required to start a model (default: 12).</summary>
        public int VramThresholdGb { get; set; } = 12;

        /// <summary>Timeout in seconds for nvidia-smi queries (default: 5).</summary>
        public int NvidiaSmiTimeoutSeconds { get; set; } = 5;

        /// <summary>Graceful shutdown timeout in seconds before force kill (default: 5).</summary>
        public int GracefulShutdownTimeoutSeconds { get; set; } = 5;

        /// <summary>Timeout in seconds for a single health check request (default: 3).</summary>
        public int HealthCheckTimeoutSeconds { get; set; } = 3;

        /// <summary>Maximum time in minutes to poll for model health (default: 5).</summary>
        public int HealthCheckPollTimeoutMinutes { get; set; } = 5;

        /// <summary>Delay in milliseconds between health check polls (default: 500).</summary>
        public int HealthCheckPollDelayMs { get; set; } = 500;

        /// <summary>Seconds of inactivity before stopping the active model (default: 600).</summary>
        public int IdleTimeoutSeconds { get; set; } = 600;

        /// <summary>Interval in seconds for the idle-check timer (default: 30).</summary>
        public int IdleCheckIntervalSeconds { get; set; } = 30;
    }
}
