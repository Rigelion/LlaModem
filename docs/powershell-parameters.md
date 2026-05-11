# PowerShell Parameters - LlaModem

## Overview

LlaModem launches local `llama-server` instances via Windows PowerShell (`powershell.exe`). Parameters can be passed through HTTP headers to override the defaults defined in each model's start script.

---

## HTTP Headers → PowerShell Parameters

The following request headers are read from **all requests** (`/v1/{**path}`) and passed to the PowerShell start script **only on first launch**. Subsequent requests with different header values are logged as warnings (parameters are only applied when the model is started).

| Header                        | Type     | Default  | Description                                                                                                     |
| ----------------------------- | -------- | -------- | --------------------------------------------------------------------------------------------------------------- |
| `X-Llama-Temperature`         | `double` | 0.6      | Sampling temperature for text generation. Lower = more deterministic, higher = more creative.                   |
| `X-Llama-TopP`                | `double` | 0.95     | Nucleus sampling threshold. Only tokens with cumulative probability ≤ this value are considered.                |
| `X-Llama-MinP`                | `double` | 0.0      | Min-p probability threshold. Filters tokens below a probability relative to the most likely token.              |
| `X-Llama-TopK`                | `int`    | 20       | Limits sampling to the top K most probable tokens.                                                              |
| `X-Llama-PresencePenalty`     | `double` | 0.0      | Penalty for reusing tokens that have already appeared in the output. Positive values encourage topic diversity. |
| `X-Llama-RepetitionPenalty`   | `double` | 1.00     | Penalty applied to token probabilities based on how many times they've been generated.                          |

### Example Request

```http
POST /v1/chat/completions
Host: localhost:9000
Authorization: Basic YWRtaW46Y2hhbmdlLW1l
X-Llama-Model: qwen36-smart
X-Llama-Temperature: 0.8
X-Llama-TopP: 0.9
X-Llama-MinP: 0.05
X-Llama-TopK: 20
X-Llama-PresencePenalty: 0.5
X-Llama-RepetitionPenalty: 1.05

{
  "model": "qwen36-smart",
  "messages": [{"role": "user", "content": "Hello!"}]
}
```

### Validation & Error Responses

If a header value is present but **not a valid `double`**, the router returns `400 Bad Request`:

```json
{
  "error": "Bad request",
  "message": "Invalid X-Llama-Temperature value: 'abc'"
}
```

---

## PowerShell Script Parameters

### Optimized Scripts (powershell/ directory)

All scripts now use a shared `common.ps1` module with:
- Auto CPU thread detection (`-Threads 0` = auto-detect)
- Port validation (`-Port 8001`)
- Error handling with exit codes
- Optional verbose output (`-Verbose`)

### Common Parameters (all scripts)

| Parameter              | Type     | Default | Passed via Header                | Description                           |
| ---------------------- | -------- | ------- | -------------------------------- | ------------------------------------- |
| `-Temperature`         | `double` | 0.6     | `X-Llama-Temperature`            | Sampling temperature                  |
| `-TopP`                | `double` | 0.95    | `X-Llama-TopP`                   | Nucleus sampling threshold            |
| `-MinP`                | `double` | 0.0     | `X-Llama-MinP`                   | Min-p probability threshold           |
| `-TopK`                | `int`    | 20      | `X-Llama-TopK`                   | Top-K sampling limit                  |
| `-PresencePenalty`     | `double` | 0.0     | `X-Llama-PresencePenalty`        | Presence penalty for output diversity |
| `-RepetitionPenalty`   | `double` | 1.00    | `X-Llama-RepetitionPenalty`      | Penalty for repeated tokens           |
| `-ContextLength`       | `int`    | varies  | -                                | Context window size                   |
| `-Port`                | `int`    | 8001    | —                                | Server port (all scripts use 8001)   |
| `-Threads`             | `int`    | 0       | -                                | CPU threads (0 = auto-detect)         |
| `-Verbose`             | `switch` | false   | -                                | Show full config                      |

### Auto Thread Detection

Set `-Threads 0` to auto-detect optimal thread count:

```powershell
function Get-CpuThreads {
    # Get physical cores only (P-cores, not E-cores)
    $physicalCores = (Get-CimInstance Win32_Processor |
        Where-Object { $_.NumberOfCores -gt 0 } |
        Select-Object -ExpandProperty NumberOfCores |
        Measure-Object -Sum).Sum

    # Use ~50% of P-cores (leaves 50% for OS + other processes)
    return [int]([math]::Round($physicalCores * 0.5))
}
```

**Example:** On a 16-core CPU, auto-detect returns 8 threads.

### Script Examples

The start script is invoked via:

```powershell
powershell.exe -ExecutionPolicy Bypass -Command "
  $Host.UI.RawUI.WindowTitle = 'qwen36-smart';
  & 'powershell/llama-qwen36-SMART.ps1' -Temperature 0.8 -TopP 0.9 -PresencePenalty 0.5
"
```

### Environment Variables Set by the Script

| Variable                     | Value                                                     | Description             |
| ---------------------------- | --------------------------------------------------------- | ----------------------- |
| `TEMP`                       | `F:\Temp`                                                 | Windows temp directory  |
| `TMP`                        | `F:\Temp`                                                 | Windows temp directory  |
| `LLAMA_CACHE`                | `F:\llama-cache`                                          | llama.cpp model cache   |
| `HF_HOME`                    | `F:\hf-cache`                                             | Hugging Face cache root |
| `HUGGINGFACE_HUB_CACHE`      | `F:\hf-cache\hub`                                         | Hugging Face hub cache  |
| `LLAMA_CHAT_TEMPLATE_KWARGS` | `'{"preserve_thinking": true}'`                           | Chat template settings  |
| `CUDA_CACHE_PATH`            | `F:\NVIDIA-cache`                                         | CUDA compilation cache  |
| `CUDA_CACHE_MAXSIZE`         | `8147483648`                                              | 8 GB max CUDA cache     |

### Directories Created at Startup

The script ensures these directories exist:

- `F:\Temp`
- `F:\llama-cache`
- `F:\hf-cache`
- `F:\NVIDIA-cache`

---

## Model-Specific Scripts

Each model has its own script in the `powershell/` directory:

| Model | Script | Alias | Context |
|-------|--------|-------|---------|  
| Qwen3.6 35B-A3B | llama-qwen36-SMART.ps1 | qwen36-smart | 80K |
| Qwen3.6 35B-A3B | llama-qwen36-OPTIMIZED.ps1 | qwen36-optimized | 65K |
| Qwen3.5 35B-A3B | qwen35-35B-A3B-BYTESHAPE.ps1 | qwen35-35b | 100K |
| Qwen3.5 9B | qwen35-9B-Byteshape.ps1 | qwen35-9b | 65K |
| Qwen3-Coder 30B | qwen3-CODER-30B-A3B-BYTESHAPE.ps1 | qwen3-coder | 202K |

---

## Hardcoded llama-server Arguments

These arguments are **not** exposed as overridable parameters. They are fixed in the start script:

| Argument             | Value                                                 | Description                           |
| -------------------- | ----------------------------------------------------- | ------------------------------------- |
| `--model`            | `F:\models\...`                                       | Path to GGUF model file               |
| `--port`             | `8001` (all scripts)                                    | llama-server listening port           |
| `--alias`            | `qwen36-smart` (varies per model)                     | Process alias                         |
| `-c`                 | varies                                                | Context length (tokens)               |
| `-n`                 | `4096`                                                | Max tokens to predict per request     |
| `--no-context-shift` | -                                                     | Disable context shift on long prompts |
| `--slot-save-path`   | `F:\models\llamacache`                                | KV cache slot save path               |
| `--top-k`            | `20`                                                  | Top-K sampling                        |
| `--repeat-penalty`   | varies                                                | Repeat penalty                        |
| `--fit`              | `on`                                                  | Enable context fitting                |
| `--parallel`         | `1`                                                   | Parallel sequences                    |
| `-fa`                | `on`                                                  | Flash attention                       |
| `-ctk`               | varies                                                | Context key tensor type               |
| `-ctv`               | varies                                                | Context value tensor type             |
| `--threads`          | `0` (auto-detect)                                     | CPU threads for inference             |

---

## Adding a New Model Parameter

To expose a new llama-server argument as an overridable parameter:

1. **Add constant to `Config/ProxyHeaders.cs`** - define the header name (e.g., `public const string TopK = "X-Llama-TopK";`)
2. **Add to `ModelLaunchParams.cs`** - add the new nullable property (e.g., `double? TopK`)
3. **Update `Services/DefaultModelLauncher.StartAsync()`** - append the new param to `paramParts`
4. **Update `Services/LaunchParamParser.ParseAsync()`** - parse the header via `TryParseDoubleHeader` with the constant
5. **Update the PowerShell script** - accept the parameter and pass it to `llama-server`

---

## Process Management

LlaModem tracks PowerShell processes by:

1. Setting `$Host.UI.RawUI.WindowTitle` to the model name before launching the script
2. `IsModelRunning()` checks for `powershell.exe` processes whose `MainWindowTitle` contains the model name
3. `StopAsync()` performs a **tree kill**: kills child processes first (via WMI), then the PowerShell process with a 5s grace period

> **Note:** Only `powershell.exe` (Windows PowerShell) is tracked -- `pwsh` (PowerShell Core) is never used or monitored.
