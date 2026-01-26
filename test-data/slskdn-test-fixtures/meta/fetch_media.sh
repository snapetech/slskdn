#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifest="$root_dir/meta/manifest.json"
target_root="$root_dir"

if ! command -v python3 >/dev/null 2>&1; then
  echo "ERROR: python3 is required to parse the manifest"
  exit 1
fi

if command -v curl >/dev/null 2>&1; then
  dl_kind="curl"
elif command -v wget >/dev/null 2>&1; then
  dl_kind="wget"
else
  echo "ERROR: need curl or wget"
  exit 1
fi

python3 - "$manifest" <<'PY' >"$root_dir/meta/_download_list.tsv"
import json, sys
mf = json.load(open(sys.argv[1], "r", encoding="utf-8"))
rows = []
for a in mf.get("assets", []):
    for d in a.get("download_via_script", []):
        rows.append((d["url"], d["path"]))
for url, path in rows:
    print(url + "\t" + path)
PY

while IFS=$'\t' read -r url relpath; do
  out="$target_root/$relpath"
  outdir="$(dirname "$out")"
  mkdir -p "$outdir"
  if [ -s "$out" ]; then
    echo "OK: already present: $relpath"
    continue
  fi
  echo "DL: $url"
  if [ "$dl_kind" = "curl" ]; then
    curl -L --fail --retry 3 --retry-delay 2 -o "$out" "$url"
  else
    wget -O "$out" "$url"
  fi
done <"$root_dir/meta/_download_list.tsv"

python3 "$root_dir/meta/write_checksums.py"
echo "Done. Checksums written to meta/checksums.sha256"

