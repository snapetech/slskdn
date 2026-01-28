import * as http from 'http';
import * as net from 'net';
import { findFreePort } from '../fixtures/helpers';

/**
 * Fake Solid server for E2E tests.
 * Serves WebID profiles (Turtle format) and OIDC discovery documents.
 * Runs on localhost with an allowed hostname for SSRF testing.
 */
export class FakeSolidServer {
  private server: http.Server | null = null;
  private port: number = 0;
  private baseUrl: string = '';

  /**
   * Start the fake Solid server.
   */
  async start(): Promise<void> {
    this.port = await findFreePort();
    this.baseUrl = `http://localhost:${this.port}`;

    this.server = http.createServer((req, res) => {
      const url = new URL(req.url!, `http://${req.headers.host}`);

      // CORS headers for browser requests
      res.setHeader('Access-Control-Allow-Origin', '*');
      res.setHeader('Access-Control-Allow-Methods', 'GET, OPTIONS');
      res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

      if (req.method === 'OPTIONS') {
        res.writeHead(200);
        res.end();
        return;
      }

      // WebID profile endpoint (Turtle format)
      if (url.pathname === '/profile/card' || url.pathname === '/profile/card#me') {
        res.setHeader('Content-Type', 'text/turtle');
        res.writeHead(200);
        res.end(`@prefix solid: <http://www.w3.org/ns/solid/terms#>.
@prefix foaf: <http://xmlns.com/foaf/0.1/>.

<${this.baseUrl}/profile/card#me>
  a foaf:Person;
  foaf:name "Test User";
  solid:oidcIssuer <${this.baseUrl}/oidc>.
`);
        return;
      }

      // OIDC discovery endpoint
      if (url.pathname === '/.well-known/openid-configuration') {
        res.setHeader('Content-Type', 'application/json');
        res.writeHead(200);
        res.end(JSON.stringify({
          issuer: `${this.baseUrl}/oidc`,
          authorization_endpoint: `${this.baseUrl}/oidc/authorize`,
          token_endpoint: `${this.baseUrl}/oidc/token`,
          userinfo_endpoint: `${this.baseUrl}/oidc/userinfo`,
          jwks_uri: `${this.baseUrl}/oidc/jwks`
        }));
        return;
      }

      // 404 for unknown paths
      res.writeHead(404);
      res.end('Not Found');
    });

    return new Promise((resolve, reject) => {
      this.server!.listen(this.port, '127.0.0.1', () => {
        resolve();
      });
      this.server!.on('error', reject);
    });
  }

  /**
   * Get the base URL of the fake server.
   */
  getBaseUrl(): string {
    return this.baseUrl;
  }

  /**
   * Get the WebID URL for testing.
   */
  getWebIdUrl(): string {
    return `${this.baseUrl}/profile/card#me`;
  }

  /**
   * Get the hostname (for AllowedHosts config).
   */
  getHostname(): string {
    return 'localhost';
  }

  /**
   * Stop the server.
   */
  async stop(): Promise<void> {
    if (this.server) {
      return new Promise((resolve) => {
        this.server!.close(() => resolve());
        this.server = null;
      });
    }
  }
}
