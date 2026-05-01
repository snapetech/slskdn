import { chromium } from '@playwright/test';
import { createServer } from 'vite';

const smokeHtml = `
  <!doctype html>
  <html>
    <body>
      <canvas id="canvas" width="192" height="128"></canvas>
      <script type="module">
        import { createMilkdropRenderer } from '/src/components/Player/visualizers/milkdrop/milkdropRenderer.js';
        import { parseMilkdropPreset } from '/src/components/Player/visualizers/milkdrop/presetParser.js';
        import { getNativeMilkdropFixture } from '/src/components/Player/visualizers/milkdrop/presetFixtures.js';

        const canvas = document.getElementById('canvas');
        const gl = canvas.getContext('webgl2');
        const fixtureIds = [
          'classic-primitives',
          'shader-subset',
          'milk2-double',
          'milkdrop3-q-registers',
          'milkdrop3-dense-primitives',
        ];
        const readCanvasStats = () => {
          const pixels = new Uint8Array(canvas.width * canvas.height * 4);
          gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
          let litPixels = 0;
          let channelTotal = 0;
          for (let index = 0; index < pixels.length; index += 4) {
            const total = pixels[index] + pixels[index + 1] + pixels[index + 2];
            if (total > 12) litPixels += 1;
            channelTotal += total;
          }
          return {
            channelTotal,
            litPixels,
            pixelCount: canvas.width * canvas.height,
          };
        };

        const smokeStats = fixtureIds.map((fixtureId, index) => {
          const fixture = getNativeMilkdropFixture(fixtureId);
          const parsed = parseMilkdropPreset(fixture.source, {
            format: fixtureId.startsWith('milkdrop3-') || fixtureId === 'milk2-double' ? 'milk2' : undefined,
          });
          const renderers = parsed.presets.map((preset) =>
            createMilkdropRenderer({ canvas, preset }));
          const frame = {
            samples: [-1, -0.25, 0.5, 1, 0.25, -0.5],
            spectrum: new Uint8Array([0, 64, 128, 255, 96, 32]),
            time: 1.25 + index,
          };
          renderers.forEach((renderer, rendererIndex) => {
            renderer.render(frame, {
              clearScreen: rendererIndex === 0,
              outputAlpha: rendererIndex === 0 ? 1 : 0.5,
            });
          });
          const stats = readCanvasStats();
          renderers.forEach((renderer) => renderer.dispose());
          return {
            fixtureId,
            ...stats,
          };
        });

        const contextLossExtension = gl.getExtension('WEBGL_lose_context');
        let contextLost = false;
        let contextRestored = false;
        canvas.addEventListener('webglcontextlost', (event) => {
          event.preventDefault();
          contextLost = true;
        });
        canvas.addEventListener('webglcontextrestored', () => {
          contextRestored = true;
        });
        if (contextLossExtension) {
          contextLossExtension.loseContext();
          await new Promise((resolve) => setTimeout(resolve, 0));
          contextLossExtension.restoreContext();
          await new Promise((resolve) => setTimeout(resolve, 0));
        }

        window.__nativeMilkdropSmoke = {
          contextLoss: {
            restored: contextRestored,
            supported: Boolean(contextLossExtension),
            lost: contextLost,
          },
          stats: smokeStats,
        };
      </script>
    </body>
  </html>
`;

const server = await createServer({
  logLevel: 'error',
  plugins: [
    {
      name: 'native-milkdrop-smoke-page',
      configureServer(viteServer) {
        viteServer.middlewares.use('/native-milkdrop-smoke', (_request, response) => {
          response.setHeader('Content-Type', 'text/html');
          response.end(smokeHtml);
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
  throw new Error('Vite did not expose a local URL for the native MilkDrop smoke test.');
}

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

try {
  await page.goto(`${url}native-milkdrop-smoke`);

  const result = await page.waitForFunction(() => window.__nativeMilkdropSmoke, null, {
    timeout: 10_000,
  });
  const { contextLoss, stats } = await result.jsonValue();
  const blankResult = stats.find((fixtureStats) =>
    fixtureStats.litPixels < fixtureStats.pixelCount * 0.05
    || fixtureStats.channelTotal <= 0);
  if (blankResult) {
    throw new Error(`Native MilkDrop smoke rendered a blank canvas: ${JSON.stringify(stats)}`);
  }
  if (!contextLoss.supported || !contextLoss.lost || !contextLoss.restored) {
    throw new Error(`Native MilkDrop context loss smoke failed: ${JSON.stringify(contextLoss)}`);
  }
  console.log(`Native MilkDrop smoke passed: ${JSON.stringify({ contextLoss, stats })}`);
} finally {
  await browser.close();
  await server.close();
}
