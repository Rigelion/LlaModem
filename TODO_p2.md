# Phase 2: Basic Auth Middleware

## Goal
Require HTTP Basic Authentication on all `/v1/*` routes and reject unauthenticated requests.

## Tasks

### 2.1 Auth Middleware Implementation
- [ ] Create `Middleware/BasicAuthMiddleware.cs` that implements HTTP Basic Auth checking
- [ ] Middleware reads the `Authorization` header from incoming requests
- [ ] Parse the base64-encoded `username:password` credentials
- [ ] Compare against configured `AuthUsername` and `AuthPassword` (constant-time comparison to prevent timing attacks)
- [ ] If auth is missing or invalid, return `401 Unauthorized` with `WWW-Authenticate` header
- [ ] If auth is valid, pass the request through via `_next(context)`

### 2.2 Auth Extension & Registration
- [ ] Create `Middleware/BasicAuthExtensions.cs` with an `IEndpointRouteBuilder` extension method
- [ ] Register the middleware in `Program.cs` for `/v1/*` routes only
- [ ] Use `app.MapWhen()` or endpoint-level grouping (`map.Map("/v1/*")`) to scope auth

### 2.3 Security Considerations
- [ ] Avoid logging the full `Authorization` header anywhere
- [ ] Log only that authentication was attempted (without credential details)
- [ ] Use `PasswordHasher` or simple string comparison with `SequenceEqual` for constant-time check

## Notes
- Basic Auth is used per the user's config — not JWT or OAuth
- Auth applies only to `/v1/*` routes; health/ping endpoints can remain unauthenticated if needed later
- Credentials come from `appsettings.json`
