# E2E CORS Fix for Cross-Node Streaming Tests

## Problem

Tests `recipient_streams_video` and `concurrency_limit_blocks_excess_streams` were failing with:
```
Access to fetch at 'http://127.0.0.1:34915/api/v0/streams/...' from origin 'http://127.0.0.1:38619' 
has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource.
```

## Root Cause

1. **Cross-origin requests**: Tests use `page.evaluate()` with `fetch()` to make requests from one node's origin (e.g., `http://127.0.0.1:38619`) to another node's origin (e.g., `http://127.0.0.1:34915`)
2. **CORS not enabled**: E2E node configuration didn't enable CORS, so cross-origin requests were blocked by the browser
3. **Wildcard origin handling**: ASP.NET Core CORS doesn't support `"*"` as a literal origin string - needs `AllowAnyOrigin()`

## Solution

### 1. Enable CORS in E2E Node Configuration
**File**: `src/web/e2e/harness/SlskdnNode.ts`

Added CORS configuration to generated `slskd.yml`:
```yaml
web:
  cors:
    enabled: true
    allowCredentials: false
    allowedOrigins:
      - "*"
    allowedMethods:
      - GET
      - POST
      - PUT
      - DELETE
      - OPTIONS
      - HEAD
      - PATCH
```

### 2. Handle Wildcard Origins in Backend
**File**: `src/slskd/Program.cs`

Modified CORS policy configuration to detect wildcard origins and use `AllowAnyOrigin()`:
```csharp
// Handle wildcard origin for E2E tests (when credentials are disabled)
var hasWildcard = c.AllowedOrigins.Contains("*") || c.AllowedOrigins.Contains("/*");
if (hasWildcard && !c.AllowCredentials)
{
    // E2E tests: allow any origin (no credentials)
    b.AllowAnyOrigin();
}
else
{
    b.WithOrigins(c.AllowedOrigins);
    if (c.AllowCredentials)
        b.AllowCredentials();
}
```

## Why This Works

1. **`AllowAnyOrigin()` with no credentials**: When `allowCredentials: false`, ASP.NET Core allows `AllowAnyOrigin()`, which permits requests from any origin
2. **E2E test isolation**: Each test node runs on a different port, so they have different origins
3. **Streaming API needs CORS**: The streaming endpoint (`/api/v0/streams/{contentId}`) is accessed cross-origin in tests that verify concurrency limits

## Test Impact

- ✅ `concurrency_limit_blocks_excess_streams` - Can now fetch streams cross-origin
- ✅ `recipient_streams_video` - Already uses `request.get()` (no CORS needed), but CORS enabled for consistency

## Security Note

CORS with `AllowAnyOrigin()` is only enabled for E2E tests (when `cors.enabled: true` and `allowedOrigins: ["*"]`). Production deployments should use explicit origin allowlists.
