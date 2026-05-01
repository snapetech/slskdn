import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const buildDir = path.resolve(scriptDir, '..', 'build');
const indexPath = path.join(buildDir, 'index.html');

function fail(message) {
  console.error(`ERROR: ${message}`);
  process.exit(1);
}

if (!fs.existsSync(indexPath)) {
  fail(`Missing built index.html at ${indexPath}`);
}

const html = fs.readFileSync(indexPath, 'utf8');

const requiredPatterns = [
  { pattern: /(?:src|href)="\.\/assets\//, reason: 'expected relative built asset URLs for reverse-proxy subpaths' },
  { pattern: /href="\.\/favicon\.ico"/, reason: 'expected relative favicon path for reverse-proxy subpaths' },
  { pattern: /href="\.\/manifest\.json"/, reason: 'expected relative manifest path for reverse-proxy subpaths' },
  { pattern: /href="\.\/logo192\.png"/, reason: 'expected relative icon path for reverse-proxy subpaths' },
];

const forbiddenPatterns = [
  { pattern: /(?:src|href)="\/assets\//, reason: 'root-relative assets break non-root web.url_base deployments' },
];

for (const { pattern, reason } of requiredPatterns) {
  if (!pattern.test(html)) {
    fail(`Built index.html is missing an expected path (${pattern}): ${reason}`);
  }
}

for (const { pattern, reason } of forbiddenPatterns) {
  if (pattern.test(html)) {
    fail(`Built index.html contains a forbidden path (${pattern}): ${reason}`);
  }
}

console.log('Verified built web output uses subpath-safe relative asset references.');
