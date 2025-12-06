import React from 'react';
import { Icon, List } from 'semantic-ui-react';

const subtree = (root, selectedDirectoryName, onSelect, onDownload) => {
  return (root || []).map((d) => {
    const selected = d.name === selectedDirectoryName;
    // const dimIfLocked = { opacity: d.locked ? 0.5 : 1 };

    return (
      <List
        className="browse-folderlist-list"
        key={d.name}
      >
        <List.Item>
          <List.Icon
            className={
              'browse-folderlist-icon' +
              (selected ? ' selected' : '') +
              (d.locked ? ' locked' : '')
            }
            name={
              d.locked === true ? 'lock' : selected ? 'folder open' : 'folder'
            }
          />
          <List.Content>
            <List.Header
              className={
                'browse-folderlist-header' +
                (selected ? ' selected' : '') +
                (d.locked ? ' locked' : '')
              }
              onClick={(event) => onSelect(event, d)}
            >
              {d.name.split('\\').pop().split('/').pop()}
              <Icon
                className="browse-folder-download-icon"
                name="download"
                onClick={(event) => {
                  event.stopPropagation();
                  onDownload(d);
                }}
                style={{ marginLeft: '0.5em', opacity: 0.7 }}
                title="Download folder"
              />
            </List.Header>
            <List.List>
              {subtree(d.children, selectedDirectoryName, onSelect, onDownload)}
            </List.List>
          </List.Content>
        </List.Item>
      </List>
    );
  });
};

const DirectoryTree = ({ onDownload, onSelect, selectedDirectoryName, tree }) =>
  subtree(tree, selectedDirectoryName, onSelect, onDownload);

export default DirectoryTree;
