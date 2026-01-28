import { type ChildProcess, execFile, spawn } from 'node:child_process';
import * as crypto from 'node:crypto';
import * as fs from 'node:fs/promises';
import * as net from 'node:net';
import * as path from 'node:path';

export type NodeConfig = {
  apiPort?: number;
  appDir?: string;
  flags?: {
    noConnect?: boolean;
  };
  nodeName: string;
  shareDir: string | string[]; // Single dir or array for multiple shares
};

/**
 * Find a free port on localhost.
 */
async function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.listen(0, () => {
      const addr = server.address();
      if (addr && typeof addr === 'object') {
        const port = addr.port;
        server.close(() => resolve(port));
      } else {
        reject(new Error('Failed to find free port'));
      }
    });
    server.on('error', reject);
  });
}

/**
 * Wait for TCP port to be listening (socket-level check, before HTTP health).
 * This proves Kestrel actually bound to the port, regardless of routing/auth.
 */
async function waitForTcpListen(
  host: string,
  port: number,
  timeoutMs: number,
): Promise<void> {
  const start = Date.now();
  for (;;) {
    const ok = await new Promise<boolean>((resolve) => {
      const sock = new net.Socket();
      sock.setTimeout(500); // Reduced from 750ms
      sock.once('connect', () => {
        sock.destroy();
        resolve(true);
      });
      sock.once('timeout', () => {
        sock.destroy();
        resolve(false);
      });
      sock.once('error', () => {
        resolve(false);
      });
      sock.connect(port, host);
    });

    if (ok) return;
    if (Date.now() - start > timeoutMs) {
      throw new Error(
        `TCP port not listening: ${host}:${port} after ${timeoutMs}ms`,
      );
    }

    await new Promise((r) => setTimeout(r, 250));
  }
}

/**
 * Tail last N lines from a string (simple implementation).
 */
function tail(text: string, lines: number = 200): string {
  const allLines = text.split('\n');
  return allLines.slice(-lines).join('\n');
}

async function runCommand(
  command: string,
  args: string[],
  cwd: string,
): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const child = spawn(command, args, {
      cwd,
      env: process.env,
      stdio: 'inherit',
    });

    child.on('error', reject);
    child.on('exit', (code) => {
      if (code === 0) {
        resolve();
      } else {
        reject(
          new Error(`${command} ${args.join(' ')} exited with code ${code}`),
        );
      }
    });
  });
}

async function getListenSummary(port: number): Promise<string> {
  return new Promise((resolve) => {
    execFile('ss', ['-ltnp'], (error, stdout, stderr) => {
      if (error) {
        resolve(`ss failed: ${stderr || error.message}`);
        return;
      }

      const lines = stdout.split('\n');
      const matches = lines.filter((line) => line.includes(`:${port} `));
      if (matches.length === 0) {
        resolve(`No ss entries for :${port}`);
        return;
      }

      resolve(matches.join('\n'));
    });
  });
}

async function getListenSummaryForPid(pid: number): Promise<string> {
  return new Promise((resolve) => {
    execFile('ss', ['-ltnp'], (error, stdout, stderr) => {
      if (error) {
        resolve(`ss failed: ${stderr || error.message}`);
        return;
      }

      const lines = stdout.split('\n');
      const matches = lines.filter((line) => line.includes(`pid=${pid}`));
      if (matches.length === 0) {
        resolve(`No ss entries for pid=${pid}`);
        return;
      }

      resolve(matches.join('\n'));
    });
  });
}

/**
 * Manages a single slskdn test node (real process).
 * Each node gets:
 * - Isolated app directory
 * - Unique API port
 * - Fixture share directory
 * - Optional flags (no_connect for CI determinism)
 */
export class SlskdnNode {
  private static webBuildPromise: Promise<void> | null = null;

  private process: ChildProcess | null = null;

  private apiPort: number = 0;

  private soulseekListenPort: number = 0;

  private shareTokenKey: string = '';

  private appDir: string = '';

  private config: NodeConfig;

  constructor(config: NodeConfig) {
    this.config = config;
  }

  /**
   * Get the repository root directory.
   */
  private getRepoRoot(): string {
    // process.cwd() is src/web/e2e/ when running tests, so go up 3 levels
    // But we need to be more robust - use __dirname if available, or calculate from cwd
    if (typeof __dirname !== 'undefined') {
      // Running as compiled JS
      return path.join(__dirname, '..', '..', '..', '..');
    } else {
      // Running as TS - process.cwd() is src/web/e2e/
      return path.resolve(process.cwd(), '..', '..', '..');
    }
  }

  private async syncWebUi(repoRoot: string): Promise<void> {
    const webBuildPath = path.join(repoRoot, 'src', 'web', 'build');
    const wwwrootPath = path.join(
      repoRoot,
      'src',
      'slskd',
      'bin',
      'Debug',
      'net8.0',
      'wwwroot',
    );

    try {
      await fs.access(webBuildPath);
    } catch {
      await this.ensureWebBuild(repoRoot);
    }

    try {
      await fs.access(webBuildPath);
    } catch {
      throw new Error(
        'Web build not found at src/web/build. Run `npm run build` first.',
      );
    }

    await fs.rm(wwwrootPath, { force: true, recursive: true });
    await fs.mkdir(wwwrootPath, { recursive: true });
    await fs.cp(webBuildPath, wwwrootPath, { recursive: true });
  }

  private async ensureWebBuild(repoRoot: string): Promise<void> {
    if (!SlskdnNode.webBuildPromise) {
      SlskdnNode.webBuildPromise = (async () => {
        const webRoot = path.join(repoRoot, 'src', 'web');
        const nodeModulesPath = path.join(webRoot, 'node_modules');

        try {
          await fs.access(nodeModulesPath);
        } catch {
          await runCommand('npm', ['ci'], webRoot);
        }

        await runCommand('npm', ['run', 'build'], webRoot);
      })();
    }

    await SlskdnNode.webBuildPromise;
  }

  /**
   * Start the slskdn node process.
   */
  async start(): Promise<void> {
    const repoRoot = this.getRepoRoot();
    await this.syncWebUi(repoRoot);

    // Enforce test fixtures exist and validate checksums (fail fast if missing/corrupt)
    // The shareDir is like 'test-data/slskdn-test-fixtures/music'
    // We need to check the parent directory 'test-data/slskdn-test-fixtures'
    const shareDirectoriesForValidation = Array.isArray(this.config.shareDir)
      ? this.config.shareDir
      : [this.config.shareDir];
    const shareDirectoriesNonEmpty =
      shareDirectoriesForValidation.filter(Boolean);
    const shareDirectoriesAbsoluteForValidation = shareDirectoriesNonEmpty.map(
      (dir) => (path.isAbsolute(dir) ? dir : path.join(repoRoot, dir)),
    );
    const fixturesRoot =
      shareDirectoriesAbsoluteForValidation.length > 0
        ? path.dirname(shareDirectoriesAbsoluteForValidation[0])
        : null;

    if (fixturesRoot) {
      // Fail fast if fixtures root doesn't exist
      try {
        await fs.access(fixturesRoot);
      } catch {
        throw new Error(
          `E2E fixtures directory not found: ${fixturesRoot}\n` +
            'Run: ./scripts/fetch-test-fixtures.sh\n' +
            'Or: cd test-data/slskdn-test-fixtures/meta && node generate-manifest.js',
        );
      }
    }

    // Fail fast if manifest doesn't exist
    const manifestPath = fixturesRoot
      ? path.join(fixturesRoot, 'meta', 'manifest.json')
      : null;
    let manifest: {
      files: Array<{ bytes: number; path: string; sha256: string }>;
      version: number;
    } | null = null;
    if (manifestPath) {
      try {
        const manifestContent = await fs.readFile(manifestPath);
        manifest = JSON.parse(manifestContent);
      } catch {
        throw new Error(
          `E2E fixtures manifest not found or invalid: ${manifestPath}\n` +
            'Run: cd test-data/slskdn-test-fixtures/meta && node generate-manifest.js',
        );
      }
    }

    // Validate required files exist and checksums match
    const validationErrors: string[] = [];
    if (fixturesRoot && manifest) {
      for (const fileEntry of manifest.files) {
        const filePath = path.join(fixturesRoot, fileEntry.path);
        try {
          await fs.access(filePath);
          // Optionally validate checksum (can be slow for large files, so make it optional via env var)
          if (process.env.SLSKDN_VALIDATE_FIXTURE_CHECKSUMS === '1') {
            const fileContent = await fs.readFile(filePath);
            const hash = crypto
              .createHash('sha256')
              .update(fileContent)
              .digest('hex');
            if (hash !== fileEntry.sha256) {
              validationErrors.push(
                `Checksum mismatch for ${fileEntry.path}: expected ${fileEntry.sha256}, got ${hash}`,
              );
            }
          }
        } catch {
          validationErrors.push(
            `Required fixture file missing: ${fileEntry.path}`,
          );
        }
      }
    }

    if (validationErrors.length > 0) {
      throw new Error(
        `E2E fixture validation failed:\n${validationErrors.join('\n')}\n` +
          'Run: ./scripts/fetch-test-fixtures.sh\n' +
          'Or: cd test-data/slskdn-test-fixtures/meta && node generate-manifest.js',
      );
    }

    // Allocate ephemeral port if not provided
    if (!this.config.apiPort) {
      this.apiPort = await findFreePort();
    } else {
      this.apiPort = this.config.apiPort;
    }

    // Allocate a unique Soulseek listen port per node (multi-instance needs this)
    this.soulseekListenPort = await findFreePort();
    // Token signing key for share-grants / streams (base64, 32 bytes decoded)
    this.shareTokenKey = crypto.randomBytes(32).toString('base64');

    // Create isolated app directory
    if (!this.config.appDir) {
      if (process.env.SLSKDN_TEST_KEEP_ARTIFACTS === '1') {
        const baseDir =
          process.env.SLSKDN_TEST_ARTIFACTS_DIR ||
          path.join(repoRoot, 'test-artifacts', 'e2e');
        this.appDir = path.join(
          baseDir,
          `${this.config.nodeName}-${this.apiPort}`,
        );
        await fs.mkdir(this.appDir, { recursive: true });
      } else {
        this.appDir = await fs.mkdtemp(path.join('/tmp', 'slskdn-test-'));
      }
    } else {
      this.appDir = this.config.appDir;
      await fs.mkdir(this.appDir, { recursive: true });
    }

    // Create subdirectories
    await fs.mkdir(path.join(this.appDir, 'downloads'), { recursive: true });
    await fs.mkdir(path.join(this.appDir, 'incomplete'), { recursive: true });
    await fs.mkdir(path.join(this.appDir, 'config'), { recursive: true });

    // Write minimal config (YAML format)
    // Convert shareDir(s) to absolute paths (slskdn requires absolute paths)
    const shareDirectories = Array.isArray(this.config.shareDir)
      ? this.config.shareDir
      : [this.config.shareDir];
    const shareDirectoriesAbsolute = shareDirectories.map((dir) =>
      path.isAbsolute(dir) ? dir : path.join(repoRoot, dir),
    );

    // Get node credentials
    const nodeCreds = {
      A: { password: 'nodeA', username: 'nodeA' },
      B: { password: 'nodeB', username: 'nodeB' },
      C: { password: 'nodeC', username: 'nodeC' },
    }[this.config.nodeName] || {
      password: this.config.nodeName,
      username: this.config.nodeName,
    };

    const configPath = path.join(this.appDir, 'config', 'slskd.yml');
    // Note: YAML provider automatically prefixes with "slskd:" namespace, so DON'T wrap under slskd: here
    // If we wrap it, we'd get slskd:slskd:web:port instead of slskd:web:port
    const sharesYaml =
      shareDirectoriesAbsolute.length > 0
        ? `shares:
  directories:
${shareDirectoriesAbsolute.map((dir) => `    - ${dir}`).join('\n')}`
        : `shares:
  directories: []`;
    const configYaml = `web:
  port: ${this.apiPort}
  host: 127.0.0.1
  https:
    disabled: true
  authentication:
    username: ${nodeCreds.username}
    password: ${nodeCreds.password}
  rateLimiting:
    enabled: false
  cors:
    enabled: true
    allowCredentials: false
    allowedOrigins:
      - "*"
    allowedMethods:
      - GET
      - POST
      - PUT
      - DELETE
      - OPTIONS
      - HEAD
      - PATCH
soulseek:
  username: ${nodeCreds.username}
  password: ${nodeCreds.password}
  listenPort: ${this.soulseekListenPort}
sharing:
  tokenSigningKey: ${this.shareTokenKey}
directories:
  downloads: ${path.join(this.appDir, 'downloads')}
  incomplete: ${path.join(this.appDir, 'incomplete')}
${sharesYaml}
feature:
  IdentityFriends: true
  CollectionsSharing: true
  Streaming: true
  StreamingRelayFallback: true
  MeshParallelSearch: true
  MeshPublishAvailability: true
  ScenePodBridge: true
  Swagger: true
overlay:
  enable: false
overlayData:
  enable: false
mesh:
  enableDht: false
  enableStun: false
  stunServers: []
  peerDescriptorRefresh:
    enableIpChangeDetection: false
flags:
  no_connect: ${this.config.flags?.noConnect ?? process.env.SLSKDN_TEST_NO_CONNECT === 'true'}
`;

    await fs.writeFile(configPath, configYaml, 'utf8');

    // Launch slskdn process
    const projectPath = path.join(repoRoot, 'src', 'slskd', 'slskd.csproj');

    // Verify project exists
    try {
      await fs.access(projectPath);
    } catch {
      throw new Error(`Project not found: ${projectPath}`);
    }

    // Add --force-share-scan to avoid ShareInitializationException when cache doesn't exist
    const skipBuild = process.env.SLSKDN_E2E_NO_BUILD === 'true';
    const args = [
      'run',
      '--project',
      projectPath,
      ...(skipBuild ? ['--no-build'] : []),
      '--',
      '--app-dir',
      this.appDir,
      '--config',
      configPath,
      '--force-share-scan',
    ];

    // Write stdout/stderr to files for debugging
    const artifactsDir = path.join(this.appDir, 'artifacts');
    await fs.mkdir(artifactsDir, { recursive: true });
    const stdoutPath = path.join(artifactsDir, 'stdout.log');
    const stderrPath = path.join(artifactsDir, 'stderr.log');
    const stdoutFd = await fs.open(stdoutPath, 'w');
    const stderrFd = await fs.open(stderrPath, 'w');

    // Force binding to harness port via ASPNETCORE_URLS (bypasses config binding issues)
    this.process = spawn('dotnet', args, {
      cwd: repoRoot,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: `http://127.0.0.1:${this.apiPort}`,
        SLSKDN_E2E_CONCURRENT_START: '1',
        SLSKDN_E2E_SERVER_PROBE: '1',
        SLSKDN_E2E_SHARE_ANNOUNCE: '1',
        SLSKDN_E2E_SKIP_BRIDGE_PROXY: '1',
      },
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    // Capture output for debugging (always log on error)
    let stdout = '';
    let stderr = '';

    const nodeStartTime = Date.now();
    const logWithTimestamp = (message: string) => {
      const elapsed = Date.now() - nodeStartTime;
      const timestamp = new Date().toISOString();
      console.log(
        `[${timestamp}] [${this.config.nodeName}] [+${elapsed}ms] ${message}`,
      );
    };

    this.process.stdout?.on('data', async (data) => {
      const text = data.toString();
      stdout += text;
      await stdoutFd.write(data);
      if (process.env.DEBUG) {
        logWithTimestamp(text.trim());
      }
    });

    this.process.stderr?.on('data', async (data) => {
      const text = data.toString();
      await stderrFd.write(data);
      stderr += text;
      if (process.env.DEBUG) {
        logWithTimestamp(`STDERR: ${text.trim()}`);
      }
    });

    // Handle process errors
    this.process.on('error', (error) => {
      throw new Error(`Failed to start slskdn process: ${error.message}`);
    });

    // Check if process exits early
    this.process.on('exit', (code, signal) => {
      const elapsed = Date.now() - nodeStartTime;
      const timestamp = new Date().toISOString();
      if (code !== null && code !== 0) {
        const errorMessage = `slskdn process exited with code ${code}${signal ? ` (signal: ${signal})` : ''} after ${elapsed}ms.\nSTDOUT (last 500 chars):\n${stdout.slice(-500)}\nSTDERR (last 500 chars):\n${stderr.slice(-500)}`;
        // Always log process exit errors
        console.error(
          `[${timestamp}] [${this.config.nodeName}] [+${elapsed}ms] ${errorMessage}`,
        );
        // Write full logs to artifacts for debugging
        if (this.appDir) {
          const exitLogPath = path.join(this.appDir, 'artifacts', 'exit.log');
          fs.writeFile(
            exitLogPath,
            `Exit code: ${code}\nSignal: ${signal || 'none'}\nUptime: ${elapsed}ms\nTimestamp: ${timestamp}\n\nSTDOUT:\n${stdout}\n\nSTDERR:\n${stderr}\n`,
            'utf8',
          ).catch(() => {});
        }
      } else if (code === 0 && signal) {
        console.log(
          `[${timestamp}] [${this.config.nodeName}] [+${elapsed}ms] Process exited normally (signal: ${signal})`,
        );
      }
    });

    // Also log stderr immediately for debugging
    if (!process.env.DEBUG) {
      this.process.stderr?.on('data', (data) => {
        const text = data.toString();
        stderr += text;
        // Log errors even without DEBUG
        if (
          text.toLowerCase().includes('error') ||
          text.toLowerCase().includes('exception') ||
          text.toLowerCase().includes('fatal')
        ) {
          console.error(`[${this.config.nodeName}] ${text}`);
        }
      });
    }

    // Step 1: Wait for TCP port to be listening (socket-level check)
    // This proves Kestrel actually bound to the port, regardless of routing/auth
    const tcpStartTime = Date.now();
    logWithTimestamp(`[start] Waiting for TCP port ${this.apiPort} to listen`);
    try {
      await waitForTcpListen('127.0.0.1', this.apiPort, 30_000);
      const tcpElapsed = Date.now() - tcpStartTime;
      logWithTimestamp(
        `[start] TCP port ${this.apiPort} listening after ${tcpElapsed}ms`,
      );
    } catch {
      // TCP never opened - true startup/bind problem
      const stdoutTail = tail(stdout, 200);
      const stderrTail = tail(stderr, 200);
      const ssSummary = await getListenSummary(this.apiPort);
      const pidSummary =
        this.process?.pid !== undefined
          ? await getListenSummaryForPid(this.process.pid)
          : 'No process pid available';
      await stdoutFd.close();
      await stderrFd.close();
      throw new Error(
        `TCP port ${this.apiPort} never started listening.\n` +
          `This indicates a true startup/bind problem.\n\n` +
          `---- ss -ltnp matches for :${this.apiPort} ----\n${ssSummary}\n\n` +
          `---- ss -ltnp matches for pid ----\n${pidSummary}\n\n` +
          `---- stdout (last 200 lines) ----\n${stdoutTail}\n\n` +
          `---- stderr (last 200 lines) ----\n${stderrTail}\n\n` +
          `Log files: ${stdoutPath}, ${stderrPath}`,
      );
    }

    // Step 2: Now that TCP is listening, check HTTP health endpoint
    // Wait for readiness endpoint (simpler than /health, doesn't run complex checks)
    // Falls back to /health if /health/ready doesn't exist (backward compatibility)
    const readinessUrl = `${this.apiUrl}/health/ready`;
    const healthUrl = `${this.apiUrl}/health`;
    const healthStartTime = Date.now();
    const timeout = 60_000;
    logWithTimestamp(`[start] Starting health check (timeout: ${timeout}ms)`);

    while (Date.now() - healthStartTime < timeout) {
      // Check if process died
      if (this.process.exitCode !== null) {
        if (this.process.exitCode !== 0) {
          const errorMessage = `slskdn process exited early with code ${this.process.exitCode}.\nSTDOUT:\n${stdout}\nSTDERR:\n${stderr}`;
          throw new Error(errorMessage);
        } else {
          // Process exited with 0, which is unexpected but might be OK
          break;
        }
      }

      try {
        // Try simpler readiness endpoint first (faster, no complex checks)
        // Use AbortController for timeout (AbortSignal.timeout may not be available in all Node versions)
        const readinessController = new AbortController();
        const readinessTimeout = setTimeout(
          () => readinessController.abort(),
          1_000,
        ); // Reduced from 2000ms
        try {
          const readinessResponse = await fetch(readinessUrl, {
            signal: readinessController.signal,
          });
          clearTimeout(readinessTimeout);
          // Treat 200/401/403 as "server is reachable" - distinguishes connection failure from auth issues
          if (
            readinessResponse.status === 200 ||
            readinessResponse.status === 401 ||
            readinessResponse.status === 403
          ) {
            const healthElapsed = Date.now() - healthStartTime;
            logWithTimestamp(
              `[start] Health check passed after ${healthElapsed}ms (status: ${readinessResponse.status})`,
            );
            return;
          }
        } catch {
          clearTimeout(readinessTimeout);
          // Readiness endpoint might not exist or failed, try full health check
          const healthController = new AbortController();
          const healthTimeout = setTimeout(
            () => healthController.abort(),
            1_000,
          ); // Reduced from 2000ms
          try {
            const response = await fetch(healthUrl, {
              signal: healthController.signal,
            });
            clearTimeout(healthTimeout);
            // Treat 200/401/403 as "server is reachable" - distinguishes connection failure from auth issues
            if (
              response.status === 200 ||
              response.status === 401 ||
              response.status === 403
            ) {
              const healthElapsed = Date.now() - healthStartTime;
              logWithTimestamp(
                `[start] Health check passed after ${healthElapsed}ms (status: ${response.status})`,
              );
              return;
            }
          } catch {
            clearTimeout(healthTimeout);
            // Ignore errors, keep polling
          }
        }
      } catch {
        // Ignore errors, keep polling
      }

      await new Promise((resolve) => setTimeout(resolve, 300)); // Reduced from 500ms
    }

    // If we timeout, include tail of captured output
    await stdoutFd.close();
    await stderrFd.close();
    const stdoutTail = tail(stdout, 200);
    const stderrTail = tail(stderr, 200);
    const errorMessage =
      `Health check timeout for ${healthUrl} after ${timeout}ms\n` +
      `TCP port ${this.apiPort} IS listening, but HTTP endpoint is unreachable.\n` +
      `This indicates a routing/auth/base URL problem.\n\n` +
      `---- stdout (last 200 lines) ----\n${stdoutTail}\n\n` +
      `---- stderr (last 200 lines) ----\n${stderrTail}\n\n` +
      `Log files: ${stdoutPath}, ${stderrPath}`;
    throw new Error(errorMessage);
  }

  /**
   * Get the API base URL for this node.
   */
  get apiUrl(): string {
    return `http://127.0.0.1:${this.apiPort}`;
  }

  /**
   * Get node configuration for use in tests.
   */
  get nodeCfg() {
    const nodeCreds = {
      A: { password: 'nodeA', username: 'nodeA' },
      B: { password: 'nodeB', username: 'nodeB' },
      C: { password: 'nodeC', username: 'nodeC' },
    }[this.config.nodeName] || {
      password: this.config.nodeName,
      username: this.config.nodeName,
    };
    return {
      baseUrl: this.apiUrl,
      password: nodeCreds.password,
      username: nodeCreds.username,
    };
  }

  /**
   * Get the app directory path.
   */
  getAppDir(): string {
    return this.appDir;
  }

  /**
   * List files in the downloads directory.
   * Returns array of file metadata (name, size, path).
   * Works for both local E2E and CI environments.
   */
  async getDownloadedFiles(): Promise<
    Array<{ modified: Date; name: string; path: string; size: number }>
  > {
    if (!this.appDir) {
      throw new Error(
        'Cannot get downloaded files: app directory not set (node not started?)',
      );
    }

    const downloadsDir = path.join(this.appDir, 'downloads');
    const files: Array<{
      modified: Date;
      name: string;
      path: string;
      size: number;
    }> = [];

    try {
      // Check if downloads directory exists
      try {
        await fs.access(downloadsDir);
      } catch {
        // Directory doesn't exist yet - return empty array
        return files;
      }

      const entries = await fs.readdir(downloadsDir, { withFileTypes: true });
      for (const entry of entries) {
        if (entry.isFile()) {
          const filePath = path.join(downloadsDir, entry.name);
          try {
            const stats = await fs.stat(filePath);
            files.push({
              modified: stats.mtime,
              name: entry.name,
              path: filePath,
              size: stats.size,
            });
          } catch (error) {
            // File might have been deleted between readdir and stat
            console.warn(
              `[SlskdnNode] Failed to stat file ${entry.name}: ${error}`,
            );
          }
        }
      }
    } catch (error) {
      const error_ = error as NodeJS.ErrnoException;
      // Downloads directory might not exist yet (ENOENT) - that's OK
      if (error_.code !== 'ENOENT') {
        console.error(
          `[SlskdnNode] Error reading downloads directory ${downloadsDir}: ${error_.message}`,
        );
        throw error;
      }
    }

    return files;
  }

  /**
   * Check if a file exists in downloads directory by name or sha256 prefix.
   * Works for both local E2E and CI environments.
   * @param searchTerm - Filename to match, or sha256 prefix (first 8 chars)
   * @returns File metadata if found, null otherwise
   */
  async findDownloadedFile(searchTerm: string): Promise<{
    modified: Date;
    name: string;
    path: string;
    size: number;
  } | null> {
    if (!this.appDir) {
      throw new Error(
        'Cannot find downloaded file: app directory not set (node not started?)',
      );
    }

    const files = await this.getDownloadedFiles();
    const searchLower = searchTerm.toLowerCase();

    // Try exact filename match first
    let found = files.find((f) => f.name.toLowerCase() === searchLower);
    if (found) return found;

    // Try partial filename match (e.g., "sintel" matches "sintel_512kb_stereo.mp4")
    found = files.find((f) => f.name.toLowerCase().includes(searchLower));
    if (found) return found;

    // Try filename without extension match
    found = files.find((f) => {
      const nameWithoutExtension = path.parse(f.name).name.toLowerCase();
      return nameWithoutExtension.includes(searchLower);
    });
    if (found) return found;

    // Try sha256 prefix match (if searchTerm looks like a hash prefix)
    // Note: Computing sha256 for all files is expensive, so we only do name matching
    // If you need sha256 matching, compute it on-demand for specific files

    return null;
  }

  /**
   * Wait for a file to appear in downloads directory.
   * Useful for backfill tests where download happens asynchronously.
   * @param searchTerm - Filename pattern to match
   * @param timeoutMs - Maximum time to wait (default: 30 seconds)
   * @param pollIntervalMs - How often to check (default: 1 second)
   * @returns File metadata if found, null if timeout
   */
  async waitForDownloadedFile(
    searchTerm: string,
    timeoutMs: number = 30_000,
    pollIntervalMs: number = 1_000,
  ): Promise<{
    modified: Date;
    name: string;
    path: string;
    size: number;
  } | null> {
    const startTime = Date.now();

    while (Date.now() - startTime < timeoutMs) {
      const file = await this.findDownloadedFile(searchTerm);
      if (file && file.size > 0) {
        return file;
      }

      await new Promise((resolve) => setTimeout(resolve, pollIntervalMs));
    }

    return null;
  }

  /**
   * Stop the node process and clean up.
   */
  async stop(): Promise<void> {
    if (this.process) {
      this.process.kill('SIGTERM');
      await new Promise<void>((resolve) => {
        if (this.process) {
          this.process.on('exit', () => resolve());
          // Force kill after 5s
          setTimeout(() => {
            if (this.process) {
              this.process.kill('SIGKILL');
              resolve();
            }
          }, 5_000);
        } else {
          resolve();
        }
      });
      this.process = null;
    }

    // Cleanup app directory unless KEEP_ARTIFACTS is set
    if (this.appDir && process.env.SLSKDN_TEST_KEEP_ARTIFACTS !== '1') {
      try {
        await fs.rm(this.appDir, { force: true, recursive: true });
      } catch {
        // Ignore cleanup errors
      }
    }
  }
}
