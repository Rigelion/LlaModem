# Centralized configuration for llama-server scripts
# Modify paths here to change all scripts at once

class CachePaths {
    [string]$Temp
    [string]$LlamaCache
    [string]$HfCache
    [string]$NvidiaCache
    [string]$Models
}

$script:CachePaths = [CachePaths]::new()
$script:CachePaths.Temp = "F:\Temp"
$script:CachePaths.LlamaCache = "F:\llama-cache"
$script:CachePaths.HfCache = "F:\hf-cache"
$script:CachePaths.NvidiaCache = "F:\NVIDIA-cache"
$script:CachePaths.Models = "F:\models"

$script:DefaultParams = @{
    Temperature = 0.6
    TopP = 0.95
    TopK = 20
    MinP = 0.0
    PresencePenalty = 0.00
    RepetitionPenalty = 1.00
    ContextLength = 65536
    Port = 8001
}

# Load config into all scripts
. $PSScriptRoot\common.ps1
