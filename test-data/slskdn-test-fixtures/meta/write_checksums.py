#!/usr/bin/env python3
from __future__ import annotations
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "meta" / "checksums.sha256"

def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()

paths: list[Path] = []
for p in ROOT.rglob("*"):
    if not p.is_file():
        continue
    if p.name in {"checksums.sha256", "_download_list.tsv"}:
        continue
    if ".git" in p.parts:
        continue
    if p.suffix == ".zip":
        continue
    paths.append(p)

paths.sort(key=lambda p: str(p.relative_to(ROOT)))

lines = []
for p in paths:
    rel = p.relative_to(ROOT)
    digest = sha256_file(p)
    lines.append(f"{digest}  {rel}")

OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Wrote {len(lines)} entries to {OUT}")

