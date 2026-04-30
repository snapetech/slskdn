import './Search.css';
import {
  acquisitionProfiles,
  getAcquisitionProfile,
  getStoredAcquisitionProfileId,
  setStoredAcquisitionProfileId,
} from '../../lib/acquisitionProfiles';
import { createSearchHubConnection } from '../../lib/hubFactory';
import { getCapabilities } from '../../lib/slskdn';
import { getLocalStorageItem, setLocalStorageItem } from '../../lib/storage';
import * as library from '../../lib/searches';
import ErrorSegment from '../Shared/ErrorSegment';
import LoaderSegment from '../Shared/LoaderSegment';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import AlbumCompletionPanel from './AlbumCompletionPanel';
import DiscographyCoveragePanel from './DiscographyCoveragePanel';
import DiscoveryGraphAtlasPanel from './DiscoveryGraphAtlasPanel';
import SearchDetail from './Detail/SearchDetail';
import SearchList from './List/SearchList';
import MusicBrainzLookup from './MusicBrainzLookup';
import SongIDPanel from './SongIDPanel';
import React, { useEffect, useRef, useState } from 'react';
import {
  useLocation,
  useNavigate,
  useParams,
} from 'react-router-dom';
import { toast } from 'react-toastify';
import {
  Button,
  Checkbox,
  Dropdown,
  Header,
  Icon,
  Input,
  Popup,
  Segment,
} from 'semantic-ui-react';
import { v4 as uuidv4 } from 'uuid';

const CollapsibleSection = ({
  children,
  defaultOpen = true,
  storageKey,
  title,
}) => {
  const [open, setOpen] = useState(() => {
    if (!storageKey) {
      return defaultOpen;
    }

    const stored = getLocalStorageItem(storageKey);
    if (stored === null) {
      return defaultOpen;
    }

    return stored === 'open';
  });

  const toggleOpen = () => {
    setOpen((current) => {
      const next = !current;

      if (storageKey) {
        setLocalStorageItem(storageKey, next ? 'open' : 'closed');
      }

      return next;
    });
  };

  return (
    <Segment raised>
      <div
        style={{
          alignItems: 'center',
          display: 'flex',
          justifyContent: 'space-between',
          marginBottom: open ? '1em' : 0,
        }}
      >
        <Header
          as="h4"
          style={{ margin: 0 }}
        >
          {title}
        </Header>
        <Popup
          content={
            open
              ? `Collapse the ${title.toLowerCase()} panel to free up room on the page.`
              : `Expand the ${title.toLowerCase()} panel to inspect its contents.`
          }
          position="top center"
          trigger={
            <Button
              aria-label={`${open ? 'Collapse' : 'Expand'} ${title}`}
              icon
              onClick={toggleOpen}
              size="mini"
            >
              <Icon name={open ? 'angle up' : 'angle down'} />
            </Button>
          }
        />
      </div>
      {open ? children : null}
    </Segment>
  );
};

const Searches = ({ server } = {}) => {
  const normalizedServer = server ?? { isConnected: false };
  const [connecting, setConnecting] = useState(true);
  const [error, setError] = useState(undefined);
  const [searches, setSearches] = useState({});

  const [removing, setRemoving] = useState(false);
  const [removingAll, setRemovingAll] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [creating, setCreating] = useState(false);

  // Scene ↔ Pod Bridging provider selection (opt-in; normal search stays Soulseek-compatible by default)
  const [scenePodBridgeEnabled, setScenePodBridgeEnabled] = useState(false);
  const [providerPod, setProviderPod] = useState(true);
  const [providerScene, setProviderScene] = useState(true); // Enabled by default when feature is on
  const [showProviderOptions, setShowProviderOptions] = useState(false);
  const [acquisitionProfileId, setAcquisitionProfileId] = useState(() =>
    getStoredAcquisitionProfileId(getLocalStorageItem),
  );

  const inputRef = useRef();

  const location = useLocation();
  const routerNavigate = useNavigate();
  const { id: searchId } = useParams();
  const acquisitionProfile = getAcquisitionProfile(acquisitionProfileId);
  const acquisitionProfileOptions = acquisitionProfiles.map((profile) => ({
    content: (
      <div>
        <strong>{profile.label}</strong>
        <div className="search-acquisition-profile-option-summary">
          {profile.summary}
        </div>
      </div>
    ),
    icon: profile.icon,
    key: profile.id,
    text: profile.label,
    value: profile.id,
  }));

  const updateAcquisitionProfile = (event, { value }) => {
    setAcquisitionProfileId(
      setStoredAcquisitionProfileId(setLocalStorageItem, value).id,
    );
  };

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
        if (id) {
          routerNavigate(`/searches/${id}`, { replace: true });
          return;
        }

        routerNavigate('/searches', { replace: true });
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

    // Scene ↔ Pod Bridging is opt-in. Do not infer it from generic capabilities,
    // otherwise ordinary searches silently leave the proven Soulseek path.
    const checkFeatureFlag = async () => {
      try {
        const capabilities = await getCapabilities();
        const enabled =
          capabilities?.feature?.scenePodBridge === true ||
          capabilities?.features?.includes('scene_pod_bridge') === true;
        setScenePodBridgeEnabled(enabled);
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

      await library.create({
        acquisitionProfile: acquisitionProfile.id,
        id,
        providers,
        searchText,
      });

      if (ref) {
        ref.value = '';
        ref.focus();
      }

      setCreating(false);

      if (navigate) {
        routerNavigate(`/searches/${id}`);
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
          disabled={!normalizedServer.isConnected}
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
    routerNavigate('/searches', { replace: true });
  }

  inputRef?.current?.inputRef?.current.focus();

  return (
    <>
      <CollapsibleSection
        storageKey="slskdn.search.section.search"
        title="Search"
      >
        <Segment className="search-segment">
          <div className="search-segment-icon">
            <Icon
              name="search"
              size="big"
            />
          </div>
          <Input
            action={
              <>
                <Popup
                  content="Queue this search without leaving the search page."
                  position="top center"
                  trigger={
                    <Button
                      disabled={creating || !normalizedServer.isConnected}
                      icon="plus"
                      onClick={create}
                    />
                  }
                />
                <Popup
                  content="Start this search and open its detailed results immediately."
                  position="top center"
                  trigger={
                    <Button
                      disabled={creating || !normalizedServer.isConnected}
                      icon="search"
                      onClick={() => create({ navigate: true })}
                    />
                  }
                />
              </>
            }
            className="search-input"
            disabled={creating || !normalizedServer.isConnected}
            input={
              <input
                data-lpignore="true"
                data-testid="search-input"
                placeholder={
                  normalizedServer.isConnected
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
          <div className="search-acquisition-profile-strip">
            <div className="search-acquisition-profile-label">
              <Icon name={acquisitionProfile.icon} />
              Acquisition Profile
            </div>
            <Popup
              content={`${acquisitionProfile.label}: ${acquisitionProfile.description}`}
              position="top center"
              trigger={
                <Dropdown
                  aria-label="Acquisition profile"
                  className="search-acquisition-profile-dropdown"
                  data-testid="acquisition-profile-select"
                  onChange={updateAcquisitionProfile}
                  options={acquisitionProfileOptions}
                  selection
                  value={acquisitionProfile.id}
                />
              }
            />
            <span className="search-acquisition-profile-summary">
              {acquisitionProfile.summary}
            </span>
          </div>
        </Segment>
      </CollapsibleSection>
      <CollapsibleSection
        defaultOpen={false}
        storageKey="slskdn.search.section.songid"
        title="SongID"
      >
        <SongIDPanel disabled={!normalizedServer.isConnected} />
      </CollapsibleSection>
      <CollapsibleSection
        defaultOpen={false}
        storageKey="slskdn.search.section.musicbrainz"
        title="MusicBrainz Lookup"
      >
        <MusicBrainzLookup disabled={!normalizedServer.isConnected} />
      </CollapsibleSection>
      <CollapsibleSection
        defaultOpen={false}
        storageKey="slskdn.search.section.discographyCoverage"
        title="Discography Concierge"
      >
        <DiscographyCoveragePanel disabled={!normalizedServer.isConnected} />
      </CollapsibleSection>
      <CollapsibleSection
        defaultOpen={false}
        storageKey="slskdn.search.section.discoveryGraphAtlas"
        title="Discovery Graph Atlas"
      >
        <DiscoveryGraphAtlasPanel disabled={!normalizedServer.isConnected} />
      </CollapsibleSection>
      <CollapsibleSection
        defaultOpen={false}
        storageKey="slskdn.search.section.albumCompletion"
        title="Album Completion"
      >
        <AlbumCompletionPanel disabled={!normalizedServer.isConnected} />
      </CollapsibleSection>
      <CollapsibleSection
        defaultOpen
        storageKey="slskdn.search.section.searchResults"
        title="Search Results"
      >
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
      </CollapsibleSection>
    </>
  );
};

export default Searches;
