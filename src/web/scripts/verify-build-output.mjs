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
  { pattern: /(?:src|href)="\/assets\//, reason: 'expected root-relative built asset URL for deep-link refreshes' },
  { pattern: /href="\/manifest\.json"/, reason: 'expected root-relative manifest path for backend urlBase rewriting' },
  { pattern: /href="\/logo192\.png"/, reason: 'expected root-relative icon path for backend urlBase rewriting' },
];

for (const { pattern, reason } of requiredPatterns) {
  if (!pattern.test(html)) {
    fail(`Built index.html is missing an expected path (${pattern}): ${reason}`);
  }
}

console.log('Verified built web output uses root-relative asset references for backend urlBase rewriting.');
