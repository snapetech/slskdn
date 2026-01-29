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
  allowInsecureHttp: true`
      : '';

    // slskdn requires absolute paths for shares; repo root when running from tests/e2e is ../..
    const repoRoot = path.resolve(process.cwd(), '..', '..');
    const shareDirAbsolute = path.isAbsolute(this.config.shareDir)
      ? this.config.shareDir
      : path.join(repoRoot, this.config.shareDir);

    const configYaml = `web:
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
flags:
  no_connect: ${this.config.flags?.noConnect ?? (process.env.SLSKDN_TEST_NO_CONNECT === 'true')}
`;

    await fs.writeFile(configPath, configYaml, 'utf8');

    const projectPath = path.join(repoRoot, 'src', 'slskd', 'slskd.csproj');

    // Do NOT build here: building takes several seconds and another process can grab findFreePort() in the meantime (AddressAlreadyInUse).
    // Caller must run `dotnet build src/slskd/slskd.csproj` (or bin/build) before starting nodes.
    const args = ['run', '--project', projectPath, '--no-build', '--', '--config', configPath, '--app-dir', this.appDir];

    // stdin must be a pipe kept open: when stdin is /dev/null (ignore), the child can see EOF and exit (e.g. dotnet run)
    const child = spawn('dotnet', args, {
      cwd: repoRoot,
      stdio: ['pipe', 'pipe', 'pipe'],
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        DOTNET_CLI_TELEMETRY_OPTOUT: '1'
      }
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
