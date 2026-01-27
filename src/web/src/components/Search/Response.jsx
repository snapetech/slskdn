import api from '../../lib/api';
import * as transfers from '../../lib/transfers';
import { getDirectoryContents, getGroup } from '../../lib/users';
import { formatBytes, getDirectoryName } from '../../lib/util';
import FileList from '../Shared/FileList';
import UserCard from '../Shared/UserCard';
import UserNoteModal from '../Users/UserNoteModal';
import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Card, Icon, Label, Popup } from 'semantic-ui-react';

const buildTree = (response) => {
  let { files = [] } = response;
  const { lockedFiles = [] } = response;
  files = files.concat(lockedFiles.map((file) => ({ ...file, locked: true })));

  return files.reduce((dict, file) => {
    const directory = getDirectoryName(file.filename);
    const selectable = { selected: false, ...file };
    dict[directory] =
      dict[directory] === undefined
        ? [selectable]
        : dict[directory].concat(selectable);
    return dict;
  }, {});
};

const getBadgeColor = (downloadStats) => {
  if (!downloadStats) return null;
  if (downloadStats.successfulDownloads >= 5) return 'green';
  if (downloadStats.successfulDownloads >= 1) return 'blue';
  if (downloadStats.failedDownloads > downloadStats.successfulDownloads)
    return 'orange';
  return 'grey';
};

const getGroupIndicator = (group) => {
  switch (group) {
    case 'privileged':
      return { color: 'yellow', icon: 'star', tooltip: 'Privileged User' };
    case 'leechers':
      return {
        color: 'orange',
        icon: 'exclamation triangle',
        tooltip: 'Leecher (Low shares)',
      };
    case 'blacklisted':
      return { color: 'red', icon: 'ban', tooltip: 'Blacklisted User' };
    default:
      // For user-defined groups or default group, show a generic user icon
      return group && group !== 'default'
        ? { color: 'blue', icon: 'user', tooltip: `Group: ${group}` }
        : null;
  }
};

const getSelectedFiles = (tree) => {
  return Object.keys(tree)
    .reduce((list, dict) => list.concat(tree[dict]), [])
    .filter((f) => f.selected);
};

const getSelectedSize = (selectedFiles) => {
  return formatBytes(selectedFiles.reduce((total, f) => total + f.size, 0));
};

class Response extends Component {
  constructor(props) {
    super(props);

    this.state = {
      downloadError: '',
      downloadRequest: undefined,
      fetchingDirectoryContents: false,
      isFolded: this.props.isInitiallyFolded,
      tree: buildTree(this.props.response),
      userGroup: null,
      userGroupLoading: false,
    };
  }

  componentDidMount() {
    this.fetchUserGroup();
  }

  componentDidUpdate(previousProps) {
    if (
      JSON.stringify(this.props.response) !==
      JSON.stringify(previousProps.response)
    ) {
      this.setState({ tree: buildTree(this.props.response) });
    }

    if (this.props.isInitiallyFolded !== previousProps.isInitiallyFolded) {
      this.setState({ isFolded: this.props.isInitiallyFolded });
    }

    if (this.props.response.username !== previousProps.response.username) {
      this.fetchUserGroup();
    }
  }

  fetchUserGroup = async () => {
    const { username } = this.props.response;

    this.setState({ userGroupLoading: true });

    try {
      const response = await getGroup({ username });
      this.setState({ userGroup: response.data, userGroupLoading: false });
    } catch (error) {
      console.debug('Failed to fetch user group for', username, error);
      this.setState({ userGroup: null, userGroupLoading: false });
    }
  };

  handleFileSelectionChange = (file, state) => {
    file.selected = state;
    this.setState((previousState) => ({
      downloadError: '',
      downloadRequest: undefined,
      tree: previousState.tree,
    }));
  };

  download = (username, files) => {
    this.setState({ downloadRequest: 'inProgress' }, async () => {
      try {
        const { response, responseIndex, searchId } = this.props;

        // Check if this is a bridged search result (has provenance) and we have searchId
        if (
          response.sourceProviders &&
          response.sourceProviders.length > 0 &&
          searchId
        ) {
          // Use new action routing endpoint for bridged searches
          // Download all selected files
          const downloadPromises = files.map(async (file) => {
            const fileIndex =
              response.files?.findIndex((f) => f.filename === file.filename) ??
              response.lockedFiles?.findIndex(
                (f) => f.filename === file.filename,
              ) ??
              -1;
            if (fileIndex < 0) {
              throw new Error(`File ${file.filename} not found in response`);
            }

            const itemId = `${responseIndex ?? 0}:${fileIndex}`;
            return api.post(`/searches/${searchId}/items/${itemId}/download`);
          });

          await Promise.all(downloadPromises);
        } else {
          // Use existing download method for non-bridged searches
          const requests = (files || []).map(({ filename, size }) => ({
            filename,
            size,
          }));
          await transfers.download({ files: requests, username });
        }

        this.setState({ downloadRequest: 'complete' });
      } catch (error) {
        this.setState({
          downloadError: error.response || {
            data: error.message,
            status: 500,
            statusText: 'Error',
          },
          downloadRequest: 'error',
        });
      }
    });
  };

  getFullDirectory = async (username, directory) => {
    this.setState({ fetchingDirectoryContents: true });

    try {
      const oldTree = { ...this.state.tree };
      const oldFiles = oldTree[directory];

      try {
        // some clients might send more than one directory in the response,
        // if the requested directory contains subdirectories. the root directory
        // is always first, and for now we'll only display the contents of that.
        const allDirectories = await getDirectoryContents({
          directory,
          username,
        });
        const theRootDirectory = allDirectories?.[0];

        // some clients might send an empty response for some reason
        if (!theRootDirectory) {
          throw new Error('No directories were included in the response');
        }

        const { files, name } = theRootDirectory;

        // the api returns file names only, so we need to prepend the directory
        // to make it look like a search result.  we also need to preserve
        // any file selections, so check the old files and assign accordingly
        const fixedFiles = files.map((file) => ({
          ...file,
          filename: `${directory}\\${file.filename}`,
          selected:
            oldFiles.find(
              (f) => f.filename === `${directory}\\${file.filename}`,
            )?.selected ?? false,
        }));

        oldTree[name] = fixedFiles;
        this.setState({ tree: { ...oldTree } });
      } catch (error) {
        throw new Error(`Failed to process directory response: ${error}`, {
          cause: error,
        });
      }
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? error);
    } finally {
      this.setState({ fetchingDirectoryContents: false });
    }
  };

  handleToggleFolded = () => {
    this.setState((previousState) => ({ isFolded: !previousState.isFolded }));
  };

  renderDownloadAction = (
    selectedFiles,
    selectedSize,
    downloadRequest,
    downloadError,
  ) => {
    const noSelection = selectedFiles.length === 0;
    const { response } = this.props;
    const hasPodSource = response.sourceProviders?.includes('pod');
    const primarySource = response.primarySource || 'scene';

    return (
      <Card.Content extra>
        <span>
          <Button
            basic={noSelection}
            color={noSelection ? 'grey' : 'green'}
            content="Download"
            disabled={
              noSelection ||
              this.props.disabled ||
              downloadRequest === 'inProgress'
            }
            icon="download"
            label={
              noSelection
                ? undefined
                : {
                    as: 'a',
                    basic: false,
                    content: `${selectedFiles.length} file${
                      selectedFiles.length === 1 ? '' : 's'
                    }, ${selectedSize}`,
                  }
            }
            labelPosition={noSelection ? undefined : 'right'}
            onClick={() =>
              this.download(this.props.response.username, selectedFiles)
            }
          />
          {hasPodSource && selectedFiles.length > 0 && (
            <Popup
              content={`Stream from Pod (${primarySource === 'pod' ? 'preferred' : 'available'})`}
              position="top center"
              trigger={
                <Button
                  basic
                  color="blue"
                  content="Stream"
                  disabled={
                    noSelection ||
                    this.props.disabled ||
                    downloadRequest === 'inProgress'
                  }
                  icon="play"
                  onClick={async () => {
                    // Stream first selected file
                    const firstFile = selectedFiles[0];
                    const {
                      response: responseProperty,
                      responseIndex,
                      searchId,
                    } = this.props;
                    if (!firstFile || !searchId) return;

                    try {
                      const fileIndex =
                        responseProperty.files?.findIndex(
                          (f) => f.filename === firstFile.filename,
                        ) ?? -1;
                      if (fileIndex < 0) return;

                      const itemId = `${responseIndex ?? 0}:${fileIndex}`;
                      const result = await api.post(
                        `/searches/${searchId}/items/${itemId}/stream`,
                      );

                      if (result.data?.stream_url) {
                        // Open stream URL in new tab or redirect
                        window.open(result.data.stream_url, '_blank');
                      }
                    } catch (error) {
                      toast.error(
                        error?.response?.data?.detail ||
                          error?.message ||
                          'Stream failed',
                      );
                    }
                  }}
                />
              }
            />
          )}
          {downloadRequest === 'inProgress' && (
            <Icon
              loading
              name="circle notch"
              size="large"
            />
          )}
          {downloadRequest === 'complete' && (
            <Icon
              color="green"
              name="checkmark"
              size="large"
            />
          )}
          {downloadRequest === 'error' && (
            <span>
              <Icon
                color="red"
                name="x"
                size="large"
              />
              <Label>
                {downloadError?.data ||
                  downloadError?.message ||
                  'Download failed'}{' '}
                {downloadError?.status &&
                  `(HTTP ${downloadError.status} ${downloadError.statusText || ''})`}
              </Label>
            </span>
          )}
        </span>
      </Card.Content>
    );
  };

  render() {
    const {
      downloadStats,
      isBlocked,
      onBlock,
      onNoteUpdate,
      onUnblock,
      response,
      smartScore,
      userNote,
    } = this.props;
    const free = response.hasFreeUploadSlot;

    const {
      downloadError,
      downloadRequest,
      fetchingDirectoryContents,
      isFolded,
      tree,
      userGroup,
      userGroupLoading,
    } = this.state;

    const selectedFiles = getSelectedFiles(tree);
    const selectedSize = getSelectedSize(selectedFiles);
    const badgeColor = getBadgeColor(downloadStats);

    return (
      <Card
        className="result-card"
        raised
      >
        <Card.Content>
          <Card.Header>
            <Icon
              link
              name={isFolded ? 'chevron right' : 'chevron down'}
              onClick={this.handleToggleFolded}
            />
            <Icon
              color={free ? 'green' : 'yellow'}
              name="circle"
            />
            <Link
              style={{ color: 'inherit' }}
              title="Browse files"
              to={{
                pathname: '/browse',
                state: { user: response.username },
              }}
            >
              <UserCard username={response.username}>
                {response.username}
              </UserCard>
            </Link>
            {!userGroupLoading &&
              userGroup &&
              (() => {
                const indicator = getGroupIndicator(userGroup);
                return indicator ? (
                  <Popup
                    content={indicator.tooltip}
                    position="top center"
                    trigger={
                      <Icon
                        color={indicator.color}
                        name={indicator.icon}
                        size="small"
                        style={{ marginLeft: '4px' }}
                      />
                    }
                  />
                ) : null;
              })()}
            {downloadStats && (
              <Popup
                content={`${downloadStats.successfulDownloads} successful, ${downloadStats.failedDownloads} failed downloads from this user`}
                position="top center"
                trigger={
                  <Label
                    color={badgeColor}
                    size="tiny"
                    style={{ marginLeft: '8px' }}
                  >
                    <Icon name="download" />
                    {downloadStats.successfulDownloads}
                  </Label>
                }
              />
            )}
            {smartScore !== undefined && (
              <Popup
                content={`Smart ranking score: ${smartScore.toFixed(1)} points`}
                position="top center"
                trigger={
                  <Label
                    color="purple"
                    size="tiny"
                    style={{ marginLeft: '4px' }}
                  >
                    <Icon name="star" />
                    {smartScore.toFixed(0)}
                  </Label>
                }
              />
            )}
            {userNote && (
              <Popup
                content={userNote.note || 'User Note'}
                position="top center"
                trigger={
                  <Label
                    circular
                    color={userNote.color || 'grey'}
                    empty={!userNote.icon}
                    icon={userNote.icon}
                    size="tiny"
                    style={{ marginLeft: '4px' }}
                  />
                }
              />
            )}
            {/* Scene ↔ Pod Bridging provenance badges */}
            {response.sourceProviders &&
              response.sourceProviders.length > 0 && (
                <>
                  {response.sourceProviders.includes('pod') && (
                    <Popup
                      content="Available from Pod/Mesh network"
                      position="top center"
                      trigger={
                        <Label
                          color="blue"
                          size="tiny"
                          style={{ marginLeft: '4px' }}
                        >
                          POD
                        </Label>
                      }
                    />
                  )}
                  {response.sourceProviders.includes('scene') && (
                    <Popup
                      content="Available from Soulseek Scene"
                      position="top center"
                      trigger={
                        <Label
                          color="purple"
                          size="tiny"
                          style={{ marginLeft: '4px' }}
                        >
                          SCENE
                        </Label>
                      }
                    />
                  )}
                  {response.sourceProviders.length > 1 && (
                    <Popup
                      content={`Available from both Pod and Scene. Preferred: ${response.primarySource?.toUpperCase() || 'POD'}`}
                      position="top center"
                      trigger={
                        <Label
                          color="teal"
                          size="tiny"
                          style={{ marginLeft: '4px' }}
                        >
                          POD+SCENE
                        </Label>
                      }
                    />
                  )}
                </>
              )}
            <UserNoteModal
              onClose={onNoteUpdate}
              trigger={
                <Icon
                  color="grey"
                  link
                  name="pencil alternate"
                  size="small"
                  style={{ marginLeft: '4px', opacity: 0.5 }}
                  title="Edit User Note"
                />
              }
              username={response.username}
            />
            <span style={{ float: 'right' }}>
              <Popup
                content={
                  isBlocked
                    ? 'Unblock this user'
                    : 'Block this user from search results'
                }
                position="top center"
                trigger={
                  <Icon
                    color={isBlocked ? 'orange' : 'grey'}
                    link
                    name={isBlocked ? 'ban' : 'user cancel'}
                    onClick={isBlocked ? onUnblock : onBlock}
                    style={{ marginRight: '8px' }}
                  />
                }
              />
              <Icon
                className="close-button"
                color="red"
                link
                name="close"
                onClick={() => this.props.onHide()}
              />
            </span>
          </Card.Header>
          <Card.Meta className="result-meta">
            <span>
              Upload Speed: {formatBytes(response.uploadSpeed)}/s, Free Upload
              Slot: {free ? 'YES' : 'NO'}, Queue Length: {response.queueLength}
            </span>
          </Card.Meta>
          {((!isFolded && Object.keys(tree)) || []).map((directory) => (
            <FileList
              directoryName={directory}
              disabled={downloadRequest === 'inProgress'}
              files={tree[directory]}
              footer={
                <button
                  disabled={fetchingDirectoryContents}
                  onClick={() =>
                    this.getFullDirectory(response.username, directory)
                  }
                  style={{
                    backgroundColor: 'transparent',
                    border: 'none',
                    cursor: 'pointer',
                    width: '100%',
                  }}
                  type="button"
                >
                  <Icon
                    loading={fetchingDirectoryContents}
                    name={fetchingDirectoryContents ? 'circle notch' : 'search'}
                  />
                  Search for Additional Files in This Directory
                </button>
              }
              key={directory}
              locked={tree[directory].find((file) => file.locked)}
              onSelectionChange={this.handleFileSelectionChange}
            />
          ))}
        </Card.Content>
        {this.renderDownloadAction(
          selectedFiles,
          selectedSize,
          downloadRequest,
          downloadError,
        )}
      </Card>
    );
  }
}

export default Response;
