# Common functions for llama-server scripts

function Get-DefaultPaths {
    param(
        [string]$BasePath = "F:\",
        [string]$Temp = "F:\Temp",
        [string]$LlamaCache = "F:\llama-cache",
        [string]$HfCache = "F:\hf-cache",
        [string]$NvidiaCache = "F:\NVIDIA-cache",
        [string]$Models = "F:\models"
    )
    return @{
        Temp = $Temp
        LlamaCache = $LlamaCache
        HfCache = $HfCache
        NvidiaCache = $NvidiaCache
        Models = $Models
    }
}

function Setup-Environment {
    param(
        [string]$Temp,
        [string]$LlamaCache,
        [string]$HfCache,
        [string]$NvidiaCache,
        [string]$Models
    )
    
    $env:TEMP = $Temp
    $env:TMP = $Temp
    $env:LLAMA_CACHE = $LlamaCache
    $env:HF_HOME = $HfCache
    $env:HUGGINGFACE_HUB_CACHE = Join-Path $HfCache "hub"
    $env:LLAMA_CHAT_TEMPLATE_KWARGS = '{"preserve_thinking": true}'
    $env:CUDA_CACHE_PATH = $NvidiaCache
    $env:CUDA_CACHE_MAXSIZE = "8147483648"
}

function New-CacheDirectories {
    param(
        [string[]]$Directories
    )
    $Directories | ForEach-Object {
        New-Item -ItemType Directory -Force $_ | Out-Null
    }
}

function Get-CpuThreads {
    # Auto-detect physical cores (P-cores) for optimal performance
    $physicalCores = (Get-CimInstance Win32_Processor | 
        Where-Object { $_.NumberOfCores -gt 0 } | 
        Select-Object -ExpandProperty NumberOfCores | 
        Measure-Object -Sum).Sum
    
    # Use ~50% of P-cores for llama-server (leave room for OS)
    return [int]([math]::Round($physicalCores * 0.5))
}

function Validate-Port {
    param([int]$Port)
    if ($Port -lt 1 -or $Port -gt 65535) {
        throw "Invalid port: $Port"
    }
    # Check if port is already in use
    try {
        $null = New-Object System.Net.Sockets.TcpClient("localhost", $Port)
        throw "Port $Port is already in use"
    } catch {
        # Port is free, good to go
    }
}

function Format-ConfigHeader {
    param(
        [string]$Title,
        [double]$Temperature,
        [double]$TopP,
        [int]$TopK,
        [double]$MinP,
        [double]$PresencePenalty,
        [double]$RepetitionPenalty,
        [int]$Port,
        [int]$ContextLength,
        [int]$Predictions,
        [int]$Threads,
        [string]$ModelPath
    )
    
    Write-Host "`n=== $Title ===" -ForegroundColor Cyan
    Write-Host "Temperature:       $Temperature"
    Write-Host "TopP:              $TopP"
    Write-Host "TopK:              $TopK"
    Write-Host "MinP:              $MinP"
    Write-Host "PresencePenalty:   $PresencePenalty"
    Write-Host "RepetitionPenalty: $RepetitionPenalty"
    Write-Host "Port:              $Port"
    Write-Host "Context Length:    $ContextLength"
    Write-Host "Predictions:       $Predictions"
    Write-Host "Threads:           $Threads"
    Write-Host "Model Path:        $ModelPath"
    Write-Host "========================================`n" -ForegroundColor Cyan
}
