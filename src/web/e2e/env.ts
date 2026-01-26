export type NodeCfg = {
  baseUrl: string;
  username: string;
  password: string;
};

/**
 * Node configuration.
 * 
 * If SLSKDN_NODE_A_URL is set, nodes are expected to be pre-launched.
 * Otherwise, tests should use MultiPeerHarness to launch nodes.
 */
export const NODES = {
  A: {
    baseUrl: process.env.SLSKDN_NODE_A_URL ?? "http://127.0.0.1:5030",
    username: process.env.SLSKDN_NODE_A_USER ?? "nodeA",
    password: process.env.SLSKDN_NODE_A_PASS ?? "nodeA",
  },
  B: {
    baseUrl: process.env.SLSKDN_NODE_B_URL ?? "http://127.0.0.1:5031",
    username: process.env.SLSKDN_NODE_B_USER ?? "nodeB",
    password: process.env.SLSKDN_NODE_B_PASS ?? "nodeB",
  },
} satisfies Record<string, NodeCfg>;

/**
 * Check if nodes should be launched by harness (vs pre-launched).
 */
export function shouldLaunchNodes(): boolean {
  return !process.env.SLSKDN_NODE_A_URL && !process.env.SLSKDN_NODE_B_URL;
}
