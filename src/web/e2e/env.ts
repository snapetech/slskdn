export type NodeCfg = {
  baseUrl: string;
  password: string;
  username: string;
};

/**
 * Node configuration.
 *
 * If SLSKDN_NODE_A_URL is set, nodes are expected to be pre-launched.
 * Otherwise, tests should use MultiPeerHarness to launch nodes.
 */
export const NODES = {
  A: {
    baseUrl: process.env.SLSKDN_NODE_A_URL ?? 'http://127.0.0.1:5030',
    password: process.env.SLSKDN_NODE_A_PASS ?? 'nodeA',
    username: process.env.SLSKDN_NODE_A_USER ?? 'nodeA',
  },
  B: {
    baseUrl: process.env.SLSKDN_NODE_B_URL ?? 'http://127.0.0.1:5031',
    password: process.env.SLSKDN_NODE_B_PASS ?? 'nodeB',
    username: process.env.SLSKDN_NODE_B_USER ?? 'nodeB',
  },
  C: {
    baseUrl: process.env.SLSKDN_NODE_C_URL ?? 'http://127.0.0.1:5032',
    password: process.env.SLSKDN_NODE_C_PASS ?? 'nodeC',
    username: process.env.SLSKDN_NODE_C_USER ?? 'nodeC',
  },
} satisfies Record<string, NodeCfg>;

/**
 * Check if nodes should be launched by harness (vs pre-launched).
 */
export function shouldLaunchNodes(): boolean {
  const hasExternalNodes =
    Boolean(process.env.SLSKDN_NODE_A_URL) ||
    Boolean(process.env.SLSKDN_NODE_B_URL) ||
    Boolean(process.env.SLSKDN_NODE_C_URL);
  if (hasExternalNodes) {
    console.warn(
      '[E2E] Using pre-launched nodes (SLSKDN_NODE_*_URL set). ' +
        'Ensure the Web UI bundle is up to date or unset these vars to let the harness rebuild/sync it.',
    );
  }

  return !hasExternalNodes;
}
