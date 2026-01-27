#!/usr/bin/env node
/**
 * Generates a manifest.json with sha256 checksums and file sizes for E2E fixtures.
 * This manifest is used to verify fixtures exist and are correct before running E2E tests.
 */

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const FIXTURES_ROOT = path.resolve(__dirname, '..');
const MANIFEST_PATH = path.join(FIXTURES_ROOT, 'meta', 'manifest.json');
const CHECKSUMS_PATH = path.join(FIXTURES_ROOT, 'meta', 'checksums.sha256');

// Files that should be included in the manifest (relative to fixtures root)
// These are the actual media files used in E2E tests (not metadata like posters, licenses)
const REQUIRED_FILES = [
  'book/treasure_island_pg120.txt',
  'movie/sintel_512kb_stereo.mp4',
  'music/open_goldberg/01_aria.ogg',
  'music/open_goldberg/02_variatio_1.ogg',
  'music/open_goldberg/03_variatio_2.ogg',
  'tv/pioneer_one_s01e01_sample.mp4',
];

function sha256File(filePath) {
  const hash = crypto.createHash('sha256');
  const data = fs.readFileSync(filePath);
  hash.update(data);
  return hash.digest('hex');
}

function loadChecksumsFromFile() {
  const checksums = new Map();
  if (fs.existsSync(CHECKSUMS_PATH)) {
    const content = fs.readFileSync(CHECKSUMS_PATH, 'utf-8');
    for (const line of content.split('\n')) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith('#')) continue;
      // Format: sha256  path
      const parts = trimmed.split(/\s+/);
      if (parts.length >= 2) {
        const hash = parts[0];
        const filePath = parts.slice(1).join(' '); // Handle paths with spaces
        checksums.set(filePath, hash);
      }
    }
  }
  return checksums;
}

function generateManifest() {
  const files = [];
  const missing = [];
  const errors = [];
  const checksums = loadChecksumsFromFile();

  for (const relPath of REQUIRED_FILES) {
    const fullPath = path.join(FIXTURES_ROOT, relPath);
    
    if (!fs.existsSync(fullPath)) {
      missing.push(relPath);
      continue;
    }

    try {
      const stats = fs.statSync(fullPath);
      // Try to get sha256 from checksums file first (faster for large files)
      let sha256 = checksums.get(relPath);
      if (!sha256) {
        // Fallback: compute it
        sha256 = sha256File(fullPath);
      }
      
      files.push({
        path: relPath,
        sha256,
        bytes: stats.size,
      });
    } catch (error) {
      errors.push({ path: relPath, error: error.message });
    }
  }

  const manifest = {
    version: 1,
    generated_utc: new Date().toISOString(),
    purpose: 'E2E test fixtures manifest with checksums for offline validation',
    files,
  };

  // Write manifest
  fs.mkdirSync(path.dirname(MANIFEST_PATH), { recursive: true });
  fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 2) + '\n');

  console.log(`Generated manifest with ${files.length} files`);
  
  if (missing.length > 0) {
    console.warn(`\n⚠️  Missing files (not in manifest):`);
    missing.forEach(f => console.warn(`  - ${f}`));
    console.warn('\nRun meta/fetch_media.sh to download missing files, then re-run this script.');
  }

  if (errors.length > 0) {
    console.error(`\n❌ Errors processing files:`);
    errors.forEach(e => console.error(`  - ${e.path}: ${e.error}`));
    process.exit(1);
  }

  return manifest;
}

if (require.main === module) {
  generateManifest();
}

module.exports = { generateManifest };
