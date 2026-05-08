# Findings: Total Context Size via Headers

## Architecture

### Existing Launch Parameter Flow
1. **Header constant** defined in `Config/ProxyHeaders.cs` (e.g., `X-Llama-Temperature`)
2. **Parsed** in `Services/LaunchParamParser.ParseAsync()` → returns `ModelLaunchParams?`
3. **Stored** in `Services/ModelLaunchParams` record (immutable record with nullable fields)
4. **Passed** to launch script in `Services/DefaultModelLauncher.StartAsync()` via command-line args
5. **Logged** in `Middleware/RequestLoggingMiddleware` for observability

### Key Files
| File | Role |
|------|------|
| `Config/ProxyHeaders.cs` | Header name constants |
| `Services/ModelLaunchParams.cs` | Immutable record of launch params |
| `Services/LaunchParamParser.cs` | Parses headers into `ModelLaunchParams` |
| `Services/DefaultModelLauncher.cs` | Builds PowerShell command with params |
| `Middleware/RequestLoggingMiddleware.cs` | Logs header values |
| `LlaModem.Tests/LaunchParamParserTests.cs` | Tests for parsing |

### Pattern for Adding a New Launch Param
```csharp
// 1. ProxyHeaders.cs - add constant
public const string NewParam = "X-Llama-NewParam";

// 2. ModelLaunchParams.cs - add field to record
int? NewParam,

// 3. LaunchParamParser.cs - parse header as int
var result = await TryParseIntHeader(...);
if (result.Parsed.HasValue) { newParam = result.Parsed.Value; hasLaunchParams = true; }

// 4. DefaultModelLauncher.cs - add to paramParts
if (launchParams?.NewParam.HasValue == true) paramParts.Add($"-NewParam {launchParams.NewParam}");
```

### Decision from Previous Session
- `max_tokens` / `contextLength` should be a **launch parameter**, NOT a body injection
- Body injection (`HeaderValueInjector`) is for runtime request body modifications
- Launch params are for model startup configuration (temperature, top_p, context_length, etc.)
