import {
  blockUser,
  filterResponse,
  getBlockedUsers,
  getResponses,
  getUserDownloadStats,
  parseFiltersFromString,
  unblockUser,
} from '../../../lib/searches';
import { sleep } from '../../../lib/util';
import ErrorSegment from '../../Shared/ErrorSegment';
import LoaderSegment from '../../Shared/LoaderSegment';
import Switch from '../../Shared/Switch';
import Response from '../Response';
import SearchDetailHeader from './SearchDetailHeader';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { toast } from 'react-toastify';
import { Button, Checkbox, Dropdown, Input, Segment } from 'semantic-ui-react';

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
  const [resultFilters, setResultFilters] = useState('');
  const [displayCount, setDisplayCount] = useState(5);
  const [userStats, setUserStats] = useState({});

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
    setDisplayCount(5);
  };

  const create = async ({ navigate, search: searchForCreate }) => {
    reset();
    onCreate({ navigate, searchForCreate });
  };

  const remove = async () => {
    reset();
    onRemove(search);
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
                Boolean(resultFilters) && {
                  color: 'red',
                  icon: 'x',
                  onClick: () => setResultFilters(''),
                }
              }
              className="search-filter"
              label={{ content: 'Filter', icon: 'filter' }}
              onChange={(_event, data) => setResultFilters(data.value)}
              placeholder="
                lackluster container -bothersome iscbr|isvbr islossless|islossy 
                minbitrate:320 minbitdepth:24 minfilesize:10 minfilesinfolder:8 minlength:5000
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
              onUnblock={() => handleUnblockUser(r.username)}
              response={r}
              smartScore={r.smartScore}
            />
          ))}
        {loaded &&
          (remainingCount > 0 ? (
            <Button
              className="showmore-button"
              fluid
              onClick={() => setDisplayCount(displayCount + 5)}
              primary
              size="large"
            >
              Show {remainingCount > 5 ? 5 : remainingCount} More Results{' '}
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
