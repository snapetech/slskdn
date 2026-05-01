import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// Custom Less plugin to handle semantic-ui-less webpack-style aliases.
// @semantic-ui-react/craco-less previously provided these via webpack Less loader.
// Handles:
//   - '../../theme.config' (imported by node_modules/semantic-ui-less/semantic.less)
//     → src/semantic-ui/theme.config
//   - '~foo/bar' (webpack-style node_modules imports in Less files)
//     → node_modules/foo/bar
class SemanticUILessFileManager {
  install(less, pluginManager) {
    const themeConfigPath = path.resolve(__dirname, 'src/semantic-ui/theme.config');

    class AliasFileManager extends less.FileManager {
      supports(filename) {
        return filename === '../../theme.config' || filename.startsWith('~');
      }

      loadFile(filename, _currentDirectory, options, environment) {
        if (filename === '../../theme.config') {
          const contents = fs.readFileSync(themeConfigPath, 'utf-8');
          return Promise.resolve({ filename: themeConfigPath, contents });
        }
        if (filename.startsWith('~')) {
          const resolved = path.resolve(__dirname, 'node_modules', filename.slice(1));
          const contents = fs.readFileSync(resolved, 'utf-8');
          return Promise.resolve({ filename: resolved, contents });
        }
        return super.loadFile(filename, _currentDirectory, options, environment);
      }
    }

    pluginManager.addFileManager(new AliasFileManager());
  }

  get minVersion() {
    return [2, 7, 0];
  }
}

export default defineConfig({
  base: './',
  plugins: [react()],

  css: {
    preprocessorOptions: {
      less: {
        math: 'always',
        relativeUrls: true,
        javascriptEnabled: true,
        paths: [path.resolve(__dirname, 'node_modules')],
        plugins: [new SemanticUILessFileManager()],
      },
    },
  },

  server: {
    port: 3001,
    proxy: {
      '/api': {
        target: 'http://localhost:5099',
        changeOrigin: true,
      },
      '/hub': {
        target: 'http://localhost:5099',
        ws: true,
        changeOrigin: true,
      },
    },
  },

  build: {
    outDir: 'build',
    emptyOutDir: true,
    chunkSizeWarningLimit: 700,
    // Vite 8 defaults to lightningcss for CSS minification, which rejects some
    // valid-but-unusual CSS emitted by semantic-ui-less (e.g. 0.0px dimensions).
    // Use esbuild CSS minifier instead, which is tolerant of this output.
    cssMinify: 'esbuild',
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined;

          if (id.includes('butterchurn-presets')) return 'milkdrop-presets';
          if (id.includes('butterchurn')) return 'milkdrop';
          if (id.includes('semantic-ui')) return 'semantic-ui';
          if (
            id.includes('/react/') ||
            id.includes('/react-dom/') ||
            id.includes('/react-router') ||
            id.includes('/scheduler/')
          ) {
            return 'react-vendor';
          }
          if (id.includes('@microsoft/signalr')) return 'signalr';
          if (id.includes('axios')) return 'api-vendor';

          return 'vendor';
        },
      },
    },
  },

  define: {
    // Shim for any remaining process.env.NODE_ENV references
    'process.env.NODE_ENV': JSON.stringify(process.env.NODE_ENV || 'development'),
  },

  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/setupTests.js'],
    exclude: ['e2e/**', 'node_modules/**'],
  },
});
