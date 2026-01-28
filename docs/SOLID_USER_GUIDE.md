# Solid Integration User Guide

This guide explains how to use slskdn's Solid integration for WebID resolution and Solid-OIDC support.

## Overview

slskdn includes optional Solid integration that allows you to:
- Resolve WebID profiles to discover OIDC issuers
- Serve a Solid-OIDC compliant Client ID document
- Prepare for future Pod-backed metadata storage (playlists, sharelists)

**Note**: This is a minimal MVP implementation. Full OIDC authentication flows and Pod metadata read/write will be added in future releases.

---

## Quick Start

### 1. Enable the Feature

The Solid feature is **enabled by default** (`feature.Solid: true`), but it won't work until you configure allowed hosts for security reasons.

### 2. Configure Allowed Hosts

Edit your `slskd.yml` configuration file and add at least one hostname to `solid.allowedHosts`:

```yaml
feature:
  Solid: true  # Already enabled by default

solid:
  allowedHosts:
    - "solidcommunity.net"
    - "inrupt.net"
    - "your-pod-provider.example"
  timeoutSeconds: 10
  maxFetchBytes: 1000000
  allowInsecureHttp: false  # Keep false in production
  redirectPath: "/solid/callback"
```

**Important Security Note**: 
- Empty `allowedHosts: []` = **deny all remote fetches** (SSRF protection)
- You **must** add at least one hostname for the feature to work
- Only add hostnames you trust (your Solid IDP, Pod provider, etc.)

### 3. Restart slskdn

After updating your configuration, restart slskdn:

```bash
sudo systemctl restart slskd  # systemd
# or
docker-compose restart slskdn  # Docker
```

---

## Using the Web UI

### Accessing Solid Settings

1. Open the slskdn web interface (default: http://localhost:5030)
2. Click **"Solid"** in the navigation menu (key icon 🔑)
3. You'll see the Solid settings page

### Checking Status

The Solid settings page shows:
- **Enabled status**: Whether the feature is enabled
- **Client ID**: The URL where your Client ID document is served (`/solid/clientid.jsonld`)
- **Redirect path**: The OIDC callback path (`/solid/callback`)

### Resolving a WebID

1. Enter a WebID URI in the "WebID" field (e.g., `https://yourname.solidcommunity.net/profile/card#me`)
2. Click **"Resolve WebID"**
3. The page will display:
   - The resolved WebID URI
   - Array of OIDC issuer URIs found in the profile

**Example WebID URIs**:
- `https://yourname.solidcommunity.net/profile/card#me`
- `https://yourname.inrupt.net/profile/card#me`
- `https://your-pod.example/profile/card#me`

---

## Using the API

### Check Status

```bash
curl -X GET http://localhost:5030/api/v0/solid/status \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Response**:
```json
{
  "enabled": true,
  "clientId": "/solid/clientid.jsonld",
  "redirectPath": "/solid/callback"
}
```

### Resolve a WebID

```bash
curl -X POST http://localhost:5030/api/v0/solid/resolve-webid \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"webId": "https://yourname.solidcommunity.net/profile/card#me"}'
```

**Response**:
```json
{
  "webId": "https://yourname.solidcommunity.net/profile/card#me",
  "oidcIssuers": [
    "https://solidcommunity.net"
  ]
}
```

**Error Responses**:
- `400 Bad Request`: Invalid WebID URI format
- `400 Bad Request`: SSRF protection blocked the fetch (host not in AllowedHosts, private IP, etc.)
- `404 Not Found`: Solid feature is disabled
- `500 Internal Server Error`: Failed to resolve WebID (network error, parse error, etc.)

---

## Client ID Document

The Solid-OIDC Client ID document is automatically served at:

```
GET /solid/clientid.jsonld
```

This endpoint is **public** (no authentication required) and returns a JSON-LD document that conforms to the Solid-OIDC specification.

**Example Response**:
```json
{
  "@context": "https://www.w3.org/ns/solid/oidc-context.jsonld",
  "client_id": "https://your-slskdn.example/solid/clientid.jsonld",
  "client_name": "slskdn",
  "application_type": "web",
  "redirect_uris": ["https://your-slskdn.example/solid/callback"],
  "scope": "openid webid"
}
```

**Note**: The `client_id` and `redirect_uris` are automatically derived from your request's base URL. You can override `client_id` by setting `solid.ClientIdUrl` in your config.

---

## Configuration Reference

### Feature Flag

```yaml
feature:
  Solid: true  # Enable Solid integration (default: true)
```

### Solid Options

```yaml
solid:
  # REQUIRED: Add at least one hostname for the feature to work
  allowedHosts:
    - "solidcommunity.net"
    - "inrupt.net"
    - "your-pod-provider.example"
  
  # HTTP timeout for WebID/Pod fetches (seconds)
  timeoutSeconds: 10
  
  # Maximum response size (bytes)
  maxFetchBytes: 1000000  # 1 MB
  
  # Allow http:// URLs (ONLY for dev/test, keep false in production)
  allowInsecureHttp: false
  
  # OIDC redirect URI path
  redirectPath: "/solid/callback"
  
  # Optional: Override Client ID URL (leave empty to auto-derive)
  clientIdUrl: ""  # e.g., "https://your-slskdn.example/solid/clientid.jsonld"
```

---

## Security Considerations

### SSRF Protection

The Solid integration includes comprehensive SSRF (Server-Side Request Forgery) protection:

1. **Host Allow-List**: Only hosts in `allowedHosts` can be fetched
2. **HTTPS Enforcement**: Only HTTPS URLs allowed by default (unless `allowInsecureHttp: true`)
3. **Private IP Blocking**: Automatically blocks:
   - `localhost` and `.local` domains
   - RFC1918 private IPs (10.x.x.x, 172.16-31.x.x, 192.168.x.x)
   - Link-local IPs (169.254.x.x)
   - IPv6 loopback, link-local, and unique-local addresses
4. **Response Size Limits**: Prevents memory exhaustion attacks
5. **Timeout Enforcement**: Prevents hanging requests

### Best Practices

1. **Only add trusted hostnames** to `allowedHosts`
2. **Keep `allowInsecureHttp: false`** in production
3. **Use specific hostnames** rather than wildcards
4. **Monitor logs** for blocked fetch attempts
5. **Start with an empty list** and add hosts as needed

---

## Troubleshooting

### "Solid fetch blocked: no AllowedHosts configured"

**Problem**: You haven't added any hostnames to `solid.allowedHosts`.

**Solution**: Add at least one trusted hostname to your configuration:
```yaml
solid:
  allowedHosts:
    - "solidcommunity.net"
```

### "Solid fetch blocked: host 'example.com' not in AllowedHosts"

**Problem**: The WebID hostname isn't in your `allowedHosts` list.

**Solution**: Add the hostname to your configuration:
```yaml
solid:
  allowedHosts:
    - "example.com"  # Add this
```

### "Solid fetch blocked: only https:// allowed"

**Problem**: You're trying to fetch an `http://` URL but `allowInsecureHttp: false`.

**Solution**: 
- Use HTTPS URLs instead, OR
- Set `allowInsecureHttp: true` (ONLY for dev/test)

### "Solid fetch blocked: localhost/.local not allowed"

**Problem**: You're trying to fetch from localhost or a `.local` domain.

**Solution**: This is blocked for security. Use a public hostname or configure a test server with a real domain.

### WebID Resolution Returns Empty OIDC Issuers

**Problem**: The WebID profile doesn't contain `solid:oidcIssuer` triples.

**Possible Causes**:
- The WebID profile isn't a valid Solid profile
- The profile uses a different vocabulary
- The RDF parsing failed

**Solution**: Verify the WebID profile manually by visiting the URL in a browser and checking for `solid:oidcIssuer` statements.

---

## Examples

### Example 1: Resolve a SolidCommunity.net WebID

**WebID**: `https://alice.solidcommunity.net/profile/card#me`

**Configuration**:
```yaml
solid:
  allowedHosts:
    - "solidcommunity.net"
```

**Result**: Returns OIDC issuer `https://solidcommunity.net`

### Example 2: Resolve an Inrupt Pod WebID

**WebID**: `https://bob.inrupt.net/profile/card#me`

**Configuration**:
```yaml
solid:
  allowedHosts:
    - "inrupt.net"
```

**Result**: Returns OIDC issuer `https://broker.pod.inrupt.com` (or similar)

### Example 3: Self-Hosted Pod

**WebID**: `https://pod.example.com/profile/card#me`

**Configuration**:
```yaml
solid:
  allowedHosts:
    - "pod.example.com"
```

**Result**: Returns the OIDC issuer configured in that Pod's WebID profile

---

## Future Features

The following features are planned for future releases:

- **Full OIDC Flow**: Complete Authorization Code + PKCE authentication flow
- **Token Storage**: Encrypted token storage using ASP.NET Core Data Protection
- **DPoP Support**: DPoP proof generation for Pod requests
- **Pod Metadata**: Read/write playlists, sharelists, and other metadata to Pods
- **Type Index Discovery**: Automatic discovery via Solid Type Index
- **SAI Registry**: Support for Solid Application Interoperability registries
- **Access Control**: WAC (Web Access Control) and ACP (Access Control Policy) writers

---

## Related Documentation

- [Solid Implementation Map](dev/SOLID_IMPLEMENTATION_MAP.md) - Technical implementation details
- [FEATURES.md](FEATURES.md) - Feature overview
- [CHANGELOG.md](../CHANGELOG.md) - Release notes

---

## Getting Help

If you encounter issues:

1. Check the [Troubleshooting](#troubleshooting) section above
2. Review your configuration for common mistakes
3. Check slskdn logs for detailed error messages
4. Open an issue on GitHub with:
   - Your configuration (redact sensitive values)
   - The WebID you're trying to resolve
   - Error messages from logs
   - Steps to reproduce

---

**Last Updated**: 2026-01-28
