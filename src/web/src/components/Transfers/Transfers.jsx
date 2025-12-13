import './Transfers.css';
import * as autoReplaceLibrary from '../../lib/autoReplace';
import * as transfersLibrary from '../../lib/transfers';
import { LoaderSegment, PlaceholderSegment } from '../Shared';
import TransferGroup from './TransferGroup';
import TransfersHeader from './TransfersHeader';
import React, { useEffect, useMemo, useState } from 'react';
import { toast } from 'react-toastify';

const AUTO_REPLACE_THRESHOLD = 0; // 0% = exact match only (configurable on backend)

const Transfers = ({ direction, server }) => {
  const [connecting, setConnecting] = useState(true);
  const [transfers, setTransfers] = useState([]);

  const [retrying, setRetrying] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [removing, setRemoving] = useState(false);

  const [autoReplaceEnabled, setAutoReplaceEnabled] = useState(false);
  const autoReplaceThreshold = AUTO_REPLACE_THRESHOLD;

  const fetch = async () => {
    try {
      const response = await transfersLibrary.getAll({ direction });
      setTransfers(response);

      // Automatically fetch queue positions for queued downloads
      if (direction === 'download') {
        const queuedDownloads = response
          .flatMap(user => user.directories.flatMap(dir => dir.files))
          .filter(file => file.state && file.state.includes('Queued'));

        // Update queue positions in parallel
        const queuePositionPromises = queuedDownloads.map(async (file) => {
          try {
            const queueResponse = await transfersLibrary.getPlaceInQueue({
              id: file.id,
              username: file.username
            });

            // Find and update the transfer in the response data
            response.forEach(user => {
              user.directories.forEach(dir => {
                const transfer = dir.files.find(f => f.id === file.id && f.username === file.username);
                if (transfer) {
                  transfer.placeInQueue = queueResponse.data;
                }
              });
            });
          } catch (error) {
            // Silently fail individual queue position fetches to avoid spam
            console.debug('Failed to fetch queue position for', file.filename, error);
          }
        });

        await Promise.allSettled(queuePositionPromises);
        // Update state with the fresh queue positions
        setTransfers([...response]);
      }
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
    </>
  );
};

export default Transfers;
