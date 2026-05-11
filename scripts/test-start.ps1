param(
    [Parameter(Mandatory = $true)][string]$ModelScript,
    [Parameter(Mandatory = $false)][string[]]$ModelParams
)

# Set window title for process identification
$Host.UI.RawUI.WindowTitle = "LlaModem"

# Get directory of the model script
$scriptDir = Split-Path -Parent $ModelScript

Write-Host "[Test Script] Starting model from: $ModelScript" -ForegroundColor Green
Write-Host "[Test Script] Script directory: $scriptDir" -ForegroundColor Green
Write-Host "[Test Script] Params: $($ModelParams -join ' ')" -ForegroundColor Green

# Check if script exists
if (-not (Test-Path $ModelScript)) {
    Write-Host "[Test Script] ERROR: Script not found: $ModelScript" -ForegroundColor Red
    exit 1
}

# Launch the model script with all parameters
& $ModelScript @ModelParams

# Exit with the model script's exit code
exit $LASTEXITCODE
