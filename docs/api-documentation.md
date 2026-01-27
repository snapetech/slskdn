# API Documentation

Complete reference for slskdN's REST API.

## Base URL

All API endpoints are prefixed with `/api/v0/` (API version 0).

**Base URL**: `http://localhost:5000/api/v0/`

## Authentication

slskdN supports multiple authentication methods:

### Cookie-Based Authentication (Web UI)

- Default authentication method for web interface
- Session cookies set after login
- CSRF protection enabled for mutating operations

### JWT Authentication (API Clients)

- Obtain JWT token via `/api/v0/session` endpoint
- Include in `Authorization: Bearer <token>` header
- No CSRF protection required

### API Key Authentication (External Tools)

- Configure API key in settings
- Include in `X-API-Key: <key>` header
- No CSRF protection required

## API Versioning

slskdN uses URL-based versioning: `/api/v{version}/`

- **Current version**: `v0`
- **Future versions**: `v1`, `v2`, etc.
- Version specified in route: `[Route("api/v{version:apiVersion}/[controller]")]`

## Response Format

### Success Response

```json
{
  "data": { ... },
  "status": "ok"
}
```

### Error Response (ProblemDetails - RFC 7807)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid request parameters",
  "instance": "/api/v0/endpoint"
}
```

## API Endpoints

### Core APIs

#### Application
- `GET /api/v0/application` - Get application state
- `DELETE /api/v0/application` - Shutdown application (admin only)

#### Server (Soulseek)
- `PUT /api/v0/server` - Connect to Soulseek
- `DELETE /api/v0/server` - Disconnect from Soulseek
- `GET /api/v0/server` - Get server status

#### Session
- `GET /api/v0/session` - Get current session
- `POST /api/v0/session` - Create session (login)
- `DELETE /api/v0/session` - End session (logout)

### Search APIs

#### Searches
- `POST /api/v0/searches` - Start a new search
- `GET /api/v0/searches` - List all searches
- `GET /api/v0/searches/{id}` - Get search details
- `GET /api/v0/searches/{id}/responses` - Get search responses
- `PUT /api/v0/searches/{id}` - Update search
- `DELETE /api/v0/searches/{id}` - Cancel/delete search
- `DELETE /api/v0/searches` - Clear all searches

#### Search Actions
- `POST /api/v0/searches/{searchId}/items/{itemId}/download` - Download search result
- `POST /api/v0/searches/{searchId}/items/{itemId}/stream` - Stream search result (Pod/Mesh only)

### Transfer APIs

#### Downloads
- `GET /api/v0/transfers/downloads` - List downloads
- `GET /api/v0/transfers/downloads/{username}/{id}` - Get download details
- `GET /api/v0/transfers/downloads/{username}/{id}/position` - Get queue position
- `DELETE /api/v0/transfers/downloads/{username}/{id}` - Cancel download

#### Uploads
- `GET /api/v0/transfers/uploads` - List uploads
- `GET /api/v0/transfers/uploads/{username}/{id}` - Get upload details
- `DELETE /api/v0/transfers/uploads/{username}/{id}` - Cancel upload

### Multi-Source / Swarm APIs

#### Swarm Downloads
- `POST /api/v0/multisource/swarm` - Start swarm download
- `POST /api/v0/multisource/swarm/async` - Start swarm download (async)
- `GET /api/v0/multisource/jobs/{jobId}` - Get swarm job status
- `GET /api/v0/multisource/jobs` - List active swarm jobs

#### Swarm Tracing
- `GET /api/v0/traces/{jobId}/summary` - Get swarm trace summary with peer contributions

#### Swarm Fairness
- `GET /api/v0/fairness/summary` - Get fairness/contribution summary

### Job APIs

#### Jobs
- `GET /api/jobs` - List all jobs (filterable, sortable, paginated)
- `GET /api/jobs/{id}` - Get job details
- `POST /api/jobs/mb-release` - Create MusicBrainz release job
- `POST /api/jobs/discography` - Create discography download job
- `POST /api/jobs/label-crate` - Create label crate download job

### User APIs

#### Users
- `GET /api/v0/users` - List users
- `GET /api/v0/users/{username}` - Get user details
- `GET /api/v0/users/{username}/group` - Get user group membership
- `GET /api/v0/users/{username}/files` - Browse user files

#### User Notes
- `GET /api/v0/users/{username}/notes` - Get user notes
- `POST /api/v0/users/{username}/notes` - Create/update user note
- `DELETE /api/v0/users/{username}/notes` - Delete user note

### Pod APIs

#### Pods
- `GET /api/v0/pods` - List pods
- `GET /api/v0/pods/{podId}` - Get pod details
- `POST /api/v0/pods` - Create pod
- `PUT /api/v0/pods/{podId}` - Update pod
- `DELETE /api/v0/pods/{podId}` - Delete pod
- `POST /api/v0/pods/{podId}/join` - Join pod
- `POST /api/v0/pods/{podId}/leave` - Leave pod

#### Pod Messages
- `GET /api/v0/pods/{podId}/channels/{channelId}/messages` - Get messages
- `POST /api/v0/pods/{podId}/channels/{channelId}/messages` - Send message

### Collections & Sharing APIs

#### Collections
- `GET /api/v0/collections` - List collections
- `GET /api/v0/collections/{id}` - Get collection details
- `POST /api/v0/collections` - Create collection
- `PUT /api/v0/collections/{id}` - Update collection
- `DELETE /api/v0/collections/{id}` - Delete collection

#### Share Grants
- `GET /api/v0/share-grants` - List share grants (shared with me)
- `GET /api/v0/share-grants/{id}` - Get share grant details
- `GET /api/v0/share-grants/{id}/manifest` - Get collection manifest
- `POST /api/v0/share-grants/{id}/backfill` - Backfill entire collection

### Mesh APIs

#### Mesh Status
- `GET /api/v0/mesh` - Get mesh status
- `GET /api/v0/mesh/health` - Get mesh health

#### DHT Rendezvous
- `GET /api/v0/dht/status` - Get DHT status

### Hash Database APIs

#### HashDb
- `GET /api/v0/hashdb/stats` - Get database statistics
- `GET /api/v0/hashdb/schema` - Get schema version

### Wishlist APIs

#### Wishlist
- `GET /api/v0/wishlist` - List wishlist items
- `POST /api/v0/wishlist` - Create wishlist item
- `PUT /api/v0/wishlist/{id}` - Update wishlist item
- `DELETE /api/v0/wishlist/{id}` - Delete wishlist item
- `POST /api/v0/wishlist/{id}/run` - Run wishlist search now

### Capabilities APIs

#### Capabilities
- `GET /api/v0/capabilities` - Get peer capabilities

### Streaming APIs

#### Streams
- `GET /api/v0/relay/streams/{contentId}` - Stream content (range requests supported)

### Library Health APIs

#### Library Health
- `GET /api/v0/library-health` - Get library health status
- `POST /api/v0/library-health/scan` - Start library health scan

### Options & Configuration

#### Options
- `GET /api/v0/options` - Get configuration options
- `PUT /api/v0/options` - Update configuration (if remote config enabled)

## Common Patterns

### Pagination

Many list endpoints support pagination:

```
GET /api/v0/endpoint?limit=20&offset=0
```

**Parameters:**
- `limit`: Number of items per page (default: varies by endpoint)
- `offset`: Number of items to skip (default: 0)

**Response:**
```json
{
  "count": 100,
  "items": [ ... ],
  "hasMore": true
}
```

### Filtering

Some endpoints support filtering:

```
GET /api/v0/jobs?type=discography&status=running
```

### Sorting

Some endpoints support sorting:

```
GET /api/v0/jobs?sortBy=created_at&sortOrder=desc
```

**Parameters:**
- `sortBy`: Field to sort by
- `sortOrder`: `asc` or `desc`

### Error Handling

All endpoints return appropriate HTTP status codes:

- `200 OK`: Success
- `201 Created`: Resource created
- `204 No Content`: Success, no content
- `400 Bad Request`: Invalid request
- `401 Unauthorized`: Authentication required
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Resource not found
- `409 Conflict`: Resource conflict
- `500 Internal Server Error`: Server error
- `501 Not Implemented`: Feature not implemented

### Rate Limiting

Some endpoints have rate limiting:
- Mesh gateway: 100 calls/minute per peer (default)
- Service calls: Configurable per service

Rate limit headers (when applicable):
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1640995200
```

## API Discovery

### Swagger/OpenAPI

slskdN does not currently expose Swagger/OpenAPI documentation. API discovery is done via:

1. **Source code**: Controllers in `src/slskd/**/API/**Controller.cs`
2. **Route attributes**: `[Route("api/v{version:apiVersion}/...")]`
3. **HTTP methods**: `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`

### Finding Endpoints

**By feature:**
- Search: `src/slskd/Search/API/Controllers/`
- Transfers: `src/slskd/Transfers/API/Controllers/`
- Pods: `src/slskd/PodCore/API/Controllers/`
- Collections: `src/slskd/Sharing/API/`

**By route pattern:**
```bash
# Find all controllers
grep -r "\[Route" src/slskd/**/API/**Controller.cs

# Find specific endpoint
grep -r "searches" src/slskd/**/API/**Controller.cs
```

## Frontend API Libraries

Frontend uses API client libraries in `src/web/src/lib/`:

- `api.js` - Base API client
- `searches.js` - Search operations
- `transfers.js` - Download/upload operations
- `jobs.js` - Job management
- `slskdn.js` - Combined stats and utilities

**Example usage:**
```javascript
import * as searchesLib from '../lib/searches';

const results = await searchesLib.startSearch('query', {
  providers: ['pod', 'scene'],
  filters: { minBitrate: 320 }
});
```

## WebSocket / SignalR

Some features use SignalR for real-time updates:

- **Search results**: Real-time search result streaming
- **Transfer progress**: Real-time download/upload progress
- **Events**: System events and notifications

**Connection:**
```javascript
import { HubConnectionBuilder } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl('/hubs/events')
  .build();

await connection.start();
```

## Examples

### Start a Search

```bash
curl -X POST http://localhost:5000/api/v0/searches \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "query": "artist album",
    "filters": {
      "minBitrate": 320
    }
  }'
```

### Download a File

```bash
curl -X POST "http://localhost:5000/api/v0/searches/{searchId}/items/{itemId}/download" \
  -H "Authorization: Bearer <token>"
```

### Get Swarm Job Status

```bash
curl http://localhost:5000/api/v0/multisource/jobs/{jobId} \
  -H "Authorization: Bearer <token>"
```

### List Jobs with Filtering

```bash
curl "http://localhost:5000/api/jobs?type=discography&status=running&limit=20&offset=0" \
  -H "Authorization: Bearer <token>"
```

## Best Practices

1. **Use API libraries**: Don't call endpoints directly from components
2. **Handle errors**: Always handle API errors gracefully
3. **Return safe values**: API libs should return empty arrays, not undefined
4. **Use pagination**: Don't fetch all data at once
5. **Respect rate limits**: Don't spam API endpoints
6. **Use appropriate auth**: JWT for API clients, cookies for web UI

## Version History

- **v0**: Current version (initial API)

Future versions will maintain backward compatibility where possible.

---

**See Also:**
- [Getting Started](getting-started.md) - User guide
- [How It Works](HOW-IT-WORKS.md) - Architecture overview
- [Configuration](config.md) - Configuration options
