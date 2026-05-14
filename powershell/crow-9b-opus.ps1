# Crow-9B-Opus: 9B parameter model - Qwen3.5 distilled for tool use and reasoning
# Model: https://huggingface.co/mradermacher/Crow-9B-Opus-4.6-Distill-Heretic_Qwen3.5-GGUF
# Context: 128K | Dtype: Qwen3.5 architecture

param(
    [double]$Temperature = 0.4,
    [double]$TopP = 0.95,
    [double]$TopK = 40,
    [double]$MinP = 0.0,
    [double]$PresencePenalty = 0.00,
    [double]$RepetitionPenalty = 1.07,
    [int]$ContextLength = 132768,
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
$ModelPath = "F:\llama-cache\models--Crownelius--Crow-9B-HERETIC-4.6\snapshots\761f0e581222372cf1258efa0ccd56901207e83b\Qwen3.5-9B-heretic-v2.Q5_K_M.gguf"

# Validate model exists
if (-not (Test-Path $ModelPath)) {
    Write-Host "ERROR: Model not found at: $ModelPath" -ForegroundColor Red
    Write-Host "Please download the GGUF quantized version from HuggingFace:" -ForegroundColor Yellow
    Write-Host "  https://huggingface.co/mradermacher/Crow-9B-Opus-4.6-Distill-Heretic_Qwen3.5-GGUF" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Download command:" -ForegroundColor Cyan
    Write-Host "  huggingface-cli download mradermacher/Crow-9B-Opus-4.6-Distill-Heretic_Qwen3.5-GGUF --include 'Crow-9B-Opus-4.6-Distill-Heretic_Qwen3.5-Q4_K_M.gguf' --local-dir '$($paths.Models)'" -ForegroundColor White
    exit 1
}

# Log configuration
if ($Verbose) {
    Format-ConfigHeader -Title "Crow-9B-Opus (Tool Use & Reasoning)" `
        -Temperature $Temperature -TopP $TopP -TopK $TopK -MinP $MinP `
        -PresencePenalty $PresencePenalty -RepetitionPenalty $RepetitionPenalty `
        -Port $Port -ContextLength $ContextLength -Predictions 4096 -Threads $ThreadCount -ModelPath $ModelPath
}

# Start llama-server
try {
    Write-Host "Starting Crow-9B-Opus on port $Port..." -ForegroundColor Green
    
    llama-server `
        --model $ModelPath `
        --port $Port `
        --alias "crow-9b-opus" `
        -c $ContextLength `
        -n 4096 `
		--jinja `
        --no-context-shift `
        --batch-size 1024 `
        --ubatch-size 1024 `
        --parallel 1 `
        --threads $ThreadCount `
        --temp $Temperature `
        --top-p $TopP `
        --top-k $TopK `
        --min-p $MinP `
		--reasoning on `
        --repeat-penalty $RepetitionPenalty `
        --presence-penalty $PresencePenalty `
        --no-mmap `
        -lv 2
    
    Write-Host "Crow-9B-Opus stopped unexpectedly" -ForegroundColor Yellow
} catch {
    Write-Host "Error starting llama-server: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
