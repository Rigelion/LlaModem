# Qwen3 Desert Coder MoE 8x0.6B - Fast coding model
# Model: https://huggingface.co/mradermacher/Qwen3-Desert.Coder.MoE-8X0.6B-i1-GGUF
# Context: 16K | Dtype: bfloat16 | MoE architecture

param(
    [decimal]$Temperature = 0.6,
    [decimal]$TopP = 0.95,
    [decimal]$TopK = 20,
    [decimal]$MinP = 0.0,
    [decimal]$PresencePenalty = 0.00,
    [decimal]$RepetitionPenalty = 1.05,
    [int]$ContextLength = 16384,
    [int]$Port = 8001,
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
$ModelPath = "$($paths.Models)\Qwen3-Desert.Coder.MoE-8X0.6B-i1-GGUF\qwen3-desert-coder-8x0.6b-i1-q4_k_m.gguf"

# Validate model exists
if (-not (Test-Path $ModelPath)) {
    Write-Host "ERROR: Model not found at: $ModelPath" -ForegroundColor Red
    Write-Host "Please download the GGUF quantized version from HuggingFace:" -ForegroundColor Yellow
    Write-Host "  https://huggingface.co/mradermacher/Qwen3-Desert.Coder.MoE-8X0.6B-i1-GGUF" -ForegroundColor Yellow
    exit 1
}

# Log configuration
if ($Verbose) {
    Format-ConfigHeader -Title "Qwen3 Desert Coder (MoE 8x0.6B)" `
        -Temperature $Temperature -TopP $TopP -TopK $TopK -MinP $MinP `
        -PresencePenalty $PresencePenalty -RepetitionPenalty $RepetitionPenalty `
        -Port $Port -ContextLength $ContextLength -Predictions 4096 -Threads $ThreadCount -ModelPath $ModelPath
}

# Start llama-server
try {
    Write-Host "Starting Qwen3 Desert Coder on port $Port..." -ForegroundColor Green
    
    llama-server `
        --model $ModelPath `
        --port $Port `
        --alias "qwen3-desert-coder" `
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
    
    Write-Host "Qwen3 Desert Coder stopped unexpectedly" -ForegroundColor Yellow
} catch {
    Write-Host "Error starting llama-server: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
