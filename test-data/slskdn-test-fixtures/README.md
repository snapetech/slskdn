# slskdn test fixtures (public domain / Creative Commons)

Test content for all domains (book, movie, music, tv) used by slskdn CI and local test instances:
- parsing / metadata ingestion
- search query handling
- cover-art and text ingestion
- library indexing
- transport and storage (different sizes and file types)

## Static vs downloaded

- **In repo:** book text, cover/poster/thumb images, license and meta files. No binary audio/video.
- **Downloaded by script:** audio (OGG) and video (MP4) from remote sources. These are not committed (see `.gitignore`).

## Populate downloads

From the **slskdn repo root**:

```bash
./scripts/fetch-test-fixtures.sh
```

Or from this directory:

```bash
./meta/fetch_media.sh
```

Requires: `python3`, and `curl` or `wget`. The script fetches from Project Gutenberg, Internet Archive, and Wikimedia as defined in `meta/manifest.json`, and writes `meta/checksums.sha256`.

## slskdn share configuration

Use this directory as a **share** so slskdn can serve, host, and process the content. In `slskd.yml` (or `slskd.example.yml`):

```yaml
shares:
  directories:
    - /path/to/slskdn/test-data/slskdn-test-fixtures
```

Example for a dev config next to the repo:

```yaml
shares:
  directories:
    - ./test-data/slskdn-test-fixtures
```

After `fetch-test-fixtures.sh`, run a share scan (or start slskdn with share scan enabled). The tree `book/`, `movie/`, `music/open_goldberg/`, `tv/` will be indexed and browseable.

## Manifest

`meta/manifest.json` lists sources, `download_via_script` URLs and paths, and suggested test queries.
