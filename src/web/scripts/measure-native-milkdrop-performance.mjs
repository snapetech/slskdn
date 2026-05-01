import { readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { chromium } from '@playwright/test';
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
  if (inputs.length === 0) return null;

  const files = (await Promise.all(inputs.map((input) =>
    walkPresetFiles(path.resolve(input))))).flat().sort();

  return Promise.all(files.map(async (fileName) => ({
    format: path.extname(fileName).toLowerCase() === '.milk2' ? 'milk2' : undefined,
    id: path.relative(process.cwd(), fileName),
    source: await readFile(fileName, 'utf8'),
  })));
};

const serializeSourcesForHtml = (sources) =>
  JSON.stringify(sources).replace(/<\/script/gi, '<\\/script');

const createSmokeHtml = (sources, frameCount) => `
  <!doctype html>
  <html>
    <body>
      <canvas id="canvas" width="192" height="128"></canvas>
      <script id="preset-sources" type="application/json">${serializeSourcesForHtml(sources)}</script>
      <script type="module">
        import { createMilkdropRenderer } from '/src/components/Player/visualizers/milkdrop/milkdropRenderer.js';
        import { analyzeMilkdropPresetCompatibility, getMilkdropCompatibilityError } from '/src/components/Player/visualizers/milkdrop/presetCompatibility.js';
        import { parseMilkdropPreset } from '/src/components/Player/visualizers/milkdrop/presetParser.js';
        import { nativeMilkdropFixturePack } from '/src/components/Player/visualizers/milkdrop/presetFixtures.js';

        const canvas = document.getElementById('canvas');
        const injectedSources = JSON.parse(document.getElementById('preset-sources').textContent);
        const sources = injectedSources || nativeMilkdropFixturePack;
        const frameCount = ${Number(frameCount)};

        const getCompatibilityError = (parsed) => parsed.presets
          .map((preset) => getMilkdropCompatibilityError(analyzeMilkdropPresetCompatibility(preset)))
          .filter(Boolean)
          .join('; ');

        const getFrame = (frame) => ({
          samples: [-1, -0.25, 0.5, 1, 0.25, -0.5, Math.sin(frame / 4)],
          spectrum: new Uint8Array([0, 64, 128, 255, 96, 32, frame % 255]),
          time: frame / 60,
        });

        const measureSource = (source) => {
          const parsed = parseMilkdropPreset(source.source, { format: source.format });
          const compatibilityError = getCompatibilityError(parsed);
          if (compatibilityError) {
            return {
              id: source.id,
              skipped: true,
              reason: compatibilityError,
            };
          }

          const renderers = parsed.presets.map((preset) =>
            createMilkdropRenderer({ canvas, preset }));
          try {
            for (let frame = 0; frame < 3; frame += 1) {
              renderers.forEach((renderer, rendererIndex) => {
                renderer.render(getFrame(frame), {
                  clearScreen: rendererIndex === 0,
                  outputAlpha: rendererIndex === 0 ? 1 : 0.5,
                });
              });
            }

            const startedAt = performance.now();
            for (let frame = 0; frame < frameCount; frame += 1) {
              renderers.forEach((renderer, rendererIndex) => {
                renderer.render(getFrame(frame + 3), {
                  clearScreen: rendererIndex === 0,
                  outputAlpha: rendererIndex === 0 ? 1 : 0.5,
                });
              });
            }
            const elapsedMs = performance.now() - startedAt;
            return {
              averageFrameMs: elapsedMs / frameCount,
              id: source.id,
              maxFpsEstimate: frameCount > 0 ? 1000 / (elapsedMs / frameCount) : 0,
              presetCount: parsed.presets.length,
              skipped: false,
              totalMs: elapsedMs,
            };
          } finally {
            renderers.forEach((renderer) => renderer.dispose());
          }
        };

        window.__nativeMilkdropPerformance = sources.map(measureSource);
      </script>
    </body>
  </html>
`;

const args = process.argv.slice(2);
const jsonOutput = args.includes('--json');
const frameArgIndex = args.findIndex((arg) => arg === '--frames');
const frameCount = frameArgIndex >= 0
  ? Math.max(1, Number(args[frameArgIndex + 1]) || 12)
  : 12;
const inputs = args.filter((arg, index) =>
  arg !== '--json'
  && arg !== '--frames'
  && index !== frameArgIndex + 1);
const sources = await loadPresetSources(inputs);

const server = await createServer({
  logLevel: 'error',
  plugins: [
    {
      name: 'native-milkdrop-performance-page',
      configureServer(viteServer) {
        viteServer.middlewares.use('/native-milkdrop-performance', (_request, response) => {
          response.setHeader('Content-Type', 'text/html');
          response.end(createSmokeHtml(sources, frameCount));
        });
      },
    },
  ],
  server: {
    host: '127.0.0.1',
    port: 0,
  },
});

await server.listen();

const url = server.resolvedUrls?.local?.[0];
if (!url) {
  await server.close();
  throw new Error('Vite did not expose a local URL for native MilkDrop performance measurement.');
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

try {
  await page.goto(`${url}native-milkdrop-performance`);
  const result = await page.waitForFunction(() => window.__nativeMilkdropPerformance, null, {
    timeout: 10_000,
  });
  const entries = await result.jsonValue();
  const measured = entries.filter((entry) => !entry.skipped);
  const skipped = entries.filter((entry) => entry.skipped);
  const slowest = measured.reduce((current, entry) =>
    (!current || entry.averageFrameMs > current.averageFrameMs ? entry : current), null);
  const summary = {
    frameCount,
    measuredCount: measured.length,
    skippedCount: skipped.length,
    slowest,
    source: sources ? 'files' : 'curated-fixtures',
  };
  const report = {
    entries,
    summary,
  };

  if (jsonOutput) {
    console.log(JSON.stringify(report, null, 2));
  } else {
    console.log(`Native MilkDrop performance (${summary.source}, ${frameCount} frames)`);
    console.log(`${summary.measuredCount} measured; ${summary.skippedCount} skipped.`);
    if (slowest) {
      console.log(
        `Slowest: ${slowest.id} at ${slowest.averageFrameMs.toFixed(2)} ms/frame `
        + `(~${slowest.maxFpsEstimate.toFixed(1)} fps).`,
      );
    }
    skipped.slice(0, 10).forEach((entry) => {
      console.log(`- skipped ${entry.id}: ${entry.reason}`);
    });
  }
} finally {
  await browser.close();
  await server.close();
}
