import './Search.css';
import { createSearchHubConnection } from '../../lib/hubFactory';
import * as library from '../../lib/searches';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import AlbumCompletionPanel from './AlbumCompletionPanel';
import SearchDetail from './Detail/SearchDetail';
import SearchList from './List/SearchList';
import MusicBrainzLookup from './MusicBrainzLookup';
import React, { useEffect, useRef, useState } from 'react';
import {
  useHistory,
  useLocation,
  useParams,
  useRouteMatch,
} from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Checkbox, Icon, Input, Segment } from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';

const Searches = ({ server } = {}) => {
  const [connecting, setConnecting] = useState(true);
  const [error, setError] = useState(undefined);
  const [searches, setSearches] = useState({});

  const [removing, setRemoving] = useState(false);
  const [removingAll, setRemovingAll] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [creating, setCreating] = useState(false);

  // Scene ↔ Pod Bridging provider selection (enabled by default)
  const [scenePodBridgeEnabled, setScenePodBridgeEnabled] = useState(true);
  const [providerPod, setProviderPod] = useState(true);
  const [providerScene, setProviderScene] = useState(true); // Enabled by default when feature is on
  const [showProviderOptions, setShowProviderOptions] = useState(false);

  const inputRef = useRef();

  const history = useHistory();
  const location = useLocation();
  const match = useRouteMatch();
  const { id: searchId } = useParams();

  // Handle URL query parameters for predictable search URLs
  useEffect(() => {
    const urlParameters = new URLSearchParams(location.search);
    const queryParameter = urlParameters.get('q');

    if (queryParameter && !creating && !searchId) {
      // Automatically create a search from the URL query parameter
      create({
        navigate: false,
        search: decodeURIComponent(queryParameter),
      }).then((id) => {
        if (id) history.replace(`${match.url}/${id}`);
        else history.replace({ search: '' });
      });
    }
  }, [location.search, creating, searchId]); // eslint-disable-line react-hooks/exhaustive-deps

  const onConnecting = () => {
    setConnecting(true);
  };

  const onConnected = () => {
    setConnecting(false);
    setError(undefined);
  };

  const onConnectionError = (connectionError) => {
    setConnecting(false);
    setError(connectionError);
  };

  const onUpdate = (update) => {
    setSearches(update);
    onConnected();
  };

  useEffect(() => {
    onConnecting();

    const searchHub = createSearchHubConnection();

    searchHub.on('list', (searchesEvent) => {
      onUpdate(
        searchesEvent.reduce((accumulator, search) => {
          accumulator[search.id] = search;
          return accumulator;
        }, {}),
      );
      onConnected();
    });

    searchHub.on('update', (search) => {
      onUpdate((old) => ({ ...old, [search.id]: search }));
    });

    searchHub.on('delete', (search) => {
      onUpdate((old) => {
        delete old[search.id];
        return { ...old };
      });
    });

    searchHub.on('create', (search) => {
      onUpdate((old) => ({ ...old, [search.id]: search }));
    });

    searchHub.onreconnecting((connectionError) =>
      onConnectionError(connectionError?.message ?? 'Disconnected'),
    );
    searchHub.onreconnected(() => onConnected());
    searchHub.onclose((connectionError) =>
      onConnectionError(connectionError?.message ?? 'Disconnected'),
    );

    const connect = async () => {
      try {
        onConnecting();
        await searchHub.start();
      } catch (connectionError) {
        toast.error(connectionError?.message ?? 'Failed to connect');
        onConnectionError(connectionError?.message ?? 'Failed to connect');
      }
    };

    connect();

    // Check if Scene ↔ Pod Bridging is enabled
    const checkFeatureFlag = async () => {
      try {
        // Check if Scene ↔ Pod Bridging is enabled (default: true)
        // For now, we'll enable UI by default and check capabilities
        // In a full implementation, we'd check a specific flag from options
        try {
          const response = await fetch('/api/slskdn/capabilities');
          if (response.ok) {
            const data = await response.json();
            // Feature is enabled by default, but we can check if it's explicitly disabled
            // For now, assume enabled unless explicitly disabled
            setScenePodBridgeEnabled(true);
          }
        } catch (error_) {
          // Feature flag check failed - assume enabled by default
          console.debug(
            'Scene ↔ Pod Bridging feature flag check failed:',
            error_,
          );
          setScenePodBridgeEnabled(true);
        }
      } catch (error_) {
        // Feature flag check failed - assume disabled
        console.debug(
          'Scene ↔ Pod Bridging feature flag check failed:',
          error_,
        );
      }
    };

    checkFeatureFlag();

    return () => {
      searchHub.stop();
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // create a new search, and optionally navigate to it to display the details
  // we do this if the user clicks the search icon, or repeats an existing search
  const create = async ({ navigate = false, search } = {}) => {
    const ref = inputRef?.current?.inputRef?.current;
    const searchText = search || ref?.value;
    const id = uuidv4();

    if (!searchText) {
      toast.error('Please enter a search phrase');
      return;
    }

    try {
      setCreating(true);

      // Include provider selection if Scene ↔ Pod Bridging is enabled
      const providers = scenePodBridgeEnabled
        ? [providerPod && 'pod', providerScene && 'scene'].filter(Boolean)
        : null;

      await library.create({ id, providers, searchText });

      if (ref) {
        ref.value = '';
        ref.focus();
      }

      setCreating(false);

      if (navigate) {
        history.push(`${match.url}/${id}`);
      }

      return id;
    } catch (createError) {
      console.error(createError);
      toast.error(
        createError?.response?.data ?? createError?.message ?? createError,
      );
      setCreating(false);
    }
  };

  // delete a search
  const remove = async (search) => {
    try {
      setRemoving(true);

      await library.remove({ id: search.id });
      setSearches((old) => {
        delete old[search.id];
        return { ...old };
      });

      setRemoving(false);
    } catch (error_) {
      console.error(error_);
      toast.error(error?.response?.data ?? error?.message ?? error);
      setRemoving(false);
    }
  };

  // delete all searches
  const removeAll = async () => {
    try {
      setRemovingAll(true);
      const result = await library.removeAll();
      setSearches({});
      toast.success(`Cleared ${result?.data?.deleted ?? 'all'} searches`);
      setRemovingAll(false);
    } catch (removeAllError) {
      console.error(removeAllError);
      toast.error(
        removeAllError?.response?.data ??
          removeAllError?.message ??
          removeAllError,
      );
      setRemovingAll(false);
    }
  };

  // stop an in-progress search
  const stop = async (search) => {
    try {
      setStopping(true);
      await library.stop({ id: search.id });
      setStopping(false);
    } catch (stoppingError) {
      console.error(stoppingError);
      toast.error(
        stoppingError?.response?.data ??
          stoppingError?.message ??
          stoppingError,
      );
      setStopping(false);
    }
  };

  if (connecting) {
    return <LoaderSegment />;
  }

  if (error) {
    return <ErrorSegment caption={error?.message ?? error} />;
  }

  // if searchId is not null, there's an id in the route.
  // display the details for the search, if there is one
  if (searchId) {
    if (searches[searchId]) {
      return (
        <SearchDetail
          creating={creating}
          disabled={!server?.isConnected}
          onCreate={create}
          onRemove={remove}
          onStop={stop}
          removing={removing}
          search={searches[searchId]}
          stopping={stopping}
        />
      );
    }

    // if the searchId doesn't match a search we know about, chop
    // the id off of the url and force navigation back to the list
    history.replace(match.url.replace(`/${searchId}`, ''));
  }

  inputRef?.current?.inputRef?.current.focus();

  return (
    <>
      <Segment
        className="search-segment"
        raised
      >
        <div className="search-segment-icon">
          <Icon
            name="search"
            size="big"
          />
        </div>
        <Input
          action={
            <>
              <Button
                disabled={creating || !server.isConnected}
                icon="plus"
                onClick={create}
              />
              <Button
                disabled={creating || !server.isConnected}
                icon="search"
                onClick={() => create({ navigate: true })}
              />
            </>
          }
          className="search-input"
          disabled={creating || !server.isConnected}
          input={
            <input
              data-lpignore="true"
              data-testid="search-input"
              placeholder={
                server.isConnected
                  ? 'Search phrase'
                  : 'Connect to server to perform a search'
              }
              type="search"
            />
          }
          loading={creating}
          onKeyUp={(keyUpEvent) => (keyUpEvent.key === 'Enter' ? create() : '')}
          placeholder="Search phrase"
          ref={inputRef}
          size="big"
        />
        {scenePodBridgeEnabled && (
          <div
            style={{
              background: 'rgba(0,0,0,0.05)',
              borderRadius: '4px',
              marginTop: '0.75em',
              padding: '0.75em',
            }}
          >
            <div
              style={{
                alignItems: 'center',
                display: 'flex',
                flexWrap: 'wrap',
                gap: '1em',
              }}
            >
              <span style={{ fontSize: '0.95em', fontWeight: 'bold' }}>
                Search Sources:
              </span>
              <Checkbox
                checked={providerPod}
                label={
                  <label>
                    <Icon
                      name="sitemap"
                      style={{ marginRight: '0.25em' }}
                    />
                    Pod/Mesh
                  </label>
                }
                onChange={(e, { checked }) => setProviderPod(checked)}
                toggle
              />
              <Checkbox
                checked={providerScene}
                label={
                  <label>
                    <Icon
                      name="globe"
                      style={{ marginRight: '0.25em' }}
                    />
                    Soulseek Scene
                  </label>
                }
                onChange={(e, { checked }) => setProviderScene(checked)}
                toggle
              />
              {!providerPod && !providerScene && (
                <span
                  style={{
                    color: 'orange',
                    fontSize: '0.9em',
                    fontStyle: 'italic',
                  }}
                >
                  <Icon name="warning" /> At least one source must be selected
                </span>
              )}
            </div>
          </div>
        )}
      </Segment>
      <MusicBrainzLookup disabled={!server?.isConnected} />
      <AlbumCompletionPanel disabled={!server?.isConnected} />
      {Object.keys(searches).length === 0 ? (
        <PlaceholderSegment
          caption="No searches to display"
          icon="search"
        />
      ) : (
        <SearchList
          connecting={connecting}
          error={error}
          onRemove={remove}
          onRemoveAll={removeAll}
          onStop={stop}
          removingAll={removingAll}
          searches={searches}
        />
      )}
    </>
  );
};

export default Searches;
