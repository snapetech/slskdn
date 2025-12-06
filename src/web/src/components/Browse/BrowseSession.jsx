/* eslint-disable promise/prefer-await-to-then */
import './Browse.css';
import * as transfers from '../../lib/transfers';
import * as userNotes from '../../lib/userNotes';
import * as users from '../../lib/users';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import UserNoteModal from '../Users/UserNoteModal';
import Directory from './Directory';
import DirectoryTree from './DirectoryTree';
import * as lzString from 'lz-string';
import React, { Component } from 'react';
import { toast } from 'react-toastify';
import { Card, Icon, Input, Loader, Segment } from 'semantic-ui-react';

const initialState = {
  browseError: undefined,
  browseState: 'idle',
  browseStatus: 0,
  info: {
    directories: 0,
    files: 0,
    lockedDirectories: 0,
    lockedFiles: 0,
  },
  selectedDirectory: {},
  selectedFiles: [],
  separator: '\\',
  tree: [],
  username: '',
  userNote: null,
};

class BrowseSession extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
    this.pollInterval = null;
  }

  componentDidMount() {
    // Check for username from props (tab only - navigation handled by parent)
    const userToBrowse = this.props.username;

    if (userToBrowse) {
      this.fetchUserNote(userToBrowse);
      // Try to load cached data first
      const hasCachedData = this.loadState();

      // Small delay to ensure ref is ready
      setTimeout(() => {
        if (this.inputtext?.inputRef?.current) {
          this.inputtext.inputRef.current.value = userToBrowse;
        }

        // Only fetch if we don't have cached data
        if (!hasCachedData) {
          this.setState({ username: userToBrowse }, this.browse);
        }
      }, 50);
    } else {
      this.loadState();
    }

    document.addEventListener('keyup', this.keyUp, false);
    document.addEventListener('visibilitychange', this.handleVisibilityChange);
  }

  componentWillUnmount() {
    this.stopPolling();
    document.removeEventListener('keyup', this.keyUp, false);
    document.removeEventListener(
      'visibilitychange',
      this.handleVisibilityChange,
    );
  }

  fetchUserNote = async (username) => {
    try {
      const response = await userNotes.getNote({ username });
      this.setState({ userNote: response.data });
    } catch {
      this.setState({ userNote: null });
    }
  };

  // Start polling only when needed (during active browse)
  startPolling = () => {
    if (!this.pollInterval) {
      this.pollInterval = window.setInterval(this.fetchStatus, 500);
    }
  };

  // Stop polling when not needed
  stopPolling = () => {
    if (this.pollInterval) {
      clearInterval(this.pollInterval);
      this.pollInterval = null;
    }
  };

  // Pause polling when page is hidden to save resources
  handleVisibilityChange = () => {
    if (document.hidden) {
      this.stopPolling();
    } else if (this.state.browseState === 'pending') {
      this.startPolling();
    }
  };

  browse = () => {
    const username = this.inputtext.inputRef.current.value;

    if (!username) {
      return;
    }

    // Notify parent to update tab label
    if (this.props.onUsernameChange) {
      this.props.onUsernameChange(username);
    }

    this.setState(
      { browseError: undefined, browseState: 'pending', username },
      () => {
        this.fetchUserNote(username);
        // Start polling only while browse is in progress
        this.startPolling();

        users
          .browse({ username })
          .then((response) => {
            let { directories } = response;
            const { lockedDirectories } = response;

            // we need to know the directory separator. assume it is \ to start
            let separator;

            const directoryCount = directories.length;
            const fileCount = directories.reduce((accumulator, directory) => {
              // examine each directory as we process it to see if it contains \ or /, and set separator accordingly
              if (!separator) {
                if (directory.name.includes('\\')) separator = '\\';
                else if (directory.name.includes('/')) separator = '/';
              }

              return accumulator + directory.fileCount;
            }, 0);

            const lockedDirectoryCount = lockedDirectories.length;
            const lockedFileCount = lockedDirectories.reduce(
              (accumulator, directory) => accumulator + directory.fileCount,
              0,
            );

            directories = directories.concat(
              lockedDirectories.map((d) => ({ ...d, locked: true })),
            );

            this.setState({
              info: {
                directories: directoryCount,
                files: fileCount,
                lockedDirectories: lockedDirectoryCount,
                lockedFiles: lockedFileCount,
              },
              separator,
              tree: this.getDirectoryTree({ directories, separator }),
            });
          })
          .then(() => {
            // Stop polling when browse completes
            this.stopPolling();
            this.setState(
              { browseError: undefined, browseState: 'complete' },
              () => {
                this.saveState();
              },
            );
          })
          .catch((error) => {
            // Stop polling on error too
            this.stopPolling();
            this.setState({ browseError: error, browseState: 'error' });
          });
      },
    );
  };

  clear = () => {
    this.stopPolling();
    this.setState(initialState, () => {
      this.saveState();
      this.inputtext.focus();
    });
  };

  keyUp = (event) => (event.key === 'Escape' ? this.clear() : '');

  getStorageKey = () => {
    const username = this.props.username || this.state.username || 'default';
    return `slskd-browse-state-${username}`;
  };

  saveState = () => {
    if (this.inputtext?.inputRef?.current) {
      this.inputtext.inputRef.current.value = this.state.username;
      this.inputtext.inputRef.current.disabled =
        this.state.browseState !== 'idle';
    }

    // Only save if we have actual browse data
    if (this.state.username && this.state.tree.length > 0) {
      try {
        localStorage.setItem(
          this.getStorageKey(),
          lzString.compress(JSON.stringify(this.state)),
        );
      } catch (error) {
        console.error(error);
      }
    }
  };

  loadState = () => {
    // Try to load saved state for this username
    const username = this.props.username;

    if (username) {
      try {
        const key = `slskd-browse-state-${username}`;
        const savedState = JSON.parse(
          lzString.decompress(localStorage.getItem(key) || ''),
        );

        if (savedState && savedState.tree && savedState.tree.length > 0) {
          // We have cached data - use it instead of re-fetching
          this.setState({
            ...savedState,
            browseState: 'complete',
          });
          return true; // Indicate we loaded cached data
        }
      } catch {
        // ignore - will fetch fresh
      }
    }

    return false;
  };

  fetchStatus = () => {
    const { browseState, username } = this.state;
    // Only poll status when actively browsing AND we have a username
    if (browseState === 'pending' && username) {
      users
        .getBrowseStatus({ username })
        .then((response) =>
          this.setState({
            browseStatus: response.data,
          }),
        )
        .catch(() => {
          // Ignore 404s during status polling
        });
    }
  };

  getDirectoryTree = ({ directories, separator }) => {
    if (directories.length === 0 || directories[0].name === undefined) {
      return [];
    }

    // Optimise this process so we only:
    // - loop through all directories once
    // - do the split once
    // - future look ups are done from the Map
    const depthMap = new Map();
    for (const d of directories) {
      const directoryDepth = d.name.split(separator).length;
      if (!depthMap.has(directoryDepth)) {
        depthMap.set(directoryDepth, []);
      }

      depthMap.get(directoryDepth).push(d);
    }

    const depth = Math.min(...Array.from(depthMap.keys()));

    return depthMap
      .get(depth)
      .map((directory) =>
        this.getChildDirectories(depthMap, directory, separator, depth + 1),
      );
  };

  getChildDirectories = (depthMap, root, separator, depth) => {
    if (!depthMap.has(depth)) {
      return { ...root, children: [] };
    }

    const children = depthMap
      .get(depth)
      .filter((d) => d.name.startsWith(root.name));

    return {
      ...root,
      children: children.map((c) =>
        this.getChildDirectories(depthMap, c, separator, depth + 1),
      ),
    };
  };

  selectDirectory = (directory) => {
    this.setState({ selectedDirectory: { ...directory, children: [] } }, () =>
      this.saveState(),
    );
  };

  handleDeselectDirectory = () => {
    this.setState({ selectedDirectory: initialState.selectedDirectory }, () =>
      this.saveState(),
    );
  };

  handleRefresh = () => {
    // Force re-fetch by clearing cache and browsing again
    const { username } = this.state;

    if (username) {
      // Clear the cached state for this user
      try {
        localStorage.removeItem(`slskd-browse-state-${username}`);
      } catch {
        // ignore
      }

      // Re-browse
      this.browse();
    }
  };

  handleDownloadDirectory = (directory) => {
    const { separator, username } = this.state;

    // Collect all files recursively
    const collectFiles = (folder) => {
      let collected = (folder.files || []).map((f) => ({
        filename: `${folder.name}${separator}${f.filename}`,
        size: f.size,
      }));

      if (folder.children) {
        for (const child of folder.children) {
          collected = collected.concat(collectFiles(child));
        }
      }

      return collected;
    };

    const filesToDownload = collectFiles(directory);

    if (filesToDownload.length === 0) {
      toast.info(`No files found in directory: ${directory.name}`);
      return;
    }

    if (
      // eslint-disable-next-line no-alert
      window.confirm(
        `Download ${filesToDownload.length} files from ${directory.name}?`,
      )
    ) {
      transfers
        .download({ files: filesToDownload, username })
        .then(() => {
          toast.success(`Queued ${filesToDownload.length} files for download`);
        })
        .catch((error) => {
          console.error(error);
          toast.error(`Failed to queue download: ${error?.message || error}`);
        });
    }
  };

  render() {
    const {
      browseError,
      browseState,
      browseStatus,
      info,
      selectedDirectory,
      separator,
      tree,
      userNote,
      username,
    } = this.state;
    const { locked, name } = selectedDirectory;
    const pending = browseState === 'pending';
    const finished = ['complete', 'error'].includes(browseState);
    const emptyTree = finished && tree.length === 0;

    const files = (selectedDirectory.files || []).map((f) => ({
      ...f,
      filename: `${name}${separator}${f.filename}`,
    }));

    return (
      <div className="search-container">
        <Segment
          className="browse-segment"
          raised
        >
          <div className="browse-segment-icon">
            <Icon
              name="folder open"
              size="big"
            />
          </div>
          <Input
            action={
              !pending &&
              (browseState === 'idle'
                ? { icon: 'search', onClick: this.browse }
                : { color: 'red', icon: 'x', onClick: this.clear })
            }
            className="search-input"
            disabled={pending}
            input={
              <input
                data-lpignore="true"
                placeholder="Username"
                type="search"
              />
            }
            loading={pending}
            onKeyUp={(event) => (event.key === 'Enter' ? this.browse() : '')}
            placeholder="Username"
            ref={(input) => (this.inputtext = input)}
            size="big"
          />
        </Segment>
        {pending ? (
          <Loader
            active
            className="search-loader"
            inline="centered"
            size="big"
          >
            Downloaded {Math.round(browseStatus.percentComplete || 0)}% of
            Response
          </Loader>
        ) : (
          <div>
            {browseError ? (
              <span className="browse-error">Failed to browse {username}</span>
            ) : (
              <div className="browse-container">
                {emptyTree ? (
                  <PlaceholderSegment
                    caption="No user share to display"
                    icon="folder open"
                  />
                ) : (
                  <Card
                    className="browse-tree-card"
                    raised
                  >
                    <Card.Content>
                      <Card.Header
                        style={{
                          alignItems: 'center',
                          display: 'flex',
                          justifyContent: 'space-between',
                        }}
                      >
                        <span>
                          <Icon
                            color="green"
                            name="circle"
                          />
                          {username}
                          {userNote && (
                            <Icon
                              color={userNote.color || 'grey'}
                              name={userNote.icon || 'sticky note'}
                              style={{ marginLeft: '8px' }}
                              title={userNote.note}
                            />
                          )}
                          <UserNoteModal
                            onClose={() => this.fetchUserNote(username)}
                            trigger={
                              <Icon
                                color="grey"
                                link
                                name="pencil alternate"
                                size="small"
                                style={{ marginLeft: '4px', opacity: 0.5 }}
                              />
                            }
                            username={username}
                          />
                        </span>
                        <Icon
                          link
                          name="refresh"
                          onClick={this.handleRefresh}
                          title="Refresh user's file list"
                        />
                      </Card.Header>
                      <Card.Meta className="browse-meta">
                        {`${info.directories} directories, ${info.files} files`}
                        {info.lockedDirectories
                          ? ` (${info.lockedDirectories} locked directories, ${info.lockedFiles} locked files)`
                          : ''}
                      </Card.Meta>
                    </Card.Content>
                    <Card.Content>
                      <Segment className="browse-folderlist">
                        <DirectoryTree
                          onDownload={this.handleDownloadDirectory}
                          onSelect={(_, value) => this.selectDirectory(value)}
                          selectedDirectoryName={name}
                          tree={tree}
                        />
                      </Segment>
                    </Card.Content>
                  </Card>
                )}
                {name && (
                  <Directory
                    files={files}
                    locked={locked}
                    marginTop={-20}
                    name={name}
                    onClose={this.handleDeselectDirectory}
                    username={username}
                  />
                )}
              </div>
            )}
          </div>
        )}
      </div>
    );
  }
}

export default BrowseSession;
