import { NodeConfig, SlskdnNode } from './SlskdnNode';

/**
 * Manages multiple slskdn test nodes for multi-peer scenarios.
 * Handles lifecycle (start/stop) and provides access to nodes.
 */
export class MultiPeerHarness {
  private nodes: Map<string, SlskdnNode> = new Map();

  /**
   * Start a new test node.
   * @param name Node name (A, B, C, etc.)
   * @param shareDir Single share directory path, or array of paths for multiple shares
   * @param flags Optional flags (noConnect, etc.)
   */
  async startNode(
    name: string,
    shareDir: string | string[],
    flags?: { noConnect?: boolean },
  ): Promise<SlskdnNode> {
    if (this.nodes.has(name)) {
      throw new Error(`Node ${name} already exists`);
    }

    // Small delay between starting nodes to avoid lock file conflicts
    if (this.nodes.size > 0) {
      await new Promise((resolve) => setTimeout(resolve, 1_000));
    }

    const node = new SlskdnNode({
      flags,
      nodeName: name,
      shareDir,
    });

    await node.start();
    this.nodes.set(name, node);
    return node;
  }

  /**
   * Get a node by name.
   */
  getNode(name: string): SlskdnNode {
    const node = this.nodes.get(name);
    if (!node) {
      throw new Error(`Node ${name} not found`);
    }

    return node;
  }

  /**
   * Stop all nodes and clean up.
   */
  async stopAll(): Promise<void> {
    await Promise.all([...this.nodes.values()].map((node) => node.stop()));
    this.nodes.clear();
  }

  /**
   * Get all node names.
   */
  getNodeNames(): string[] {
    return [...this.nodes.keys()];
  }
}
