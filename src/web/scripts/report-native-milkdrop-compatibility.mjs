import { readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { createServer } from 'vite';

const presetExtensions = new Set(['.milk', '.milk2']);

const walkPresetFiles = async (inputPath) => {
  const inputStat = await stat(inputPath);
  if (inputStat.isFile()) {
    return presetExtensions.has(path.extname(inputPath).toLowerCase())
      ? [inputPath]
      : [];
  }

  if (!inputStat.isDirectory()) return [];

  const { readdir } = await import('node:fs/promises');
  const entries = await readdir(inputPath, { withFileTypes: true });
  const nested = await Promise.all(entries.map((entry) =>
    walkPresetFiles(path.join(inputPath, entry.name))));
  return nested.flat();
};

const loadPresetSources = async (inputs) => {
  if (inputs.length === 0) return [];

  const files = (await Promise.all(inputs.map((input) =>
    walkPresetFiles(path.resolve(input))))).flat().sort();

  return Promise.all(files.map(async (fileName) => ({
    fileName,
    format: path.extname(fileName).toLowerCase() === '.milk2' ? 'milk2' : undefined,
    id: path.relative(process.cwd(), fileName),
    source: await readFile(fileName, 'utf8'),
  })));
};

const createViteModuleLoader = async () => {
  const server = await createServer({
    logLevel: 'error',
    server: {
      middlewareMode: true,
    },
  });
  return {
    close: () => server.close(),
    load: (modulePath) => server.ssrLoadModule(modulePath),
  };
};

const args = process.argv.slice(2);
const jsonOutput = args.includes('--json');
const inputs = args.filter((arg) => arg !== '--json');
const loader = await createViteModuleLoader();

try {
  const [
    { nativeMilkdropFixturePack },
    {
      buildMilkdropCompatibilityMatrix,
      summarizeMilkdropCompatibilityMatrix,
    },
  ] = await Promise.all([
    loader.load('/src/components/Player/visualizers/milkdrop/presetFixtures.js'),
    loader.load('/src/components/Player/visualizers/milkdrop/presetCompatibilityMatrix.js'),
  ]);
  const sources = inputs.length > 0
    ? await loadPresetSources(inputs)
    : nativeMilkdropFixturePack;

  const matrix = buildMilkdropCompatibilityMatrix(sources);
  const summary = summarizeMilkdropCompatibilityMatrix(matrix);
  const report = {
    entries: matrix,
    source: inputs.length > 0 ? 'files' : 'curated-fixtures',
    summary,
  };

  if (jsonOutput) {
    console.log(JSON.stringify(report, null, 2));
  } else {
    console.log(`Native MilkDrop compatibility (${report.source})`);
    console.log(
      `${summary.supportedCount}/${summary.totalCount} files supported; `
      + `${summary.presetCount} preset bodies scanned.`,
    );
    console.log(
      `Max counts: shapes=${summary.maxShapeCount}, `
      + `waves=${summary.maxWaveCount}, sprites=${summary.maxSpriteCount}.`,
    );
    if (summary.unsupportedFunctions.length > 0) {
      console.log(`Unsupported functions: ${summary.unsupportedFunctions.join(', ')}`);
    }
    if (summary.unsupportedShaderSections.length > 0) {
      console.log(`Unsupported shader sections: ${summary.unsupportedShaderSections.join(', ')}`);
    }
    matrix
      .filter((entry) => !entry.supported)
      .slice(0, 10)
      .forEach((entry) => {
        console.log(`- ${entry.id}: ${entry.unsupportedFunctions.join(', ') || 'shader gap'}`);
      });
  }
} finally {
  await loader.close();
}
