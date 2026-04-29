import './Transfers.css';
import * as autoReplaceLibrary from '../../lib/autoReplace';
import * as transfersLibrary from '../../lib/transfers';
import { LoaderSegment, PlaceholderSegment } from '../Shared';
import TransferGroup from './TransferGroup';
import TransfersHeader from './TransfersHeader';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { toast } from 'react-toastify';

const AUTO_REPLACE_THRESHOLD = 0; // 0% = exact match only (configurable on backend)

const getErrorMessage = (error) =>
  error?.response?.data ?? error?.message ?? `${error}`;

const summarizeBulkFailures = ({ action, failures }) => {
  if (failures.length === 0) {
    return;
  }

  const [firstFailure] = failures;
  toast.error(
    failures.length === 1
      ? `Failed to ${action} ${firstFailure.label}: ${firstFailure.message}`
      : `Failed to ${action} ${failures.length} transfer(s). First error: ${firstFailure.label}: ${firstFailure.message}`,
  );
};

const getTransferKey = ({ file, suffix = '' }) => {
  return `${file.username}:${file.id}${suffix ? `:${suffix}` : ''}`;
};

const OPTIMISTIC_HIDE_MS = 15_000;
const QUEUE_POSITION_REFRESH_MS = 30_000;
const MAX_QUEUE_POSITION_LOOKUPS_PER_FETCH = 5;

const Transfers = ({ direction, server }) => {
  const testId = direction === 'download' ? 'downloads-root' : 'uploads-root';
  const [connecting, setConnecting] = useState(true);
  const [transfers, setTransfers] = useState([]);

  const [retryingSingle, setRetryingSingle] = useState(false);
  const [cancellingSingle, setCancellingSingle] = useState(false);
  const [removingSingle, setRemovingSingle] = useState(false);
  const [bulkCounts, setBulkCounts] = useState({ retry: 0, cancel: 0, remove: 0 });

  const [autoReplaceEnabled, setAutoReplaceEnabled] = useState(false);
  const [acceleratedEnabled, setAcceleratedEnabled] = useState(false);
  const autoReplaceThreshold = AUTO_REPLACE_THRESHOLD;

  const bulkQueueRef = useRef([]);
  const queuedBulkKeysRef = useRef(new Set());
  const bulkQueueRunningRef = useRef(false);
  const hiddenTransfersRef = useRef(new Map());
  const latestFetchIdRef = useRef(0);
  const lastQueuePositionBatchAtRef = useRef(0);
  const queuePositionCacheRef = useRef(new Map());
  const queuePositionRequestsRef = useRef(new Set());

  const retrying = retryingSingle || bulkCounts.retry > 0;
  const cancelling = cancellingSingle || bulkCounts.cancel > 0;
  const removing = removingSingle || bulkCounts.remove > 0;

  const changeBulkCount = (action, delta) => {
    setBulkCounts((previousCounts) => ({
      ...previousCounts,
      [action]: Math.max(0, previousCounts[action] + delta),
    }));
  };

  const isOptimisticallyHidden = (file, now = Date.now()) => {
    const entry = hiddenTransfersRef.current.get(getTransferKey({ file }));

    if (!entry) {
      return false;
    }

    if (entry.until <= now) {
      hiddenTransfersRef.current.delete(getTransferKey({ file }));
      return false;
    }

    return entry.matches(file);
  };

  const filterHiddenTransfers = (users) => {
    const now = Date.now();

    return users
      .map((user) => ({
        ...user,
        directories: user.directories
          .map((directory) => ({
            ...directory,
            files: directory.files.filter(
              (file) => !isOptimisticallyHidden(file, now),
            ),
          }))
          .filter((directory) => directory.files.length > 0),
      }))
      .filter((user) => user.directories.length > 0);
  };

  const hideTransfers = (files, matches = () => true) => {
    const until = Date.now() + OPTIMISTIC_HIDE_MS;

    files.forEach((file) => {
      hiddenTransfersRef.current.set(getTransferKey({ file }), {
        matches,
        until,
      });
    });

    setTransfers((previousTransfers) =>
      filterHiddenTransfers(previousTransfers),
    );
  };

  const runBulkQueue = async () => {
    if (bulkQueueRunningRef.current) {
      return;
    }

    bulkQueueRunningRef.current = true;

    while (bulkQueueRef.current.length > 0) {
      const queuedOperation = bulkQueueRef.current.shift();

      try {
        await queuedOperation.run();
      } catch (error) {
        queuedOperation.batch.failures.push({
          label: queuedOperation.label,
          message: getErrorMessage(error),
        });
      } finally {
        queuedBulkKeysRef.current.delete(queuedOperation.key);
        changeBulkCount(queuedOperation.action, -1);
        queuedOperation.batch.remaining -= 1;

        if (queuedOperation.batch.remaining === 0) {
          summarizeBulkFailures({
            action: queuedOperation.batch.action,
            failures: queuedOperation.batch.failures,
          });
        }
      }
    }

    bulkQueueRunningRef.current = false;
  };

  const enqueueBulkOperations = ({ action, operations }) => {
    const batch = {
      action,
      failures: [],
      remaining: 0,
    };

    let enqueuedCount = 0;

    operations.forEach((operation) => {
      if (queuedBulkKeysRef.current.has(operation.key)) {
        return;
      }

      queuedBulkKeysRef.current.add(operation.key);
      bulkQueueRef.current.push({
        ...operation,
        action,
        batch,
      });
      batch.remaining += 1;
      enqueuedCount += 1;
    });

    if (enqueuedCount === 0) {
      return;
    }

    changeBulkCount(action, enqueuedCount);
    runBulkQueue();
  };

  const refreshQueuePositions = async (users) => {
    if (direction !== 'download') {
      return;
    }

    const now = Date.now();
    const queuedDownloads = users
      .flatMap((user) => user.directories.flatMap((dir) => dir.files))
      .filter((file) => file.state && file.state.includes('Queued'));

    queuedDownloads.forEach((file) => {
      const cached = queuePositionCacheRef.current.get(getTransferKey({ file }));
      if (cached?.placeInQueue) {
        file.placeInQueue = cached.placeInQueue;
      }
    });

    if (
      lastQueuePositionBatchAtRef.current > 0 &&
      now - lastQueuePositionBatchAtRef.current < QUEUE_POSITION_REFRESH_MS
    ) {
      return;
    }

    const dueDownloads = queuedDownloads
      .filter((file) => {
        const key = getTransferKey({ file });
        const cached = queuePositionCacheRef.current.get(key);

        return (
          !queuePositionRequestsRef.current.has(key) &&
          (!cached || now - cached.updatedAt >= QUEUE_POSITION_REFRESH_MS)
        );
      })
      .slice(0, MAX_QUEUE_POSITION_LOOKUPS_PER_FETCH);

    if (dueDownloads.length === 0) {
      return;
    }

    lastQueuePositionBatchAtRef.current = now;

    const queuePositionPromises = dueDownloads.map(async (file) => {
      const key = getTransferKey({ file });
      queuePositionRequestsRef.current.add(key);

      try {
        const queueResponse = await transfersLibrary.getPlaceInQueue({
          id: file.id,
          username: file.username,
        });

        queuePositionCacheRef.current.set(key, {
          placeInQueue: queueResponse.data,
          updatedAt: Date.now(),
        });
        file.placeInQueue = queueResponse.data;
      } catch (error) {
        console.debug(
          'Failed to fetch queue position for',
          file.filename,
          error,
        );
      } finally {
        queuePositionRequestsRef.current.delete(key);
      }
    });

    await Promise.allSettled(queuePositionPromises);
  };

  const fetch = async () => {
    const fetchId = latestFetchIdRef.current + 1;
    latestFetchIdRef.current = fetchId;

    try {
      const response = await transfersLibrary.getAll({ direction });

      await refreshQueuePositions(response);

      if (fetchId === latestFetchIdRef.current) {
        setTransfers(filterHiddenTransfers(response));
      }
    } catch (error) {
      console.error(error);
      toast.error(getErrorMessage(error));
    }
  };

  useEffect(() => {
    setConnecting(true);

    const init = async () => {
      await fetch();
      setConnecting(false);
    };

    init();
    const interval = window.setInterval(fetch, 1_000);

    return () => {
      clearInterval(interval);
    };
  }, [direction]); // eslint-disable-line react-hooks/exhaustive-deps

  useMemo(() => {
    setConnecting(true);
  }, [direction]); // eslint-disable-line react-hooks/exhaustive-deps

  const retry = async ({
    file,
    suppressErrorToast = false,
    suppressStateChange = false,
  }) => {
    const { filename, size, username } = file;

    try {
      if (!suppressStateChange) {
        setRetryingSingle(true);
      }

      await transfersLibrary.download({
        files: [{ filename, size }],
        username,
      });
    } catch (error) {
      console.error(error);
      if (!suppressErrorToast) {
        toast.error(getErrorMessage(error));
      }

      throw error;
    } finally {
      if (!suppressStateChange) {
        setRetryingSingle(false);
      }
    }
  };

  const retryAll = (transfersToRetry) => {
    enqueueBulkOperations({
      action: 'retry',
      operations: transfersToRetry.map((file) => ({
        key: `retry:${getTransferKey({ file })}`,
        label: `${file.username}/${file.filename}`,
        run: async () => {
          await retry({
            file,
            suppressErrorToast: true,
            suppressStateChange: true,
          });
          hideTransfers([file], (candidate) =>
            transfersLibrary.isStateRetryable(candidate.state),
          );
        },
      })),
    });
  };

  const cancel = async ({
    file,
    suppressErrorToast = false,
    suppressStateChange = false,
  }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) {
        setCancellingSingle(true);
      }

      await transfersLibrary.cancel({ direction, id, username });
    } catch (error) {
      console.error(error);
      if (!suppressErrorToast) {
        toast.error(getErrorMessage(error));
      }

      throw error;
    } finally {
      if (!suppressStateChange) {
        setCancellingSingle(false);
      }
    }
  };

  const cancelAll = (transfersToCancel) => {
    enqueueBulkOperations({
      action: 'cancel',
      operations: transfersToCancel.map((file) => ({
        key: `cancel:${getTransferKey({ file })}`,
        label: `${file.username}/${file.filename}`,
        run: () =>
          cancel({
            file,
            suppressErrorToast: true,
            suppressStateChange: true,
          }),
      })),
    });
  };

  const remove = async ({
    deleteFile = false,
    file,
    suppressErrorToast = false,
    suppressStateChange = false,
  }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) {
        setRemovingSingle(true);
      }

      await transfersLibrary.cancel({
        deleteFile,
        direction,
        id,
        remove: true,
        username,
      });
    } catch (error) {
      console.error(error);
      if (!suppressErrorToast) {
        toast.error(getErrorMessage(error));
      }

      throw error;
    } finally {
      if (!suppressStateChange) {
        setRemovingSingle(false);
      }
    }
  };

  const removeAll = (
    transfersToRemove,
    deleteFile = false,
    { useBulkClear = false } = {},
  ) => {
    if (useBulkClear && !deleteFile) {
      enqueueBulkOperations({
        action: 'remove',
        operations: [
          {
            key: `remove:clear-completed:${direction}`,
            label: `all completed ${direction}s`,
            run: async () => {
              await transfersLibrary.clearCompleted({ direction });
              hideTransfers(transfersToRemove);
            },
          },
        ],
      });
      return;
    }

    enqueueBulkOperations({
      action: 'remove',
      operations: transfersToRemove.map((file) => ({
        key: `remove:${getTransferKey({ file, suffix: deleteFile ? 'delete' : 'keep' })}`,
        label: `${file.username}/${file.filename}`,
        run: async () => {
          await remove({
            deleteFile,
            file,
            suppressErrorToast: true,
            suppressStateChange: true,
          });
          hideTransfers([file]);
        },
      })),
    });
  };

  useEffect(() => {
    const fetchDownloadModeStatus = async () => {
      if (direction !== 'download') {
        return;
      }

      try {
        const [autoReplaceStatus, acceleratedStatus] = await Promise.all([
          autoReplaceLibrary.getAutoReplaceStatus(),
          transfersLibrary.getAcceleratedMode(),
        ]);
        setAutoReplaceEnabled(autoReplaceStatus?.enabled ?? false);
        setAcceleratedEnabled(acceleratedStatus?.enabled ?? false);
      } catch (error) {
        console.error('Failed to fetch download mode status:', error);
      }
    };

    fetchDownloadModeStatus();
  }, [direction]);

  const handleAutoReplaceChange = async (enabled) => {
    try {
      if (enabled) {
        await autoReplaceLibrary.enableAutoReplace();
        setAutoReplaceEnabled(true);
        toast.info(
          'Auto-replace enabled. Backend will check for stuck downloads periodically.',
        );
      } else {
        await autoReplaceLibrary.disableAutoReplace();
        setAutoReplaceEnabled(false);
        toast.info('Auto-replace disabled');
      }
    } catch (error) {
      console.error('Failed to toggle auto-replace:', error);
      toast.error('Failed to toggle auto-replace');
    }
  };

  const handleAcceleratedChange = async (enabled) => {
    try {
      const status = await transfersLibrary.setAcceleratedMode({ enabled });
      setAcceleratedEnabled(status?.enabled ?? enabled);
      toast.info(
        enabled
          ? 'Accelerated mode enabled. Slow or stalled downloads can use verified alternate sources.'
          : 'Accelerated mode disabled',
      );
    } catch (error) {
      console.error('Failed to toggle accelerated mode:', error);
      toast.error('Failed to toggle accelerated mode');
    }
  };

  if (connecting) {
    return <LoaderSegment />;
  }

  return (
    <div data-testid={testId}>
      <TransfersHeader
        acceleratedEnabled={acceleratedEnabled}
        autoReplaceEnabled={autoReplaceEnabled}
        autoReplaceThreshold={autoReplaceThreshold}
        cancelling={cancelling}
        direction={direction}
        onAutoReplaceChange={handleAutoReplaceChange}
        onAcceleratedChange={handleAcceleratedChange}
        onCancelAll={cancelAll}
        onRemoveAll={removeAll}
        onRetryAll={retryAll}
        removing={removing}
        retrying={retrying}
        server={server}
        transfers={transfers}
      />
      {transfers.length === 0 ? (
        <PlaceholderSegment
          caption={`No ${direction}s to display`}
          icon={direction}
        />
      ) : (
        transfers.map((user) => (
          <TransferGroup
            cancel={cancel}
            cancelAll={cancelAll}
            direction={direction}
            key={user.username}
            remove={remove}
            removeAll={removeAll}
            retry={retry}
            retryAll={retryAll}
            user={user}
          />
        ))
      )}
    </div>
  );
};

export default Transfers;
