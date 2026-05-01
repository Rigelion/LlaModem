# PowerShell Parameters — LlaModem

## Overview

LlaModem launches local `llama-server` instances via Windows PowerShell (`powershell.exe`). Parameters can be passed through HTTP headers to override the defaults defined in each model's start script.

---

## HTTP Headers → PowerShell Parameters

The following request headers are read from **all requests** (`/v1/{**path}`) and passed to the PowerShell start script **only on first launch**. Subsequent requests with different header values are logged as warnings (parameters are only applied when the model is started).

| Header                        | Type     | Default  | Description                                                                                                     |
| ----------------------------- | -------- | -------- | --------------------------------------------------------------------------------------------------------------- |
| `X-Llama-Temperature`         | `double` | script   | Sampling temperature for text generation. Lower = more deterministic, higher = more creative.                   |
| `X-Llama-TopP`                | `double` | script   | Nucleus sampling threshold. Only tokens with cumulative probability ≤ this value are considered.                |
| `X-Llama-MinP`                | `double` | script   | Min-p probability threshold. Filters tokens below a probability relative to the most likely token.              |
| `X-Llama-TopK`                | `double` | script   | Limits sampling to the top K most probable tokens.                                                              |
| `X-Llama-PresencePenalty`     | `double` | script   | Penalty for reusing tokens that have already appeared in the output. Positive values encourage topic diversity. |
| `X-Llama-RepetitionPenalty`   | `double` | script   | Penalty applied to token probabilities based on how many times they've been generated.                          |

### Example Request

```http
POST /v1/chat/completions
Host: localhost:9000
Authorization: Basic YWRtaW46Y2hhbmdlLW1l
X-Llama-Model: qwen-smart
X-Llama-Temperature: 0.8
X-Llama-TopP: 0.9
X-Llama-MinP: 0.05
X-Llama-TopK: 20
X-Llama-PresencePenalty: 0.5
X-Llama-RepetitionPenalty: 1.05

{
  "model": "qwen-smart",
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

### `run-qwen-smart.ps1` (example)

The start script is invoked via:

```powershell
powershell.exe -ExecutionPolicy Bypass -Command "
  $Host.UI.RawUI.WindowTitle = 'qwen-smart';
  & 'F:\llama\start-qwen-smart.ps1' -Temperature 0.8 -TopP 0.9 -PresencePenalty 0.5
"
```

### Script Parameters

| Parameter              | Type     | Default | Passed via Header                | Description                           |
| ---------------------- | -------- | ------- | -------------------------------- | ------------------------------------- |
| `-Temperature`         | `double` | script  | `X-Llama-Temperature`            | Sampling temperature                  |
| `-TopP`                | `double` | script  | `X-Llama-TopP`                   | Nucleus sampling threshold            |
| `-MinP`                | `double` | script  | `X-Llama-MinP`                   | Min-p probability threshold           |
| `-TopK`                | `double` | script  | `X-Llama-TopK`                   | Top-K sampling limit                  |
| `-PresencePenalty`     | `double` | script  | `X-Llama-PresencePenalty`        | Presence penalty for output diversity |
| `-RepetitionPenalty`   | `double` | script  | `X-Llama-RepetitionPenalty`      | Penalty for repeated tokens           |

### Environment Variables Set by the Script

| Variable                     | Value                                                     | Description             |
| ---------------------------- | --------------------------------------------------------- | ----------------------- |
| `TEMP`                       | `F:\Temp`                                                 | Windows temp directory  |
| `TMP`                        | `F:\Temp`                                                 | Windows temp directory  |
| `LLAMA_CACHE`                | `F:\llama-cache`                                          | llama.cpp model cache   |
| `HF_HOME`                    | `F:\hf-cache`                                             | Hugging Face cache root |
| `HUGGINGFACE_HUB_CACHE`      | `F:\hf-cache\hub`                                         | Hugging Face hub cache  |
| `LLAMA_CHAT_TEMPLATE_KWARGS` | `'{"preserve_thinking": true, "enable_thinking": false}'` | Chat template settings  |
| `CUDA_CACHE_PATH`            | `F:\NVIDIA-cache`                                         | CUDA compilation cache  |
| `CUDA_CACHE_MAXSIZE`         | `8147483648`                                              | 8 GB max CUDA cache     |

### Directories Created at Startup

The script ensures these directories exist:

- `F:\Temp`
- `F:\llama-cache`
- `F:\hf-cache`
- `F:\NVIDIA-cache`

---

## Hardcoded llama-server Arguments

These arguments are **not** exposed as overridable parameters. They are fixed in the start script:

| Argument             | Value                                                 | Description                           |
| -------------------- | ----------------------------------------------------- | ------------------------------------- |
| `--model`            | `F:\models\models--unsloth--Qwen3.6-35B-A3B-GGUF\...` | Path to GGUF model file               |
| `--port`             | `8001`                                                | llama-server listening port           |
| `--alias`            | `qwen-smart`                                          | Process alias                         |
| `-c`                 | `131072`                                              | Context length (tokens)               |
| `-n`                 | `4096`                                                | Max tokens to predict per request     |
| `--no-context-shift` | —                                                     | Disable context shift on long prompts |
| `--slot-save-path`   | `F:\models\llamacache`                                | KV cache slot save path               |
| `--top-k`            | `20`                                                  | Top-K sampling                        |
| `--repeat-penalty`   | `1.05`                                                | Repeat penalty                        |
| `--fit`              | `on`                                                  | Enable context fitting                |
| `--parallel`         | `1`                                                   | Parallel sequences                    |
| `-fa`                | `on`                                                  | Flash attention                       |
| `-ctk`               | `q4_0`                                                | Context key tensor type               |
| `-ctv`               | `q4_0`                                                | Context value tensor type             |
| `--threads`          | `6`                                                   | CPU threads for inference             |

---

## Model-Specific Scripts

Each configured model has its own start script. The pattern is:

```
Config/Models → appsettings.json "StartScript" → PowerShell launch
```

### Adding a New Model Parameter

To expose a new llama-server argument as an overridable parameter:

1. **Add constant to `Config/ProxyHeaders.cs`** — define the header name (e.g., `public const string TopK = "X-Llama-TopK";`)
2. **Add to `ModelLaunchParams.cs`** — add the new nullable property (e.g., `double? TopK`)
3. **Update `Services/DefaultModelLauncher.StartAsync()`** — append the new param to `paramParts`
4. **Update `Services/LaunchParamParser.ParseAsync()`** — parse the header via `TryParseDoubleHeader` with the constant
5. **Update the PowerShell script** — accept the parameter and pass it to `llama-server`

---

## Process Management

LlaModem tracks PowerShell processes by:

1. Setting `$Host.UI.RawUI.WindowTitle` to the model name before launching the script
2. `IsModelRunning()` checks for `powershell.exe` processes whose `MainWindowTitle` contains the model name
3. `StopAsync()` performs a **tree kill**: kills child processes first (via WMI), then the PowerShell process with a 5s grace period

> **Note:** Only `powershell.exe` (Windows PowerShell) is tracked -- `pwsh` (PowerShell Core) is never used or monitored.
