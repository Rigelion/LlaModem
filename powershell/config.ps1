# Centralized configuration for llama-server scripts
# Modify paths here to change all scripts at once

$script:CachePaths = @{
    Temp = "F:\Temp"
    LlamaCache = "F:\llama-cache"
    HfCache = "F:\hf-cache"
    NvidiaCache = "F:\NVIDIA-cache"
    Models = "F:\models"
}

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

# Initialize paths from config
$paths = $script:CachePaths
