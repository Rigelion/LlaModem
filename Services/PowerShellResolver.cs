namespace LlaModem.Services;

/// <summary>
/// Resolves which PowerShell executable is available on the system.
/// Returns "pwsh" (PowerShell Core) if found, otherwise "powershell.exe" (Windows PowerShell).
/// Cached after first resolution.
/// </summary>
public static class PowerShellResolver
{
    private static string? _cachedExe;

    public static string GetExe()
    {
        _cachedExe ??= Resolve();
        return _cachedExe!;
    }

    private static string Resolve()
    {
        // Check pwsh (PowerShell Core) first, then powershell.exe (Windows PowerShell)
        var pwsh = FindExecutable("pwsh");
        if (!string.IsNullOrEmpty(pwsh))
            return pwsh;

        var ps = FindExecutable("powershell");
        if (!string.IsNullOrEmpty(ps))
            return ps;

        // Last resort: just use "pwsh" and let the exception be caught upstream
        return "pwsh";
    }

    private static string? FindExecutable(string name)
    {
        var paths = new[] { name + ".exe", name };
        foreach (var path in paths)
        {
            if (CanExecute(path))
                return path;
        }
        return null;
    }

    private static bool CanExecute(string fileName)
    {
        // If it has an extension or path separator, check File.Exists directly
        if (fileName.Contains('.') || fileName.Contains('/') || fileName.Contains('\\'))
            return File.Exists(fileName);

        // Otherwise search PATH for the executable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
                return true;
        }
        return false;
    }
}
