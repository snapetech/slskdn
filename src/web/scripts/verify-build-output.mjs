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

const forbiddenPatterns = [
  { pattern: /(?:src|href)="\/assets\//, reason: 'root-relative /assets URLs break web.url_base subpath hosting' },
  { pattern: /href="\/manifest\.json"/, reason: 'root-relative manifest path breaks subpath hosting' },
  { pattern: /href="\/logo192\.png"/, reason: 'root-relative icon path breaks subpath hosting' },
];

for (const { pattern, reason } of forbiddenPatterns) {
  if (pattern.test(html)) {
    fail(`Built index.html contains a forbidden path (${pattern}): ${reason}`);
  }
}

const requiredPatterns = [
  { pattern: /(?:src|href)="\.\/assets\//, reason: 'expected relative built asset URL' },
  { pattern: /href="\.\/manifest\.json"/, reason: 'expected relative manifest path' },
];

for (const { pattern, reason } of requiredPatterns) {
  if (!pattern.test(html)) {
    fail(`Built index.html is missing an expected path (${pattern}): ${reason}`);
  }
}

console.log('Verified built web output uses subpath-safe relative asset references.');
