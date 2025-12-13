import './Transfers.css';
import * as autoReplaceLibrary from '../../lib/autoReplace';
import * as transfersLibrary from '../../lib/transfers';
import { LoaderSegment, PlaceholderSegment } from '../Shared';
import TransferGroup from './TransferGroup';
import TransfersTicker from './TransfersTicker';
import TransfersHeader from './TransfersHeader';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { toast } from 'react-toastify';

const AUTO_REPLACE_THRESHOLD = 0; // 0% = exact match only (configurable on backend)

const Transfers = ({ direction, server }) => {
  const [connecting, setConnecting] = useState(true);
  const [transfers, setTransfers] = useState([]);
  const [queuePositions, setQueuePositions] = useState({});

  const [retrying, setRetrying] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [removing, setRemoving] = useState(false);

  const [autoReplaceEnabled, setAutoReplaceEnabled] = useState(false);
  const autoReplaceThreshold = AUTO_REPLACE_THRESHOLD;

  const fetch = async () => {
    try {
      const response = await transfersLibrary.getAll({ direction });
      setTransfers(response);
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
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

  const transfersRef = useRef([]);
  useEffect(() => {
    transfersRef.current = transfers;
  }, [transfers]);

  useMemo(() => {
    // this is used to prevent weird update issues if switching
    // between uploads and downloads.  useEffect fires _after_ the
    // prop 'direction' updates, meaning there's a flash where the
    // screen contents switch to the new direction for a brief moment
    // before the connecting animation shows.  this memo fires the instant
    // the direction prop changes, preventing this flash.
    setConnecting(true);
  }, [direction]); // eslint-disable-line react-hooks/exhaustive-deps

  const retry = async ({ file, suppressStateChange = false }) => {
    const { filename, size, username } = file;

    try {
      if (!suppressStateChange) {
        setRetrying(true);
      }

      await transfersLibrary.download({
        files: [{ filename, size }],
        username,
      });
      if (!suppressStateChange) {
        setRetrying(false);
      }
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) {
        setRetrying(false);
      }
    }
  };

  const retryAll = async (transfersToRetry) => {
    setRetrying(true);
    await Promise.all(
      transfersToRetry.map((file) =>
        retry({ file, suppressStateChange: true }),
      ),
    );
    setRetrying(false);
  };

  const cancel = async ({ file, suppressStateChange = false }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) {
        setCancelling(true);
      }

      await transfersLibrary.cancel({ direction, id, username });
      if (!suppressStateChange) {
        setCancelling(false);
      }
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) {
        setCancelling(false);
      }
    }
  };

  const cancelAll = async (transfersToCancel) => {
    setCancelling(true);
    await Promise.all(
      transfersToCancel.map((file) =>
        cancel({ file, suppressStateChange: true }),
      ),
    );
    setCancelling(false);
  };

  const remove = async ({
    deleteFile = false,
    file,
    suppressStateChange = false,
  }) => {
    const { id, username } = file;

    try {
      if (!suppressStateChange) {
        setRemoving(true);
      }

      await transfersLibrary.cancel({
        deleteFile,
        direction,
        id,
        remove: true,
        username,
      });
      if (!suppressStateChange) {
        setRemoving(false);
      }
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
      if (!suppressStateChange) {
        setRemoving(false);
      }
    }
  };

  const removeAll = async (transfersToRemove, deleteFile = false) => {
    setRemoving(true);
    await Promise.all(
      transfersToRemove.map((file) =>
        remove({ deleteFile, file, suppressStateChange: true }),
      ),
    );
    setRemoving(false);
  };

  // Fetch auto-replace status from backend on mount
  useEffect(() => {
    const fetchAutoReplaceStatus = async () => {
      if (direction !== 'download') {
        return;
      }

      try {
        const status = await autoReplaceLibrary.getAutoReplaceStatus();
        setAutoReplaceEnabled(status?.enabled ?? false);
      } catch (error) {
        console.error('Failed to fetch auto-replace status:', error);
      }
    };

    fetchAutoReplaceStatus();
  }, [direction]);

  // Handle auto-replace toggle via backend API
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

  const refreshQueuePositions = async (currentTransfers) => {
    if (direction !== 'download') {
      setQueuePositions({});
      return;
    }

    const queued = [];
    currentTransfers.forEach((user) =>
      (user.directories || []).forEach((directory) =>
        (directory.files || []).forEach((file) => {
          if (
            file.id !== undefined &&
            file.state &&
            file.state.includes('Queued')
          ) {
            queued.push({ id: file.id, username: user.username });
          }
        }),
      ),
    );

    if (queued.length === 0) {
      setQueuePositions({});
      return;
    }

    const results = await Promise.all(
      queued.map(async (item) => {
        try {
          const response = await transfersLibrary.getPlaceInQueue(item);
          return {
            id: item.id,
            position: response?.data ?? response,
          };
        } catch (error) {
          console.error(error);
          return { id: item.id, position: undefined };
        }
      }),
    );

    const nextPositions = {};
    results.forEach((result) => {
      if (typeof result.position === 'number') {
        nextPositions[result.id] = result.position;
      }
    });
    setQueuePositions(nextPositions);
  };

  useEffect(() => {
    if (direction !== 'download') {
      return undefined;
    }

    const poll = async () => {
      await refreshQueuePositions(transfersRef.current);
    };

    poll();
    const interval = window.setInterval(poll, 5_000);
    return () => clearInterval(interval);
  }, [direction]); // eslint-disable-line react-hooks/exhaustive-deps

  const transfersWithQueue = useMemo(
    () =>
      transfers.map((user) => ({
        ...user,
        directories: (user.directories || []).map((directory) => ({
          ...directory,
          files: (directory.files || []).map((file) => ({
            ...file,
            placeInQueue:
              queuePositions[file.id] !== undefined
                ? queuePositions[file.id]
                : file.placeInQueue,
          })),
        })),
      })),
    [queuePositions, transfers],
  );

  if (connecting) {
    return <LoaderSegment />;
  }

  return (
    <>
      <TransfersHeader
        autoReplaceEnabled={autoReplaceEnabled}
        autoReplaceThreshold={autoReplaceThreshold}
        cancelling={cancelling}
        direction={direction}
        onAutoReplaceChange={handleAutoReplaceChange}
        onCancelAll={cancelAll}
        onRemoveAll={removeAll}
        onRetryAll={retryAll}
        removing={removing}
        retrying={retrying}
        server={server}
        transfers={transfersWithQueue}
      />
      <TransfersTicker
        direction={direction}
        transfers={transfersWithQueue}
      />
      {transfersWithQueue.length === 0 ? (
        <PlaceholderSegment
          caption={`No ${direction}s to display`}
          icon={direction}
        />
      ) : (
        transfersWithQueue.map((user) => (
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
    </>
  );
};

export default Transfers;
