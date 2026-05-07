param(
    [double]$Temperature = 0.6,
    [double]$TopP = 0.95,
    [double]$TopK = 20,
    [double]$MinP = 0.0,
    [double]$PresencePenalty = 0.00,
    [double]$RepetitionPenalty = 1.05
)

$env:TEMP = "F:\Temp"
$env:TMP = "F:\Temp"
$env:LLAMA_CACHE = "F:\llama-cache"
$env:HF_HOME = "F:\hf-cache"
$env:HUGGINGFACE_HUB_CACHE = "F:\hf-cache\hub"
$env:LLAMA_CHAT_TEMPLATE_KWARGS = '{"preserve_thinking": true, "enable_thinking": false}'

$env:CUDA_CACHE_PATH = "F:\NVIDIA-cache"
$env:CUDA_CACHE_MAXSIZE = "8147483648"

New-Item -ItemType Directory -Force F:\Temp, F:\llama-cache, F:\hf-cache, F:\NVIDIA-cache | Out-Null

# Display parameters before starting
Write-Host "`n=== Qwen Smart Server Configuration ===" -ForegroundColor Cyan
Write-Host "Temperature:       $Temperature"
Write-Host "TopP:              $TopP"
Write-Host "TopK:              $TopK"
Write-Host "MinP:              $MinP"
Write-Host "PresencePenalty:   $PresencePenalty"
Write-Host "RepetitionPenalty: $RepetitionPenalty"
Write-Host "Port:              8001"
Write-Host "Context Length:    131072"
Write-Host "Predictions:       4096"
Write-Host "Threads:           6"
Write-Host "Model Path:        F:\models\models--unsloth--Qwen3.6-35B-A3B-GGUF\snapshots\a483e9e6cbd595906af30beda3187c2663a1118c\Qwen3.6-35B-A3B-UD-Q4_K_M.gguf"
Write-Host "========================================`n" -ForegroundColor Cyan

llama-server `
    --model "F:\models\models--unsloth--Qwen3.6-35B-A3B-GGUF\snapshots\a483e9e6cbd595906af30beda3187c2663a1118c\Qwen3.6-35B-A3B-UD-Q4_K_M.gguf" `
    --port 8001 `
    --alias "qwen-smart" `
    -c 131072 `
    -n 4096 `
    --no-context-shift `
    --temp $Temperature `
    --slot-save-path "F:\models\llamacache" `
    --top-p $TopP `
    --top-k $TopK `
    --min-p $MinP `
    --presence-penalty $PresencePenalty `
    --repeat-penalty $RepetitionPenalty `
    --fit on `
    --parallel 1 `
    -fa on `
    -ctk q4_0 `
    -ctv q4_0 `
    --threads 6
