param(
    [Parameter(Mandatory = $true)][string]$ModelScript,
    [Parameter(Mandatory = $false)][string[]]$ModelParams
)

# Set window title for process identification
$Host.UI.RawUI.WindowTitle = "LlaModem"

# Get directory of the model script
$scriptDir = Split-Path -Parent $ModelScript

# Launch the model script with all parameters
if ($ModelParams -and $ModelParams.Count -gt 0) {
    & $ModelScript @ModelParams
} else {
    & $ModelScript
}

# Exit with the model script's exit code
exit $LASTEXITCODE
