# HashDb Schema & Migrations

## Overview

The HashDb is a SQLite database that stores:
- **Peers**: Known Soulseek users and their capabilities
- **FlacInventory**: FLAC files discovered from searches and interactions
- **HashDb**: Content-addressed hash database for file identity verification
- **FileSources**: Multiple sources (peers) for each content hash
- **MeshPeerState**: Mesh sync state for epidemic gossip protocol
- **HashDbState**: Key-value store for miscellaneous state

## Schema Version

Current schema version: **2**

Check current version via API:
```bash
curl http://localhost:5030/api/v0/hashdb/schema
```

Response:
```json
{
  "currentVersion": 2,
  "targetVersion": 2,
  "isUpToDate": true,
  "message": "Schema is up to date"
}
```

## Migration System

The HashDb uses a versioned migration system (`HashDbMigrations.cs`) that:

1. **Tracks schema version** in `__HashDbMigrations` table
2. **Runs pending migrations** automatically on startup
3. **Supports rollback** via transactions (migration failure = rollback)
4. **Is idempotent** - safe to run multiple times

### Migration History

| Version | Name | Description |
|---------|------|-------------|
| 1 | Initial schema | Base tables: Peers, FlacInventory, HashDb, MeshPeerState, HashDbState |
| 2 | Extended schema | Added FileSources table, extended FlacInventory and HashDb with new columns |

### Adding New Migrations

1. Increment `CurrentVersion` in `HashDbMigrations.cs`
2. Add new `Migration` object to `GetMigrations()` list
3. Test migration on existing databases

Example:
```csharp
new Migration
{
    Version = 3,
    Name = "Add audio fingerprinting",
    Apply = conn =>
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            ALTER TABLE HashDb ADD COLUMN fingerprint_version INTEGER;
            CREATE INDEX IF NOT EXISTS idx_hashdb_fingerprint ON HashDb(audio_fingerprint);
        ";
        cmd.ExecuteNonQuery();
    },
},
```

## Table Schemas

### Peers
Tracks known Soulseek users and their slskdn capabilities.

| Column | Type | Description |
|--------|------|-------------|
| peer_id | TEXT PK | Username |
| caps | INTEGER | Capability flags (bitmask) |
| client_version | TEXT | slskdn version string |
| last_seen | INTEGER | Unix timestamp |
| last_cap_check | INTEGER | When capabilities were last probed |
| backfills_today | INTEGER | Rate limiting counter |
| backfill_reset_date | INTEGER | When to reset counter |

### FlacInventory
FLAC files discovered from searches and peer interactions.

| Column | Type | Description |
|--------|------|-------------|
| file_id | TEXT PK | Unique identifier (hash of peer+path+size) |
| peer_id | TEXT | Source peer username |
| path | TEXT | Remote file path |
| size | INTEGER | File size in bytes |
| discovered_at | INTEGER | Unix timestamp |
| hash_status | TEXT | 'none', 'pending', 'known', 'failed' |
| hash_value | TEXT | SHA256 of first 32KB (primary identity) |
| hash_source | TEXT | How hash was obtained |
| flac_audio_md5 | TEXT | MD5 from FLAC STREAMINFO |
| full_file_hash | TEXT | SHA256 of entire file (v2+) |
| sample_rate | INTEGER | Audio sample rate |
| channels | INTEGER | Number of channels |
| bit_depth | INTEGER | Bits per sample |
| duration_samples | INTEGER | Total samples |
| min_block_size | INTEGER | From STREAMINFO (v2+) |
| max_block_size | INTEGER | From STREAMINFO (v2+) |
| encoder_info | TEXT | FLAC encoder string (v2+) |
| album_hash | TEXT | Group files from same album (v2+) |
| probe_fail_count | INTEGER | Probe failure counter (v2+) |
| probe_fail_reason | TEXT | Last failure reason (v2+) |
| last_probe_at | INTEGER | Last probe timestamp (v2+) |

### HashDb
Content-addressed hash database for file identity verification.

| Column | Type | Description |
|--------|------|-------------|
| flac_key | TEXT PK | Unique key (hash of filename+size) |
| byte_hash | TEXT | SHA256 of first 32KB |
| size | INTEGER | File size in bytes |
| meta_flags | INTEGER | Metadata flags |
| first_seen_at | INTEGER | Unix timestamp |
| last_updated_at | INTEGER | Unix timestamp |
| seq_id | INTEGER | Sequence ID for mesh sync |
| use_count | INTEGER | How many times referenced |
| full_file_hash | TEXT | SHA256 of entire file (v2+) |
| audio_fingerprint | TEXT | AcoustID/Chromaprint (v2+, future) |
| musicbrainz_id | TEXT | MusicBrainz recording ID (v2+, future) |

### FileSources (v2+)
Tracks multiple sources (peers) for each content hash, enabling multi-source downloads.

| Column | Type | Description |
|--------|------|-------------|
| content_hash | TEXT | byte_hash from HashDb |
| peer_id | TEXT | Peer username |
| path | TEXT | Remote file path |
| size | INTEGER | File size |
| first_seen | INTEGER | Unix timestamp |
| last_seen | INTEGER | Unix timestamp |
| download_success_count | INTEGER | Successful downloads |
| download_fail_count | INTEGER | Failed downloads |
| avg_speed_bps | INTEGER | Average download speed |
| last_download_at | INTEGER | Last download timestamp |

Primary key: (content_hash, peer_id, path)

### MeshPeerState
Tracks mesh sync state for epidemic gossip protocol.

| Column | Type | Description |
|--------|------|-------------|
| peer_id | TEXT PK | Peer username |
| last_sync_time | INTEGER | Unix timestamp |
| last_seq_seen | INTEGER | Highest seq_id received |

### HashDbState
Key-value store for miscellaneous state.

| Column | Type | Description |
|--------|------|-------------|
| key | TEXT PK | State key |
| value | TEXT | State value |

Used for:
- `backfill_progress`: Current offset in search history backfill

## Hash Types Explained

### byte_hash (Primary Identity)
- **What**: SHA256 of first 32KB (32,768 bytes)
- **Why**: Fast to compute from partial download
- **Use**: Primary identity for multi-source matching

### flac_audio_md5
- **What**: MD5 from FLAC STREAMINFO block (bytes 26-42)
- **Why**: Identifies identical audio content regardless of metadata
- **Use**: Verify sources have same audio before multi-source download

### full_file_hash
- **What**: SHA256 of entire file
- **Why**: Complete verification after download
- **Use**: Post-download integrity check

### audio_fingerprint (Future)
- **What**: AcoustID/Chromaprint fingerprint
- **Why**: Identify same song regardless of encode/master
- **Use**: Deduplication, music identification

## Database Location

Default: `{AppDirectory}/hashdb.sqlite`

Where `AppDirectory` is:
- Linux: `~/.local/share/slskd/`
- macOS: `~/Library/Application Support/slskd/`
- Windows: `%APPDATA%\slskd\`

## Backup & Recovery

### Manual Backup
```bash
cp ~/.local/share/slskd/hashdb.sqlite hashdb-backup-$(date +%Y%m%d).sqlite
```

### Export to JSON (Future)
API endpoint planned for exporting hash database to JSON for mesh sharing.

## Troubleshooting

### Check Schema Version
```bash
curl http://localhost:5030/api/v0/hashdb/schema
```

### Force Migration Re-run
Delete the `__HashDbMigrations` table (not recommended in production):
```sql
DROP TABLE __HashDbMigrations;
```

### Reset Database
Stop slskd, delete `hashdb.sqlite`, restart. Fresh database will be created.

---

*Last updated: 2025-12-09*

