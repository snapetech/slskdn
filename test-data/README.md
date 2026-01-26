# test-data

## slskdn-test-fixtures

Public-domain / CC test content for **all domains** (book, movie, music, tv), used for slskdn CI and local test instances.

- **Static files** (in repo): book text, cover/poster/thumb images, license and meta files.
- **Downloaded by script** (not in repo): audio (OGG) and video (MP4) from Project Gutenberg, Internet Archive, Wikimedia. See `slskdn-test-fixtures/.gitignore`.

### Populate downloads

From repo root:

```bash
./scripts/fetch-test-fixtures.sh
```

Requires: `python3`, `curl` or `wget`.

### Use as slskdn share

Point `shares.directories` at the fixtures folder so slskdn can serve, host, and process the content:

```yaml
shares:
  directories:
    - ./test-data/slskdn-test-fixtures
```

See [slskdn-test-fixtures/README.md](slskdn-test-fixtures/README.md) and `meta/manifest.json` for details.
