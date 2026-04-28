using System.Diagnostics;
using System.Globalization;

namespace LlaModem.Services;

/// <summary>
/// Checks available VRAM (via nvidia-smi) and system RAM on Windows.
/// </summary>
public class GpuMemoryChecker : IGpuMemoryChecker
{
    private const long MinimumVramBytes = 12L * 1024 * 1024 * 1024; // 12 GB
    private const long MinimumRamBytes = 20L * 1024 * 1024 * 1024;   // 20 GB

    public (bool isSufficient, string? errorMessage) CheckAvailableResources()
    {
        var vramResult = GetFreeVramBytes();
        var ramResult = GetFreeRamBytes();

        var messages = new List<string>();

        if (!vramResult.HasValue || vramResult.Value < MinimumVramBytes)
        {
            var available = vramResult.HasValue ? FormatBytes(vramResult.Value) : "unknown";
            messages.Add($"Insufficient VRAM: {available} free (need at least {FormatBytes(MinimumVramBytes)})");
        }

        if (!ramResult.HasValue || ramResult.Value < MinimumRamBytes)
        {
            var available = ramResult.HasValue ? FormatBytes(ramResult.Value) : "unknown";
            messages.Add($"Insufficient RAM: {available} free (need at least {FormatBytes(MinimumRamBytes)})");
        }

        if (messages.Count == 0)
            return (true, null);

        return (false, string.Join("; ", messages));
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

            process.WaitForExit(5000);
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

    /// <summary>
    /// Gets free system RAM in bytes using OS-specific commands.
    /// </summary>
    private long? GetFreeRamBytes()
    {
        try
        {
            // Try macOS / BSD first (free -b)
            var macosResult = TryRunCommand("free", "-b");
            if (!string.IsNullOrEmpty(macosResult))
            {
                return ParseFreeOutput(macosResult);
            }

            // Try Linux (free -b)
            var linuxResult = TryRunCommand("free", "-b");
            if (!string.IsNullOrEmpty(linuxResult))
            {
                return ParseFreeOutput(linuxResult);
            }

            // Fallback: Windows PowerShell
            var psResult = TryRunPowerShell(
                "$mem = Get-CimInstance Win32_OperatingSystem; " +
                "$freeBytes = $mem.FreePhysicalMemory * 1024; " +
                "Write-Output $freeBytes");
            if (!string.IsNullOrEmpty(psResult))
            {
                return long.Parse(psResult.Trim(), CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            // All methods failed
        }

        return 0;
    }

    private long? ParseFreeOutput(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return 0;

        // "              total        used        free      shared  buff/cache   available"
        // "16842751488  8934685696  2147483648   536870912  5760582144  7908060800"
        var values = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length >= 7)
        {
            // "available" column (last) is what we want
            return long.Parse(values[values.Length - 1], CultureInfo.InvariantCulture);
        }

        return null;
    }

    private string? TryRunCommand(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            process.WaitForExit(5000);
            var output = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private string? TryRunPowerShell(string command)
    {
        try
        {
            var psExe = PowerShellResolver.GetExe();
            var psi = new ProcessStartInfo
            {
                FileName = psExe,
                Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            process.WaitForExit(5000);
            var output = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch
        {
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
