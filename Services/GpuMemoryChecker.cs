using System.Diagnostics;
using System.Globalization;
using LlaModem.Config;
using Microsoft.Extensions.Options;

namespace LlaModem.Services;

/// <summary>
/// Checks available VRAM via nvidia-smi.
/// </summary>
public class GpuMemoryChecker : IGpuMemoryChecker
{
    private readonly RouterConfig.TimeoutConfig _timeouts;

    public GpuMemoryChecker(IOptions<RouterConfig> config)
    {
        _timeouts = config.Value.Timeouts;
    }

    public (bool isSufficient, string? errorMessage) CheckAvailableResources()
    {
        var vramResult = GetFreeVramBytes();
        var minVramBytes = (long)_timeouts.VramThresholdGb * 1024 * 1024 * 1024;

        if (!vramResult.HasValue || vramResult.Value < minVramBytes)
        {
            var available = vramResult.HasValue ? FormatBytes(vramResult.Value) : "unknown";
            return (false, $"Insufficient VRAM: {available} free (need at least {FormatBytes(minVramBytes)})");
        }

        return (true, null);
    }

    /// <summary>
    /// Gets free VRAM in bytes by parsing nvidia-smi output.
    /// Returns null if nvidia-smi is not available.
    /// </summary>
    private long? GetFreeVramBytes()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=memory.free --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            process.WaitForExit(_timeouts.NvidiaSmiTimeoutSeconds * 1000);
            var output = process.StandardOutput.ReadToEnd().Trim();

            if (string.IsNullOrWhiteSpace(output)) return null;

            // Parse the first line (GPU 0)
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var freeMib = long.Parse(lines[0].Trim(), CultureInfo.InvariantCulture);
            return freeMib * 1024 * 1024; // Convert MiB to bytes
        }
        catch
        {
            // nvidia-smi not found, permission denied, etc.
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024 * 1024)
            return $"{bytes / (1024L * 1024 * 1024):F1} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024L * 1024):F0} MB";
        return $"{bytes / 1024:F0} KB";
    }
}
