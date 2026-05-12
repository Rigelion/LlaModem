# Squeez-2B: 2B parameter model optimized for LLM agent tool output compression/summarization
# Model: https://huggingface.co/KRLabsOrg/squeez-2b
# Context: 16K | Dtype: bfloat16

param(
    [double]$Temperature = 0.6,
    [double]$TopP = 0.95,
    [double]$TopK = 20,
    [double]$MinP = 0.0,
    [double]$PresencePenalty = 0.00,
    [double]$RepetitionPenalty = 1.05,
    [int]$ContextLength = 16384,
    [int]$Port = 8003,
    [int]$Threads = 0,
    [switch]$Verbose
)

# Load common utilities
. .\common.ps1

# Load centralized config
. .\config.ps1

# Setup cache directories
$paths = $script:CachePaths
New-CacheDirectories -Directories @($paths.Temp, $paths.LlamaCache, $paths.HfCache, $paths.NvidiaCache)
Setup-Environment -Temp $paths.Temp -LlamaCache $paths.LlamaCache -HfCache $paths.HfCache -NvidiaCache $paths.NvidiaCache

# Auto-detect CPU threads
$ThreadCount = if ($Threads -eq 0) { Get-CpuThreads } else { $Threads }

# Validate port availability
Validate-Port -Port $Port -ErrorAction Stop

# Model path - adjust to your local model storage
$ModelPath = "$($paths.Models)\squeez-2b-Q4_K_M.gguf"

# Validate model exists
if (-not (Test-Path $ModelPath)) {
    Write-Host "ERROR: Model not found at: $ModelPath" -ForegroundColor Red
    Write-Host "Please download the GGUF quantized version from HuggingFace:" -ForegroundColor Yellow
    Write-Host "  https://huggingface.co/KRLabsOrg/squeez-2b" -ForegroundColor Yellow
    exit 1
}

# Log configuration
if ($Verbose) {
    Format-ConfigHeader -Title "Squeez-2B (Summarization)" `
        -Temperature $Temperature -TopP $TopP -TopK $TopK -MinP $MinP `
        -PresencePenalty $PresencePenalty -RepetitionPenalty $RepetitionPenalty `
        -Port $Port -ContextLength $ContextLength -Predictions 4096 -Threads $ThreadCount -ModelPath $ModelPath
}

# Start llama-server
try {
    Write-Host "Starting Squeez-2B on port $Port..." -ForegroundColor Green
    
    llama-server `
        --model $ModelPath `
        --port $Port `
        --alias "squeez-2b" `
        -c $ContextLength `
        -n 4096 `
        --no-context-shift `
        --batch-size 1024 `
        --ubatch-size 1024 `
        --parallel 1 `
        --threads $ThreadCount `
        --temp $Temperature `
        --top-p $TopP `
        --top-k $TopK `
        --min-p $MinP `
        --repeat-penalty $RepetitionPenalty `
        --presence-penalty $PresencePenalty `
        --no-mmap `
        -lv 2
    
    Write-Host "Squeez-2B stopped unexpectedly" -ForegroundColor Yellow
} catch {
    Write-Host "Error starting llama-server: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
