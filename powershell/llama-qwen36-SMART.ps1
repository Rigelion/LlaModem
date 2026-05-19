param(
    [decimal]$Temperature = 0.6,
    [decimal]$TopP = 0.95,
    [decimal]$TopK = 20,
    [decimal]$MinP = 0.0,
    [decimal]$PresencePenalty = 0.00,
    [decimal]$RepetitionPenalty = 1.05
)

$env:TEMP = "F:\Temp"
$env:TMP = "F:\Temp"
$env:LLAMA_CACHE = "F:\llama-cache"
$env:HF_HOME = "F:\hf-cache"
$env:HUGGINGFACE_HUB_CACHE = "F:\hf-cache\hub"
$env:LLAMA_CHAT_TEMPLATE_KWARGS = '{"preserve_thinking": true}'

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
    --model "F:\llama-cache\models--cHunter789--Qwen3.6-27B-i1-IQ4_XS-GGUF\snapshots\8f2f875ed8fb2f923941d90f10994b63553e6e3c\Qwen3.6-27B.i1-IQ4_XS-attn_qkv-IQ4_XS.gguf" `
    --port 8001 `
    --alias "qwen-smart" `
    -c 65536 `
    -n 2000 `
    --no-context-shift `
    --batch-size 1024 `
    --ubatch-size 512 `
    --parallel 1 `
    --threads 8 `
    --temp $Temperature `
    --top-p $TopP `
    --top-k $TopK `
    --min-p $MinP `
    --repeat-penalty $RepetitionPenalty `
    --presence-penalty $PresencePenalty `
    --slot-save-path "F:\models\llamacache" `
    --reasoning on `
    -fa on `
    --cache-type-k turbo4 `
    --cache-type-v turbo4 `
    --cache-ram 2048 `
    --no-mmap `
    -lv 2
