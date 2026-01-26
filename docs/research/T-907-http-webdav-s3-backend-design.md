# T-907: HTTP / WebDAV / S3 Backend — Design

> **Status:** Implemented. WebDavBackend and S3Backend as separate `IContentBackend` impls; HttpBackend unchanged.

---

## One-line

Extend HTTP-style sources with **WebDAV** and **S3-compatible** object storage as `IContentBackend` implementations (separate backends, matching existing HttpBackend pattern).

---

## Implemented

### ContentBackendType

- **`WebDav`** — WebDAV (PROPFIND, GET); BackendRef = full URL; domain allowlist; optional Basic/Bearer.
- **`S3`** — S3-compatible (MinIO, AWS S3, B2); BackendRef = `s3://bucket/key`; bucket allowlist; HeadObject validation.

### WebDavBackend

- **FindCandidatesAsync:** `ISourceRegistry.FindCandidatesForItemAsync(..., ContentBackendType.WebDav)`; filter by `DomainAllowlist` (SSRF). BackendRef = full WebDAV resource URL (http/https).
- **ValidateCandidateAsync:** Allowlist check; HTTP **HEAD**; `MaxFileSizeBytes`, reject empty; optional **Basic** (`Username`/`Password`) or **Bearer** (`BearerToken`) via `WebDavBackendOptions`.
- **WebDavBackendOptions:** `Enabled`, `DomainAllowlist`, `MaxFileSizeBytes`, `ValidationTimeoutSeconds`, `Username`, `Password`, `BearerToken`.

### S3Backend

- **FindCandidatesAsync:** `ISourceRegistry.FindCandidatesForItemAsync(..., ContentBackendType.S3)`; optional filter by `BucketAllowlist`.
- **ValidateCandidateAsync:** Parse `s3://bucket/key`; `BucketAllowlist` if set; **HeadObject** (GetObjectMetadata); `MaxFileSizeBytes`, reject empty.
- **S3BackendOptions:** `Enabled`, `Endpoint` (MinIO/B2; empty = AWS), `Region`, `AccessKey`, `SecretKey`, `ForcePathStyle`, `BucketAllowlist`, `MaxFileSizeBytes`, `ValidationTimeoutSeconds`.
- **Client:** `AWSSDK.S3`; `AmazonS3Client` built from options (ServiceURL when Endpoint set; credentials from options or default chain).

### Resolver / fetch

- **WebDAV:** Resolver must perform **GET** on BackendRef (full URL) with same auth as validation (options or per-request); not implemented in resolver in this task.
- **S3:** Resolver must perform **GetObject** for `s3://bucket/key` using same endpoint/credentials; not implemented in resolver in this task.

---

## BackendRef formats

| Backend   | BackendRef example        | Parsing / use |
|-----------|---------------------------|---------------|
| Http      | `https://cdn.example/f.x` | URL as-is; GET by resolver. |
| WebDav    | `https://dav.example/path/f.flac` | URL as-is; HEAD for validate, GET for fetch. |
| S3        | `s3://my-bucket/path/to/file.flac` | `bucket` = segment before first `/`, `key` = rest. HeadObject/GetObject. |

---

## Security

- **WebDAV:** Domain allowlist (SSRF). Prefer HTTPS. Credentials in options; avoid logging.
- **S3:** Bucket allowlist (optional). Endpoint limits which host we talk to. Credentials via options or IAM/env; avoid logging secrets.

---

## DI and config

- **WebDavBackend:** `IHttpClientFactory`, `IOptionsMonitor<WebDavBackendOptions>`, `ISourceRegistry`. Bind options from e.g. `VirtualSoulfindV2:WebDav` or `Options:WebDav`.
- **S3Backend:** `IOptionsMonitor<S3BackendOptions>`, `ISourceRegistry`. No IAmazonS3 in DI; client built from options in `ValidateCandidateAsync` (and would be in resolver). Bind from `VirtualSoulfindV2:S3` or `Options:S3`.
- Register as `IContentBackend`: `AddSingleton<IContentBackend, WebDavBackend>()`, `AddSingleton<IContentBackend, S3Backend>()` when the host wires v2 backends.

---

## Open / follow-ups

- **Caching:** ETag, Last-Modified, or S3 etag for conditional GET/GetObject; not implemented.
- **Range requests:** For partial fetch; not implemented.
- **Cost/rate limits:** Cloud egress and rate limits; policy/configuration only.
- **Resolver:** Implement fetch for `ContentBackendType.WebDav` (GET + auth) and `ContentBackendType.S3` (GetObject).
- **PROPFIND / ListObjectsV2:** For discovery (browse WebDAV collections or S3 prefixes); currently only registry-backed discovery.
- **Pre-signed S3 URLs:** BackendRef could be a pre-signed URL; then no AccessKey/SecretKey in app. Validate/fetch would use the URL as-is. Design choice: support as optional BackendRef format.

---

## References

- `9-research-design-scope.md` § T-907
- `HttpBackend`, `WebDavBackend`, `S3Backend`; `IContentBackend`, `ISourceRegistry`
- `AWSSDK.S3`, `AmazonS3Client`, `GetObjectMetadataRequest`
