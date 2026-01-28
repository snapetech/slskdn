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
    
    // Build Solid config section if enabled
    const solidConfig = this.config.solidEnabled !== false ? `
feature:
  solid: true
solid:
  allowedHosts:${this.config.solidAllowedHosts && this.config.solidAllowedHosts.length > 0 
    ? '\n' + this.config.solidAllowedHosts.map(h => `    - "${h}"`).join('\n')
    : ' []'}
  allowInsecureHttp: true` : `
feature:
  solid: false`;

    const configYaml = `web:
  port: ${this.apiPort}
  host: 127.0.0.1
  authentication:
    username: admin
    password: admin
directories:
  downloads: ${path.join(this.appDir, 'downloads')}
  incomplete: ${path.join(this.appDir, 'incomplete')}
shares:
  directories:
    - ${this.config.shareDir}
feature:
  identityFriends: true
  collectionsSharing: true${solidConfig}
flags:
  no_connect: ${this.config.flags?.noConnect ?? (process.env.SLSKDN_TEST_NO_CONNECT === 'true')}
`;

    await fs.writeFile(configPath, configYaml, 'utf8');

    // Build if needed (first run)
    // In CI, assume already built
    const buildNeeded = !process.env.CI;
    if (buildNeeded) {
      // Could run dotnet build here, but assume it's done
    }

    // Launch slskdn process
    const projectPath = path.join(process.cwd(), '..', '..', 'src', 'slskd', 'slskd.csproj');
    const args = ['run', '--project', projectPath, '--no-build', '--', '--config', configPath];

    this.process = spawn('dotnet', args, {
      cwd: path.join(process.cwd(), '..', '..'),
      stdio: ['ignore', 'pipe', 'pipe'],
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development'
      }
    });

    // Log output for debugging
    this.process.stdout?.on('data', (data) => {
      if (process.env.DEBUG) {
        console.log(`[${this.config.nodeName}] ${data}`);
      }
    });
    this.process.stderr?.on('data', (data) => {
      if (process.env.DEBUG) {
        console.error(`[${this.config.nodeName}] ${data}`);
      }
    });

    // Wait for health endpoint
    const healthUrl = `${this.apiUrl}/health`;
    const startTime = Date.now();
    const timeout = 30000;
    
    while (Date.now() - startTime < timeout) {
      try {
        const response = await fetch(healthUrl);
        if (response.ok) {
          return;
        }
      } catch (err) {
        // Ignore errors, keep polling
      }
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    
    throw new Error(`Health check timeout for ${healthUrl} after ${timeout}ms`);
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
          }, 5000);
        } else {
          resolve();
        }
      });
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
