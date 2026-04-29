# TODO

## Goal
Inject specified HTTP header values into the root level of JSON request bodies before proxying to backends, with configurable mappings, auto type conversion (string/number/boolean), and overwrite semantics.

## Tasks

### 1. Add configuration model for body header mappings
- [x] Add `Dictionary<string, string>` property `BodyHeaderMappings` to `RouterConfig.cs` mapping header names → body key names
- [x] Update `appsettings.json` with a sample `"BodyHeaderMappings"` section under `Router`

### 2. Create injection service
- [x] Create `Services/HeaderValueInjector.cs` — injects header values into the JSON request body before forwarding
- [x] Auto-detect value types: numeric → number, `true`/`false` → boolean, otherwise string
- [x] Only process requests with Content-Type of `application/json` and a parseable JSON object body
- [x] Overwrite existing keys if they already exist in the body
- [x] Log each injected header at Information level: `[HEADER_INJECT] <header> -> <key>=<value>(<type>)`

### 3. Wire injection service into DI and proxy endpoint
- [x] Register `HeaderValueInjector` as singleton in `Program.cs`
- [x] In `EndpointSetup.cs`, after reading the request body (for buffering/logging), inject header values before creating the forwarded HTTP content

## Notes
- Body is modified at root level only — no nested JSON path support.
- Headers are checked case-insensitively against configured mappings.
- Only headers listed in the config mapping are considered — no wildcard matching.
- If body cannot be parsed as JSON object, injection is silently skipped (no error).
