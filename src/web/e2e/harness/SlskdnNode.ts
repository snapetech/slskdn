import { spawn, ChildProcess } from 'child_process';
import * as fs from 'fs/promises';
import * as path from 'path';
import * as net from 'net';

export interface NodeConfig {
  nodeName: string;
  shareDir: string;
  apiPort?: number;
  appDir?: string;
  flags?: {
    noConnect?: boolean;
  };
}

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

  /**
   * Start the slskdn node process.
   */
  async start(): Promise<void> {
    const repoRoot = this.getRepoRoot();
    
    // Ensure test fixtures are available (check only, don't fetch automatically)
    // The shareDir is like 'test-data/slskdn-test-fixtures/music'
    // We need to check the parent directory 'test-data/slskdn-test-fixtures'
    const shareDirPath = path.isAbsolute(this.config.shareDir)
      ? this.config.shareDir
      : path.join(repoRoot, this.config.shareDir);
    const fixturesRoot = path.dirname(shareDirPath); // Parent of 'music' or 'book'
    
    // Check if fixtures exist (basic validation only)
    // Full fixture fetching should be done in CI or manually via ./scripts/fetch-test-fixtures.sh
    try {
      await fs.access(fixturesRoot);
      const manifestPath = path.join(fixturesRoot, 'meta', 'manifest.json');
      await fs.access(manifestPath);
    } catch (error) {
      // Log warning but continue - static files may be enough for basic tests
      if (process.env.DEBUG) {
        console.warn(`[SlskdnNode] Fixture directory check: ${error}`);
      }
    }

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
    // Convert shareDir to absolute path (slskdn requires absolute paths)
    const shareDirAbsolute = path.isAbsolute(this.config.shareDir)
      ? this.config.shareDir
      : path.join(repoRoot, this.config.shareDir);
    
    const configPath = path.join(this.appDir, 'config', 'slskd.yml');
    const configYaml = `web:
  port: ${this.apiPort}
  host: 127.0.0.1
  authentication:
    username: ${this.config.nodeName === 'A' ? 'nodeA' : 'nodeB'}
    password: ${this.config.nodeName === 'A' ? 'nodeA' : 'nodeB'}
directories:
  downloads: ${path.join(this.appDir, 'downloads')}
  incomplete: ${path.join(this.appDir, 'incomplete')}
shares:
  directories:
    - ${shareDirAbsolute}
feature:
  identityFriends: true
  collectionsSharing: true
flags:
  no_connect: ${this.config.flags?.noConnect ?? (process.env.SLSKDN_TEST_NO_CONNECT === 'true')}
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
    
    const args = ['run', '--project', projectPath, '--no-build', '--', '--app-dir', this.appDir, '--config', configPath];

    this.process = spawn('dotnet', args, {
      cwd: repoRoot,
      stdio: ['ignore', 'pipe', 'pipe'],
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development'
      }
    });

    // Capture output for debugging (always log on error)
    let stdout = '';
    let stderr = '';
    
    this.process.stdout?.on('data', (data) => {
      const text = data.toString();
      stdout += text;
      if (process.env.DEBUG) {
        console.log(`[${this.config.nodeName}] ${text}`);
      }
    });
    
    this.process.stderr?.on('data', (data) => {
      const text = data.toString();
      stderr += text;
      if (process.env.DEBUG) {
        console.error(`[${this.config.nodeName}] ${text}`);
      }
    });

    // Handle process errors
    this.process.on('error', (err) => {
      throw new Error(`Failed to start slskdn process: ${err.message}`);
    });

    // Check if process exits early
    this.process.on('exit', (code, signal) => {
      if (code !== null && code !== 0) {
        const errorMsg = `slskdn process exited with code ${code}.\nSTDOUT:\n${stdout}\nSTDERR:\n${stderr}`;
        // Always log process exit errors
        console.error(`[${this.config.nodeName}] ${errorMsg}`);
      }
    });
    
    // Also log stderr immediately for debugging
    if (!process.env.DEBUG) {
      this.process.stderr?.on('data', (data) => {
        const text = data.toString();
        stderr += text;
        // Log errors even without DEBUG
        if (text.toLowerCase().includes('error') || text.toLowerCase().includes('exception') || text.toLowerCase().includes('fatal')) {
          console.error(`[${this.config.nodeName}] ${text}`);
        }
      });
    }

    // Wait for health endpoint
    const healthUrl = `${this.apiUrl}/health`;
    const startTime = Date.now();
    const timeout = 30000;
    
    while (Date.now() - startTime < timeout) {
      // Check if process died
      if (this.process.exitCode !== null) {
        if (this.process.exitCode !== 0) {
          const errorMsg = `slskdn process exited early with code ${this.process.exitCode}.\nSTDOUT:\n${stdout}\nSTDERR:\n${stderr}`;
          throw new Error(errorMsg);
        } else {
          // Process exited with 0, which is unexpected but might be OK
          break;
        }
      }
      
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
    
    // If we timeout, include any captured output
    const errorMsg = `Health check timeout for ${healthUrl} after ${timeout}ms`;
    if (stdout || stderr) {
      throw new Error(`${errorMsg}\nProcess output:\nSTDOUT:\n${stdout}\nSTDERR:\n${stderr}`);
    }
    throw new Error(errorMsg);
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
    return {
      baseUrl: this.apiUrl,
      username: this.config.nodeName === 'A' ? 'nodeA' : 'nodeB',
      password: this.config.nodeName === 'A' ? 'nodeA' : 'nodeB',
    };
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
