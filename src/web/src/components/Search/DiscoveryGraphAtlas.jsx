import React from 'react';
import { Header, Label, Segment } from 'semantic-ui-react';
import DiscoveryGraphCanvas from './DiscoveryGraphCanvas';

const DiscoveryGraphAtlas = ({
  edgeTypes = [],
  graph,
  maxDepth = 99,
  minNodeWeight = 0,
  onNodeClick,
}) => {
  const nodes = Array.isArray(graph?.nodes) ? graph.nodes : [];
  const visibleNodes = nodes.filter(
    (node) => (node.depth || 0) <= maxDepth && (node.weight || 0) >= minNodeWeight,
  );
  const visibleNodeIds = new Set(visibleNodes.map((node) => node.nodeId));
  const visibleEdges = (graph?.edges || []).filter(
    (edge) =>
      visibleNodeIds.has(edge.sourceNodeId) &&
      visibleNodeIds.has(edge.targetNodeId) &&
      (edgeTypes.length === 0 || edgeTypes.includes(edge.edgeType)),
  );

  const typeCounts = visibleNodes.reduce((accumulator, node) => {
    accumulator[node.nodeType] = (accumulator[node.nodeType] || 0) + 1;
    return accumulator;
  }, {});

  return (
    <Segment secondary>
      <Header as="h5" style={{ marginTop: 0 }}>
        Atlas
      </Header>
      <p style={{ marginTop: 0 }}>
        Wider neighborhood view with semantic filtering. Raise the threshold to
        suppress weak nodes and lower depth to stay at artist or album scale.
      </p>
      <div style={{ marginBottom: '0.75em' }}>
        {Object.entries(typeCounts).map(([type, count]) => (
          <Label key={type} size="tiny">
            {type} {count}
          </Label>
        ))}
        <Label size="tiny">Edges {visibleEdges.length}</Label>
      </div>
      <div
        style={{
          background:
            'radial-gradient(circle at top, rgba(59,130,246,0.12), rgba(15,23,42,0.04) 45%, rgba(255,255,255,0.96) 100%)',
          borderRadius: 8,
          padding: '0.75em',
        }}
      >
        <DiscoveryGraphCanvas
          edgeTypes={edgeTypes}
          graph={{
            ...graph,
            edges: visibleEdges,
            nodes: visibleNodes,
          }}
          onNodeClick={onNodeClick}
        />
      </div>
    </Segment>
  );
};

export default DiscoveryGraphAtlas;
