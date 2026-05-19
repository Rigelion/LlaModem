---
type: concept
created: 2026-05-19
updated: 2026-05-19
sources: []
status: complete
---

# HeaderValueInjectionPattern

**Description:** Pattern for injecting HTTP headers into request body as JSON fields when communicating with llama-server backends.

## Overview

The `HeaderValueInjector` service maps HTTP header values to JSON body fields, enabling OpenAI-compatible clients to control llama-server parameters through standard headers.

## Implementation

```csharp
public class HeaderValueInjector
{
    public async Task<HttpRequestMessage> InjectAsync(
        HttpRequestMessage request,
        RouterConfig config)
    {
        // Map X-Llama-Temperature -> body.temperature
        // Map X-Llama-TopP -> body.top_p
        // etc.
    }
}
```

## Related Components

- [[entities/RequestForwarder]] - Uses injector to prepare requests
- [[concepts/HeaderValueInjector]] - Implementation details
- [[concepts/LlaModemArchitectureOverview]] - Architecture context
