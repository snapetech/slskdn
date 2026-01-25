# Security Configuration Guide

This document describes how to configure security features in slskdn.

> **Last Updated**: December 2025  
> **Branch**: `experimental/whatAmIThinking`

## Overview

slskdn includes comprehensive security features to protect against common attacks and abuse:

- **Path Traversal Protection**: Blocks directory traversal attacks
- **Rate Limiting**: Prevents DoS attacks and abuse
- **Authentication**: Optional API key and JWT authentication
- **Mesh Gateway Security**: API key and CSRF protection for mesh service gateway
- **Input Validation**: Validates all external inputs
- **Payload Size Limits**: Prevents resource exhaustion

## Default Security Settings

By default, slskdn uses a **secure-by-default** configuration:

- ✅ Authentication **enabled** (requires API key or JWT)
- ✅ Security middleware **enabled**
- ✅ Mesh gateway **disabled** (opt-in feature)
- ✅ Path traversal protection **enabled**
- ✅ Rate limiting **enabled**

## Authentication

### Enabling/Disabling Authentication

In `slskd.yml`:

```yaml
web:
  authentication:
    disabled: false  # Set to true to disable authentication (NOT RECOMMENDED for production)
```

**Warning**: Disabling authentication allows unrestricted access to all API endpoints. Only disable for development or trusted networks.

### API Key Authentication

Set the API key via environment variable:

```bash
export SLSKD_API_KEY=your-secret-api-key
```

Or configure in `slskd.yml`:

```yaml
web:
  authentication:
    api_keys:
      - key: your-secret-api-key
        role: Administrator
        cidr: 0.0.0.0/0  # Allow from any IP (use specific CIDR for production)
```

### JWT Authentication

JWT authentication is used for web UI sessions. Users log in via `/api/v0/session` endpoint.

## Mesh Gateway Security

The mesh gateway exposes mesh services via HTTP. **It is disabled by default** for security.

### Enabling Mesh Gateway

```yaml
MeshGateway:
  Enabled: true
  BindAddress: "127.0.0.1"  # IMPORTANT: Only bind to localhost unless you understand the risks
  Port: 5030
  ApiKey: "your-gateway-api-key"  # REQUIRED if not binding to localhost
  CsrfToken: ""  # Auto-generated if empty
  AllowedServices:
    - "pods"
    - "shadow-index"
  MaxRequestBodyBytes: 1048576  # 1 MB
  RequestTimeoutSeconds: 30
```

### Security Requirements

1. **API Key**: Required for non-localhost access
2. **CSRF Token**: Required for localhost access (prevents browser-based attacks)
3. **Service Allowlist**: Only explicitly listed services can be called
4. **Origin Validation**: Cross-origin requests to localhost are blocked by default

### Security Best Practices

- ✅ **Always bind to localhost** unless you have proper firewall rules
- ✅ **Use strong API keys** (generate with `slskd generate-gateway-key`)
- ✅ **Minimize AllowedServices** list (only enable services you need)
- ✅ **Set appropriate timeouts** to prevent resource exhaustion
- ✅ **Monitor gateway access logs** for suspicious activity

## Security Middleware

The security middleware provides:

- Path traversal protection
- Rate limiting
- IP banning
- Connection limits
- Payload size limits

### Configuration

```yaml
Security:
  Enabled: true
  NetworkGuard:
    MaxConnectionsPerIp: 10
    MaxMessageSize: 10485760  # 10 MB
  ViolationTracker:
    MaxViolationsPerIp: 5
    BanDurationMinutes: 60
```

### Path Traversal Protection

**Always enabled** - Path traversal protection is active even when other security features are disabled. This is a critical security requirement that should never be bypassed.

The SecurityMiddleware is placed **first** in the ASP.NET Core pipeline (before `UsePathBase` and `UseFileServer`) to ensure it processes the raw request path before any path rewriting occurs.

Blocks paths containing:
- `../` sequences (plain traversal)
- URL-encoded traversal (`..%2F`, `%2e%2e`)
- Double-encoded traversal (`%252e%252e`)
- Suspicious system paths (`/etc/passwd`, `/etc/shadow`, `/proc/`, etc.)

**Configuration:**
```yaml
security:
  enabled: true  # Path traversal protection is ALWAYS enabled, even if this is false
  profile: Standard  # Minimal, Standard, Maximum, or Custom
```

**Note**: Even when `security.enabled: false`, path traversal protection remains active. Only advanced security features (rate limiting, violation tracking, etc.) are disabled.

### Rate Limiting

Rate limiting is applied per IP address:
- Connection limits: Max connections per IP
- Request rate: Configurable requests per minute
- Violation tracking: Automatic IP banning after repeated violations

## Input Validation

All external inputs are validated:

- **File paths**: Validated against root directory, no traversal allowed
- **Request payloads**: Size limits enforced
- **Service names**: Must be in allowlist
- **API keys**: Constant-time comparison to prevent timing attacks

## Security Features by Component

### Service Fabric

- **Service Discovery**: Ed25519 signatures prevent spoofing
- **Rate Limiting**: Per-peer and per-service call limits (100 calls/min default)
- **Work Budget**: Universal work unit system prevents amplification
- **Payload Size Limits**: Configurable max payload size (1MB default)

### VirtualSoulfind

- **Soulseek Safety**: Compile-time gating prevents non-music abuse
- **Rate Caps**: Configurable searches/browses per minute
- **Work Budget**: All operations consume budget

### Proxy/Relay Services

- **Catalogue Fetch**: Domain allowlist, SSRF protection, method restrictions
- **Content Relay**: Content ID mapping (no arbitrary file access)
- **Trusted Relay**: Peer allowlist, service allowlist

## Security Checklist

Before deploying to production:

- [x] Authentication enabled (`authentication.disabled: false`)
- [x] Strong API keys configured
- [x] Mesh gateway disabled (or properly secured if enabled)
- [x] Firewall rules configured
- [x] Rate limits appropriate for your use case
- [x] Security middleware enabled
- [x] Logging enabled for security events
- [x] Regular security updates applied

## Troubleshooting

### API Returns 401 Even With Auth Disabled

If you've disabled authentication but still get 401 errors:

1. Check that `authentication.disabled: true` is set correctly
2. Restart the application after changing config
3. Check logs for authentication errors

### Mesh Gateway Returns 400 Instead of 404

If the mesh gateway returns 400 when disabled:

1. Ensure `MeshGateway.Enabled: false` is set
2. Check that middleware is registered before routing
3. Verify options are loaded correctly

### Path Traversal Not Caught

If path traversal attacks aren't being blocked:

1. **Verify SecurityMiddleware is first in pipeline**: The middleware must be registered before `UsePathBase` and `UseFileServer`
2. **Check configuration file**: Ensure `security.enabled: true` is set in the correct config file (check for duplicate config files)
3. **Verify middleware execution**: Check logs for `[SecurityMiddleware] *** INVOKED ***` messages
4. **Test with curl**: `curl -I "http://localhost:5099/etc/passwd"` should return `400 Bad Request`
5. **Check for UseFileServer short-circuiting**: `UseFileServer` should be placed AFTER `UseEndpoints`, not before

**Common Issues:**
- Duplicate config files: Check both `config/slskd.dev.yml` and `src/slskd/config/slskd.dev.yml`
- Middleware order: SecurityMiddleware must be first in `ConfigureAspDotNetPipeline`
- Static file server: `UseFileServer` placed before routing can short-circuit requests

## Reporting Security Issues

If you discover a security vulnerability, please report it responsibly:

1. **DO NOT** open a public issue
2. Email security@slskdn.org (if available) or use private communication
3. Include detailed steps to reproduce
4. Allow time for the issue to be fixed before public disclosure

## Additional Resources

- [Security Guidelines](./SECURITY-GUIDELINES.md) - Detailed security requirements
- [Features](../../FEATURES.md) - Feature overview and security considerations
- [Testing Strategy](./TESTING-STRATEGY.md) - Security testing approach
