import * as fs from 'fs/promises';
import * as path from 'path';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

/**
 * Ensure test fixtures are available.
 * 
 * Checks if fixture directories exist and have content.
 * If media files are missing, attempts to fetch them (optional).
 * 
 * @param fixturesDir Path to test-data/slskdn-test-fixtures
 * @param fetchIfMissing If true, run fetch script if media files are missing
 */
export async function ensureFixtures(
  fixturesDir: string = 'test-data/slskdn-test-fixtures',
  fetchIfMissing: boolean = false
): Promise<void> {
  const repoRoot = path.join(process.cwd(), '..', '..', '..');
  const fullFixturesPath = path.isAbsolute(fixturesDir) 
    ? fixturesDir 
    : path.join(repoRoot, fixturesDir);

  // Check if fixtures directory exists
  try {
    await fs.access(fullFixturesPath);
  } catch {
    throw new Error(`Test fixtures directory not found: ${fullFixturesPath}`);
  }

  // Check for manifest
  const manifestPath = path.join(fullFixturesPath, 'meta', 'manifest.json');
  try {
    await fs.access(manifestPath);
  } catch {
    throw new Error(`Test fixtures manifest not found: ${manifestPath}`);
  }

  // Check if key directories exist (static files should always be present)
  const requiredDirs = ['book', 'music', 'movie', 'tv'];
  for (const dir of requiredDirs) {
    const dirPath = path.join(fullFixturesPath, dir);
    try {
      await fs.access(dirPath);
    } catch {
      throw new Error(`Required fixture directory missing: ${dirPath}`);
    }
  }

  // Optionally check for downloaded media files
  // These are optional - tests can work with static files only
  const mediaFiles = [
    'music/open_goldberg/01_aria.ogg',
    'movie/sintel_512kb_stereo.mp4',
    'tv/pioneer_one_s01e01_sample.mp4'
  ];

  const missingMedia: string[] = [];
  for (const mediaFile of mediaFiles) {
    const filePath = path.join(fullFixturesPath, mediaFile);
    try {
      const stat = await fs.stat(filePath);
      if (stat.size === 0) {
        missingMedia.push(mediaFile);
      }
    } catch {
      missingMedia.push(mediaFile);
    }
  }

  if (missingMedia.length > 0 && fetchIfMissing) {
    console.log(`[E2E] Missing ${missingMedia.length} media files, fetching...`);
    try {
      const fetchScript = path.join(repoRoot, 'scripts', 'fetch-test-fixtures.sh');
      await execAsync(`bash "${fetchScript}"`, { cwd: repoRoot });
      console.log('[E2E] Media files fetched successfully');
    } catch (error) {
      console.warn(`[E2E] Failed to fetch media files: ${error}`);
      console.warn('[E2E] Tests will continue with static fixtures only');
    }
  } else if (missingMedia.length > 0) {
    console.warn(`[E2E] ${missingMedia.length} media files missing (optional): ${missingMedia.join(', ')}`);
    console.warn('[E2E] Tests will use static fixtures only. Run ./scripts/fetch-test-fixtures.sh to download media.');
  }
}
