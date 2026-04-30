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

        const preset = parseMilkdropPreset(\`
          decay=0.82
          wave_r=0.25
          wave_g=0.7
          wave_b=0.95
          wave_scale=1.4
          meshx=3
          meshy=2
          per_frame_1=rot=0.015*sin(time);
          per_pixel_1=dx=0.02*sin((x+time)*6.283);
          per_pixel_2=dy=0.02*cos((y+time)*6.283);
          mv_x=4
          mv_y=3
          mv_l=0.2
          mv_a=0.45
          shape00_enabled=1
          shape00_sides=5
          shape00_rad=0.22
          shape00_x=0.5
          shape00_y=0.5
          shape00_r=0.9
          shape00_g=0.85
          shape00_b=0.15
          shape00_a=0.45
          wavecode_0_enabled=1
          wavecode_0_samples=32
          wavecode_0_spectrum=1
          wavecode_0_dots=1
          wavecode_0_r=0.8
          wavecode_0_g=1
          wavecode_0_b=0.3
          wavecode_0_a=0.9
          wavecode_0_per_point1=x=i;
          wavecode_0_per_point2=y=0.1+sample*0.65;
        \`).primary;

        const canvas = document.getElementById('canvas');
        const renderer = createMilkdropRenderer({ canvas, preset });
        renderer.render({
          samples: [-1, -0.25, 0.5, 1, 0.25, -0.5],
          spectrum: new Uint8Array([0, 64, 128, 255, 96, 32]),
          time: 1.25,
        });

        const gl = canvas.getContext('webgl2');
        const pixels = new Uint8Array(canvas.width * canvas.height * 4);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        let litPixels = 0;
        let channelTotal = 0;
        for (let index = 0; index < pixels.length; index += 4) {
          const total = pixels[index] + pixels[index + 1] + pixels[index + 2];
          if (total > 12) litPixels += 1;
          channelTotal += total;
        }
        renderer.dispose();
        window.__nativeMilkdropSmoke = {
          channelTotal,
          litPixels,
          pixelCount: canvas.width * canvas.height,
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
  const stats = await result.jsonValue();
  if (stats.litPixels < stats.pixelCount * 0.05 || stats.channelTotal <= 0) {
    throw new Error(`Native MilkDrop smoke rendered a blank canvas: ${JSON.stringify(stats)}`);
  }
  console.log(`Native MilkDrop smoke passed: ${JSON.stringify(stats)}`);
} finally {
  await browser.close();
  await server.close();
}
