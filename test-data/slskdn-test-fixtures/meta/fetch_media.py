#!/usr/bin/env python3
from __future__ import annotations
import json
import sys
import time
from pathlib import Path
from urllib.request import Request, urlopen
from urllib.error import URLError, HTTPError

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "meta" / "manifest.json"

def download(url: str, out_path: Path, retries: int = 3) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    if out_path.exists() and out_path.stat().st_size > 0:
        print(f"OK: already present: {out_path.relative_to(ROOT)}")
        return
    attempt = 0
    while True:
        attempt += 1
        try:
            print(f"DL: {url}")
            req = Request(url, headers={"User-Agent": "slskdn-fixtures-fetch/1.0"})
            with urlopen(req, timeout=60) as r:
                data = r.read()
            out_path.write_bytes(data)
            return
        except (HTTPError, URLError, TimeoutError) as e:
            if attempt >= retries:
                raise
            wait = 2 * attempt
            print(f"WARN: download failed (attempt {attempt}/{retries}): {e}. Retrying in {wait}s", file=sys.stderr)
            time.sleep(wait)

def main() -> int:
    mf = json.loads(MANIFEST.read_text(encoding="utf-8"))
    for asset in mf.get("assets", []):
        for d in asset.get("download_via_script", []):
            url = d["url"]
            rel = d["path"]
            out = ROOT / rel
            download(url, out)
    print("OK: downloads complete. Run meta/write_checksums.py to (re)generate checksums.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())

