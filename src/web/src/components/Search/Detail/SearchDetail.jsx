import {
  blockUser,
  filterResponse,
  getBlockedUsers,
  getResponses,
  getUserDownloadStats,
  parseFiltersFromString,
  unblockUser,
} from '../../../lib/searches';
import { getAllNotes } from '../../../lib/userNotes';
import { sleep } from '../../../lib/util';
import ErrorSegment from '../../Shared/ErrorSegment';
import LoaderSegment from '../../Shared/LoaderSegment';
import Switch from '../../Shared/Switch';
import Response from '../Response';
import SearchDetailHeader from './SearchDetailHeader';
import SearchFilterModal from './SearchFilterModal';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import {
  Button,
  Checkbox,
  Dropdown,
  Icon,
  Input,
  Segment,
} from 'semantic-ui-react';

const sortDropdownOptions = [
  {
    key: 'smart',
    text: '⭐ Smart Ranking (Best Overall)',
    value: 'smart',
  },
  {
    key: 'uploadSpeed',
    text: 'Upload Speed (Fastest to Slowest)',
    value: 'uploadSpeed',
  },
  {
    key: 'queueLength',
    text: 'Queue Depth (Least to Most)',
    value: 'queueLength',
  },
  {
    key: 'fileCount',
    text: 'File Count (Most to Least)',
    value: 'fileCount',
  },
];

// Smart ranking algorithm - combines multiple factors
const calculateSmartScore = (response, userStats) => {
  let score = 0;

  // Upload speed score (0-40 points) - normalized to max 10MB/s
  const speedScore = Math.min((response.uploadSpeed / 10_485_760) * 40, 40);
  score += speedScore;

  // Queue length score (0-30 points) - lower is better
  const queueScore = Math.max(30 - response.queueLength * 3, 0);
  score += queueScore;

  // Free slot bonus (15 points)
  if (response.hasFreeUploadSlot) {
    score += 15;
  }

  // Past download history bonus (0-15 points)
  const stats = userStats?.[response.username];
  if (stats) {
    // Successful downloads give positive score
    score += Math.min(stats.successfulDownloads * 3, 10);
    // Failed downloads subtract
    score -= Math.min(stats.failedDownloads * 2, 5);
  }

  return score;
};

// eslint-disable-next-line complexity
const SearchDetail = ({
  creating,
  disabled,
  onCreate,
  onRemove,
  onStop,
  removing,
  search,
  stopping,
}) => {
  const { fileCount, id, isComplete, lockedFileCount, responseCount, state } =
    search;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(undefined);

  const [results, setResults] = useState([]);

  // filters and sorting options
  const [hiddenResults, setHiddenResults] = useState([]);
  const [blockedUsers, setBlockedUsers] = useState(getBlockedUsers());
  const [hideBlockedUsers, setHideBlockedUsers] = useState(true);
  const [resultSort, setResultSort] = useState('smart');
  const [hideLocked, setHideLocked] = useState(true);
  const [hideNoFreeSlots, setHideNoFreeSlots] = useState(false);
  const [foldResults, setFoldResults] = useState(false);
  const [resultFilters, setResultFilters] = useState(
    localStorage.getItem('slskd-default-search-filter') || '',
  );
  const [pageSize, setPageSize] = useState(
    Number.parseInt(localStorage.getItem('slskd-search-page-size') || '25', 10),
  );
  const [displayCount, setDisplayCount] = useState(pageSize);
  const [userStats, setUserStats] = useState({});
  const [userNotes, setUserNotes] = useState({});

  const fetchUserNotes = useCallback(async () => {
    try {
      const response = await getAllNotes();
      const notesMap = response.data.reduce((accumulator, note) => {
        accumulator[note.username] = note;
        return accumulator;
      }, {});
      setUserNotes(notesMap);
    } catch (error_) {
      console.error('Failed to fetch user notes', error_);
    }
  }, []);

  useEffect(() => {
    fetchUserNotes();
  }, [fetchUserNotes]);

  const [hasSavedDefault, setHasSavedDefault] = useState(
    Boolean(localStorage.getItem('slskd-default-search-filter')),
  );

  // Sync hasSavedDefault across tabs/searches when localStorage changes
  useEffect(() => {
    const handleStorageChange = (event) => {
      if (event.key === 'slskd-default-search-filter') {
        setHasSavedDefault(Boolean(event.newValue));
      }
    };

    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, []);

  // Fetch user download stats for smart ranking
  useEffect(() => {
    const fetchStats = async () => {
      try {
        const stats = await getUserDownloadStats();
        setUserStats(stats);
      } catch {
        // Stats are optional, don't fail if unavailable
      }
    };

    fetchStats();
  }, []);

  // Handle blocking/unblocking users
  const handleBlockUser = useCallback((username) => {
    const updated = blockUser(username);
    setBlockedUsers(updated);
    toast.info(`Blocked ${username} from search results`);
  }, []);

  const handleUnblockUser = useCallback((username) => {
    const updated = unblockUser(username);
    setBlockedUsers(updated);
    toast.info(`Unblocked ${username}`);
  }, []);

  // when the search transitions from !isComplete -> isComplete,
  // fetch the results from the server
  useEffect(() => {
    const get = async () => {
      try {
        setLoading(true);

        // the results may not be ready yet.  this is very rare, but
        // if it happens the search will complete with no results.
        await sleep(500);

        const responses = await getResponses({ id });
        setResults(responses);
        setLoading(false);
      } catch (getError) {
        setError(getError);
        setLoading(false);
      }
    };

    if (isComplete) {
      get();
    }
  }, [id, isComplete]);

  // apply sorting and filters.  this can take a while for larger result
  // sets, so memoize it.
  const sortedAndFilteredResults = useMemo(() => {
    const sortOptions = {
      fileCount: { field: 'fileCount', order: 'desc' },
      queueLength: { field: 'queueLength', order: 'asc' },
      smart: { field: 'smartScore', order: 'desc' },
      uploadSpeed: { field: 'uploadSpeed', order: 'desc' },
    };

    const { field, order } = sortOptions[resultSort];

    const filters = parseFiltersFromString(resultFilters);

    return results
      .filter((r) => !hiddenResults.includes(r.username))
      .filter((r) => !(hideBlockedUsers && blockedUsers.includes(r.username)))
      .map((r) => {
        if (hideLocked) {
          return { ...r, lockedFileCount: 0, lockedFiles: [] };
        }

        return r;
      })
      .map((response) => filterResponse({ filters, response }))
      .filter((r) => r.fileCount + r.lockedFileCount > 0)
      .filter((r) => !(hideNoFreeSlots && !r.hasFreeUploadSlot))
      .map((r) => ({
        ...r,
        downloadStats: userStats[r.username],
        smartScore: calculateSmartScore(r, userStats),
      }))
      .sort((a, b) => {
        if (order === 'asc') {
          return a[field] - b[field];
        }

        return b[field] - a[field];
      });
  }, [
    blockedUsers,
    hiddenResults,
    hideBlockedUsers,
    hideLocked,
    hideNoFreeSlots,
    resultFilters,
    resultSort,
    results,
    userStats,
  ]);

  // when a user uses the action buttons, we will *probably* re-use this component,
  // but with a new search ID.  clear everything to prepare for the transition
  const reset = () => {
    setLoading(false);
    setError(undefined);
    setResults([]);
    setHiddenResults([]);
    setDisplayCount(pageSize);
  };

  const handlePageSizeChange = (newSize) => {
    setPageSize(newSize);
    localStorage.setItem('slskd-search-page-size', newSize);
    // If we're showing less than the new page size, expand to fill it
    if (displayCount < newSize) {
      setDisplayCount(newSize);
    }
  };

  const create = async ({ navigate, search: searchForCreate }) => {
    reset();
    onCreate({ navigate, searchForCreate });
  };

  const remove = async () => {
    reset();
    onRemove(search);
  };

  const saveAsDefault = () => {
    localStorage.setItem('slskd-default-search-filter', resultFilters);
    setHasSavedDefault(true);
    toast.success('Search filters saved as default');
  };

  const clearSavedDefault = () => {
    localStorage.removeItem('slskd-default-search-filter');
    setHasSavedDefault(false);
    toast.info('Saved default filter cleared');
  };

  const filteredCount = results?.length - sortedAndFilteredResults.length;
  const remainingCount = sortedAndFilteredResults.length - displayCount;
  const loaded = !removing && !creating && !loading && results;

  if (error) {
    return <ErrorSegment caption={error?.message ?? error} />;
  }

  return (
    <>
      <SearchDetailHeader
        creating={creating}
        disabled={disabled}
        loaded={loaded}
        loading={loading}
        onCreate={create}
        onRemove={remove}
        onStop={onStop}
        removing={removing}
        search={search}
        stopping={stopping}
      />
      <Switch
        loading={loading && <LoaderSegment />}
        searching={
          !isComplete && (
            <LoaderSegment>
              {state === 'InProgress'
                ? `Found ${fileCount} files ${
                    lockedFileCount > 0
                      ? `(plus ${lockedFileCount} locked) `
                      : ''
                  }from ${responseCount} users`
                : 'Loading results...'}
            </LoaderSegment>
          )
        }
      >
        {loaded && (
          <Segment
            className="search-options"
            raised
          >
            <Dropdown
              button
              className="search-options-sort icon"
              floating
              icon="sort"
              labeled
              onChange={(_event, { value }) => setResultSort(value)}
              options={sortDropdownOptions}
              text={
                sortDropdownOptions.find((o) => o.value === resultSort).text
              }
            />
            <Dropdown
              button
              className="search-options-pagesize"
              floating
              onChange={(_event, { value }) => handlePageSizeChange(value)}
              options={[
                { key: '10', text: '10 per page', value: 10 },
                { key: '25', text: '25 per page', value: 25 },
                { key: '50', text: '50 per page', value: 50 },
                { key: '100', text: '100 per page', value: 100 },
                { key: 'all', text: 'Show All', value: 999_999 },
              ]}
              style={{ marginLeft: '0.5em' }}
              text={pageSize >= 999_999 ? 'Show All' : `${pageSize} per page`}
            />
            <div className="search-option-toggles">
              <Checkbox
                checked={hideLocked}
                className="search-options-hide-locked"
                label="Hide Locked Results"
                onChange={() => setHideLocked(!hideLocked)}
                toggle
              />
              <Checkbox
                checked={hideNoFreeSlots}
                className="search-options-hide-no-slots"
                label="Hide Results with No Free Slots"
                onChange={() => setHideNoFreeSlots(!hideNoFreeSlots)}
                toggle
              />
              <Checkbox
                checked={hideBlockedUsers}
                className="search-options-hide-blocked"
                label={`Hide Blocked Users (${blockedUsers.length})`}
                onChange={() => setHideBlockedUsers(!hideBlockedUsers)}
                toggle
              />
              <Checkbox
                checked={foldResults}
                className="search-options-fold-results"
                label="Fold Results"
                onChange={() => setFoldResults(!foldResults)}
                toggle
              />
            </div>
            <Input
              action={
                <Button.Group>
                  {Boolean(resultFilters) && (
                    <Button
                      color="red"
                      icon="x"
                      onClick={() => setResultFilters('')}
                      title="Clear current filter"
                    />
                  )}
                  <Button
                    color="blue"
                    icon="save"
                    onClick={saveAsDefault}
                    title="Save as default filter"
                  />
                  {hasSavedDefault && (
                    <Button
                      color="orange"
                      icon="trash"
                      onClick={clearSavedDefault}
                      title="Clear saved default filter"
                    />
                  )}
                  <SearchFilterModal
                    filterString={resultFilters}
                    onChange={setResultFilters}
                    trigger={
                      <Button
                        icon
                        title="Advanced Filters"
                      >
                        <Icon name="sliders horizontal" />
                      </Button>
                    }
                  />
                </Button.Group>
              }
              className="search-filter"
              label={{ content: 'Filter', icon: 'filter' }}
              onChange={(_event, data) => setResultFilters(data.value)}
              placeholder="
                lackluster container -bothersome iscbr|isvbr islossless|islossy 
                minbr:320 minfilesize:100mb maxfilesize:2gb minfilesinfolder:8 minlength:5000
              "
              value={resultFilters}
            />
          </Segment>
        )}
        {loaded &&
          sortedAndFilteredResults.slice(0, displayCount).map((r) => (
            <Response
              disabled={disabled}
              downloadStats={r.downloadStats}
              isBlocked={blockedUsers.includes(r.username)}
              isInitiallyFolded={foldResults}
              key={r.username}
              onBlock={() => handleBlockUser(r.username)}
              onHide={() => setHiddenResults([...hiddenResults, r.username])}
              onNoteUpdate={fetchUserNotes}
              onUnblock={() => handleUnblockUser(r.username)}
              response={r}
              smartScore={r.smartScore}
              userNote={userNotes[r.username]}
            />
          ))}
        {loaded &&
          (remainingCount > 0 ? (
            <Button
              className="showmore-button"
              fluid
              onClick={() => setDisplayCount(displayCount + pageSize)}
              primary
              size="large"
            >
              Show {remainingCount > pageSize ? pageSize : remainingCount} More
              Results{' '}
              {`(${remainingCount} remaining, ${filteredCount} hidden by filter(s))`}
            </Button>
          ) : filteredCount > 0 ? (
            <Button
              className="showmore-button"
              disabled
              fluid
              size="large"
            >{`All results shown. ${filteredCount} results hidden by filter(s)`}</Button>
          ) : (
            ''
          ))}
      </Switch>
    </>
  );
};

export default SearchDetail;
