import { spawn, ChildProcess } from 'child_process';
import * as fs from 'fs/promises';
import * as path from 'path';
import { findFreePort } from '../fixtures/helpers';

export interface NodeConfig {
  nodeName: string;
  shareDir: string;
  apiPort?: number;
  appDir?: string;
  flags?: {
    noConnect?: boolean;
  };
  solidEnabled?: boolean;
  solidAllowedHosts?: string[];
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
  private process: ChildProcess | null = null;
  private apiPort: number = 0;
  private appDir: string = '';
  private config: NodeConfig;

  constructor(config: NodeConfig) {
    this.config = config;
  }

  /**
   * Start the slskdn node process.
   */
  async start(): Promise<void> {
    // Allocate ephemeral port if not provided
    if (!this.config.apiPort) {
      this.apiPort = await findFreePort();
    } else {
      this.apiPort = this.config.apiPort;
    }

    // Create isolated app directory
    if (!this.config.appDir) {
      this.appDir = await fs.mkdtemp(path.join('/tmp', 'slskdn-test-'));
    } else {
      this.appDir = this.config.appDir;
      await fs.mkdir(this.appDir, { recursive: true });
    }

    // Create subdirectories
    await fs.mkdir(path.join(this.appDir, 'downloads'), { recursive: true });
    await fs.mkdir(path.join(this.appDir, 'incomplete'), { recursive: true });
    await fs.mkdir(path.join(this.appDir, 'config'), { recursive: true });

    // Write minimal config (YAML format)
    const configPath = path.join(this.appDir, 'config', 'slskd.yml');
    const solidEnabled = this.config.solidEnabled !== false;
    const allowedHostsYaml = this.config.solidAllowedHosts && this.config.solidAllowedHosts.length > 0
      ? '\n' + this.config.solidAllowedHosts.map(h => `    - "${h}"`).join('\n')
      : ' []';
    const solidBlock = solidEnabled
      ? `
solid:
  allowedHosts:${allowedHostsYaml}
  allowInsecureHttp: true
  allowLocalhostForWebId: true`
      : '';

    // slskdn requires absolute paths for shares; repo root when running from tests/e2e is ../..
    const repoRoot = path.resolve(process.cwd(), '..', '..');
    const shareDirAbsolute = path.isAbsolute(this.config.shareDir)
      ? this.config.shareDir
      : path.join(repoRoot, this.config.shareDir);

    const configYaml = `soulseek:
  username: admin
  password: admin
web:
  port: ${this.apiPort}
  host: 127.0.0.1
  https:
    disabled: true
  authentication:
    username: admin
    password: admin
directories:
  downloads: ${path.join(this.appDir, 'downloads')}
  incomplete: ${path.join(this.appDir, 'incomplete')}
shares:
  directories:
    - ${shareDirAbsolute}
feature:
  identityFriends: true
  collectionsSharing: true
  solid: ${solidEnabled}
${solidBlock}
sharing:
  tokenSigningKey: "${Buffer.alloc(32).fill(0).toString('base64')}"
flags:
  no_connect: ${this.config.flags?.noConnect ?? (process.env.SLSKDN_TEST_NO_CONNECT === 'true')}
`;

    await fs.writeFile(configPath, configYaml, 'utf8');

    const projectPath = path.join(repoRoot, 'src', 'slskd', 'slskd.csproj');

    // Do NOT build here: building takes several seconds and another process can grab findFreePort() in the meantime (AddressAlreadyInUse).
    // Caller must run `dotnet build src/slskd/slskd.csproj` (or bin/build) before starting nodes.
    const args = ['run', '--project', projectPath, '--no-build', '--', '--config', configPath, '--app-dir', this.appDir];

    // stdin must be a pipe kept open: when stdin is /dev/null (ignore), the child can see EOF and exit (e.g. dotnet run)
    const baseEnv = {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_CLI_TELEMETRY_OPTOUT: '1',
      // Share-grant ingest uses Soulseek.Username as current user; E2E nodes use admin/admin (config prefix is SLSKD_)
      SLSKD_SLSK_USERNAME: 'admin',
      SLSKD_SLSK_PASSWORD: 'admin'
    };
    // E2E announce endpoint: ensure server sees SLSKDN_E2E_SHARE_ANNOUNCE when test sets it
    if (process.env.SLSKDN_E2E_SHARE_ANNOUNCE === '1') {
      baseEnv.SLSKDN_E2E_SHARE_ANNOUNCE = '1';
    }
    const child = spawn('dotnet', args, {
      cwd: repoRoot,
      stdio: ['pipe', 'pipe', 'pipe'],
      env: baseEnv
    });
    this.process = child;
    // Keep stdin write end open so child never sees EOF (do not call child.stdin.end())

    const stderrChunks: Buffer[] = [];
    const stdoutChunks: Buffer[] = [];
    this.process.stderr?.on('data', (data: Buffer) => {
      stderrChunks.push(data);
      if (process.env.DEBUG) {
        process.stderr.write(`[${this.config.nodeName}] ${data}`);
      }
    });
    this.process.stdout?.on('data', (data: Buffer) => {
      stdoutChunks.push(data);
      if (process.env.DEBUG) {
        process.stdout.write(`[${this.config.nodeName}] ${data}`);
      }
    });

    const healthUrl = `${this.apiUrl}/health`;
    const startTime = Date.now();
    const timeout = 90000;

    const healthPromise = (async (): Promise<void> => {
      while (Date.now() - startTime < timeout) {
        try {
          const response = await fetch(healthUrl);
          if (response.ok) {
            return;
          }
        } catch {
          // Keep polling
        }
        await new Promise(resolve => setTimeout(resolve, 500));
      }
      const stderr = Buffer.concat(stderrChunks).toString('utf8').trim();
      throw new Error(
        `Health check timeout for ${healthUrl} after ${timeout}ms${stderr ? `\nProcess stderr:\n${stderr}` : ''}`
      );
    })();

    const exitPromise = new Promise<{ code: number | null; signal: string | null }>((resolve) => {
      this.process?.on('exit', (code, signal) => {
        resolve({ code: code ?? null, signal: signal ?? null });
      });
    });

    const result = await Promise.race([healthPromise, exitPromise]);
    if (result && 'code' in result) {
      const stderr = Buffer.concat(stderrChunks).toString('utf8').trim();
      const stdout = Buffer.concat(stdoutChunks).toString('utf8').trim();
      throw new Error(
        `SlskdnNode process exited before health (code=${result.code}, signal=${result.signal})${stderr ? `\nStderr:\n${stderr}` : ''}${stdout ? `\nStdout:\n${stdout}` : ''}`
      );
    }
  }

  /**
   * Get the API base URL for this node.
   */
  get apiUrl(): string {
    return `http://127.0.0.1:${this.apiPort}`;
  }

  /**
   * Get the app directory path.
   */
  getAppDir(): string {
    return this.appDir;
  }

  /**
   * List files in the downloads directory.
   */
  async getDownloadedFiles(): Promise<
    Array<{ modified: Date; name: string; path: string; size: number }>
  > {
    if (!this.appDir) {
      throw new Error('Cannot get downloaded files: app directory not set (node not started?)');
    }
    const downloadsDir = path.join(this.appDir, 'downloads');
    const files: Array<{ modified: Date; name: string; path: string; size: number }> = [];
    try {
      try {
        await fs.access(downloadsDir);
      } catch {
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
          } catch {
            // File might have been deleted between readdir and stat
          }
        }
      }
    } catch (err: unknown) {
      const code = (err as NodeJS.ErrnoException).code;
      if (code !== 'ENOENT') {
        throw err;
      }
    }
    return files;
  }

  /**
   * Find a file in downloads by name or partial match (e.g. sha256 prefix or "sintel").
   */
  async findDownloadedFile(searchTerm: string): Promise<{
    modified: Date;
    name: string;
    path: string;
    size: number;
  } | null> {
    if (!this.appDir) {
      throw new Error('Cannot find downloaded file: app directory not set (node not started?)');
    }
    const files = await this.getDownloadedFiles();
    const searchLower = searchTerm.toLowerCase();
    let found = files.find((f) => f.name.toLowerCase() === searchLower);
    if (found) return found;
    found = files.find((f) => f.name.toLowerCase().includes(searchLower));
    if (found) return found;
    found = files.find((f) => {
      const nameWithoutExt = path.parse(f.name).name.toLowerCase();
      return nameWithoutExt.includes(searchLower);
    });
    return found ?? null;
  }

  /**
   * Wait for a file to appear in downloads (e.g. after backfill).
   */
  async waitForDownloadedFile(
    searchTerm: string,
    timeoutMs: number = 30_000,
    pollIntervalMs: number = 1_000
  ): Promise<{
    modified: Date;
    name: string;
    path: string;
    size: number;
  } | null> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const file = await this.findDownloadedFile(searchTerm);
      if (file && file.size > 0) return file;
      await new Promise((r) => setTimeout(r, pollIntervalMs));
    }
    return null;
  }

  /**
   * Stop the node process and clean up.
   */
  async stop(): Promise<void> {
    if (this.process) {
      const proc = this.process;

      const waitForExit = () =>
        new Promise<void>((resolve) => {
          proc.once('exit', () => resolve());
        });

      const delay = (ms: number) =>
        new Promise<void>((resolve) => {
          setTimeout(resolve, ms);
        });

      const exitPromise = waitForExit();

      // Graceful shutdown first.
      proc.kill('SIGTERM');

      const exitedGracefully = await Promise.race([
        exitPromise.then(() => true),
        delay(5000).then(() => false),
      ]);

      if (!exitedGracefully) {
        // Force kill, then wait for actual exit.
        proc.kill('SIGKILL');
        await Promise.race([exitPromise, delay(5000)]);
      }

      this.process = null;
    }

    // Cleanup app directory unless KEEP_ARTIFACTS is set
    if (this.appDir && process.env.SLSKDN_TEST_KEEP_ARTIFACTS !== '1') {
      try {
        await fs.rm(this.appDir, { recursive: true, force: true });
      } catch (err) {
        // Ignore cleanup errors
      }
    }
  }
}
