param(
    [Parameter(Mandatory = $true)][string]$ModelScript,
    [Parameter(Mandatory = $false)][string]$ModelParams
)

# Parse ModelParams string into array
$paramArray = if ($ModelParams -and $ModelParams.Trim() -ne "") { $ModelParams.Trim().Split(' ', [StringSplitOptions]::RemoveEmptyEntries) } else { @() }

# Set window title for process identification
$Host.UI.RawUI.WindowTitle = "LlaModem"

# Get directory of the model script
$scriptDir = Split-Path -Parent $ModelScript

Write-Host "[Wrapper] Starting model from: $ModelScript" -ForegroundColor Green
Write-Host "[Wrapper] Script directory: $scriptDir" -ForegroundColor Green
if ($paramArray -and $paramArray.Count -gt 0) {
    Write-Host "[Wrapper] Params: $($paramArray -join ' ')" -ForegroundColor Green
}

# Check if script exists
if (-not (Test-Path $ModelScript)) {
    Write-Host "[Wrapper] ERROR: Script not found: $ModelScript" -ForegroundColor Red
    exit 1
}

# Launch the model script with all parameters
if ($paramArray -and $paramArray.Count -gt 0) {
    # Build command string and invoke it
    $cmd = "$ModelScript $($paramArray -join ' ')"
    Invoke-Expression $cmd
} else {
    & $ModelScript
}

# Exit with the model script's exit code
exit $LASTEXITCODE
