import SearchStatusIcon from '../SearchStatusIcon';
import { buildDiscoveryGraph } from '../../../lib/discoveryGraph';
import DiscoveryGraphModal from '../DiscoveryGraphModal';
import * as searches from '../../../lib/searches';
import SearchActionIcon from './SearchActionIcon';
import React, { useState } from 'react';
import { Link, useRouteMatch } from 'react-router-dom';
import { Icon, Popup, Table } from 'semantic-ui-react';
import { toast } from 'react-toastify';

const SearchListRow = ({ onRemove, onStop, search }) => {
  const [working, setWorking] = useState(false);
  const [graphData, setGraphData] = useState(null);
  const [graphLoading, setGraphLoading] = useState(false);
  const [graphOpen, setGraphOpen] = useState(false);
  const [graphRequest, setGraphRequest] = useState(null);
  const match = useRouteMatch();

  const invoke = async (function_) => {
    setWorking(true);

    try {
      await function_();
    } catch (error) {
      console.error(error);
    } finally {
      setWorking(false);
    }
  };

  const openDiscoveryGraph = async (request) => {
    setGraphLoading(true);
    setGraphOpen(true);
    setGraphRequest(request);

    try {
      const graph = await buildDiscoveryGraph(request);
      setGraphData(graph);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to build discovery graph',
      );
      setGraphOpen(false);
    } finally {
      setGraphLoading(false);
    }
  };

  const handleOpenGraph = async () => {
    await openDiscoveryGraph({
      artist: search.searchText,
      scope: 'songid_run',
      title: search.searchText,
    });
  };

  const handleGraphRecenter = async (nodeId) => {
    if (!nodeId) {
      return;
    }

    const [nodeType, rawId] = nodeId.split(':');
    if (nodeType === 'artist') {
      await openDiscoveryGraph({ scope: 'artist', artistId: rawId });
      return;
    }

    if (nodeType === 'album' || nodeType === 'release-group') {
      await openDiscoveryGraph({ scope: 'album', releaseId: rawId });
      return;
    }

    if (nodeType === 'track') {
      await openDiscoveryGraph({ scope: 'track', recordingId: rawId });
      return;
    }

    await handleOpenGraph();
  };

  const handleGraphCompare = async (nodeId, label) => {
    if (!graphRequest || !nodeId) {
      return;
    }

    await openDiscoveryGraph({
      ...graphRequest,
      compareLabel: label,
      compareNodeId: nodeId,
    });
  };

  const handleQueueNearby = async (graph) => {
    const queries = (graph?.nodes || [])
      .filter((node) => node.nodeType === 'track')
      .map((node) => node.label || '')
      .filter(Boolean)
      .slice(0, 8);

    if (queries.length === 0) {
      toast.error('No nearby track nodes were available to queue');
      return;
    }

    try {
      const count = await searches.createBatch({ queries });
      toast.success(`Started ${count} nearby graph searches`);
    } catch (error) {
      console.error(error);
      toast.error(
        error?.response?.data ?? error?.message ?? 'Failed to queue nearby graph searches',
      );
    }
  };

  return (
    <>
      <Table.Row
        disabled={working}
        style={{ cursor: working ? 'wait' : undefined }}
      >
        <Table.Cell>
          <SearchStatusIcon state={search.state} />
        </Table.Cell>
        <Table.Cell>
          <Link to={`${match.url}/${encodeURIComponent(search.id)}`}>
            {search.searchText}
          </Link>
          <Popup
            content="Open a Discovery Graph for this search phrase so the query history becomes a browsable neighborhood."
            position="top center"
            trigger={
              <Icon
                color="blue"
                link
                name="crosshairs"
                onClick={handleOpenGraph}
                style={{ marginLeft: '0.5em' }}
              />
            }
          />
        </Table.Cell>
        <Table.Cell>{search.fileCount}</Table.Cell>
        <Table.Cell>
          <Icon
            color="yellow"
            name="lock"
            size="small"
          />
          {search.lockedFileCount}
        </Table.Cell>
        <Table.Cell>{search.responseCount}</Table.Cell>
        <Table.Cell>
          {search.endedAt ? new Date(search.endedAt).toLocaleTimeString() : '-'}
        </Table.Cell>
        <Table.Cell>
          <SearchActionIcon
            loading={working}
            onRemove={() => invoke(() => onRemove(search))}
            onStop={() => invoke(() => onStop(search))}
            search={search}
            style={{ cursor: 'pointer' }}
          />
        </Table.Cell>
      </Table.Row>
      <DiscoveryGraphModal
        graph={graphData}
        loading={graphLoading}
        onClose={() => setGraphOpen(false)}
        onCompare={handleGraphCompare}
        onQueueNearby={handleQueueNearby}
        onRecenter={handleGraphRecenter}
        onRestoreBranch={(branch) => branch?.request && openDiscoveryGraph(branch.request)}
        open={graphOpen}
      />
    </>
  );
};

export default SearchListRow;
