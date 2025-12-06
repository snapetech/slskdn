import ErrorSegment from '../../Shared/ErrorSegment';
import Switch from '../../Shared/Switch';
import SearchListRow from './SearchListRow';
import React from 'react';
import { Button, Card, Icon, Loader, Popup, Table } from 'semantic-ui-react';

const SearchList = ({
  connecting = false,
  error = undefined,
  onRemove = () => {},
  onRemoveAll = () => {},
  onStop = () => {},
  removingAll = false,
  searches = {},
}) => {
  return (
    <Card
      className="search-list-card"
      raised
    >
      <Card.Content>
        <div className="search-list-header">
          <Popup
            content="Clear all completed searches"
            position="left center"
            trigger={
              <Button
                color="red"
                compact
                disabled={removingAll || Object.keys(searches).length === 0}
                icon
                labelPosition="left"
                loading={removingAll}
                onClick={onRemoveAll}
                size="small"
              >
                <Icon name="trash" />
                Clear All
              </Button>
            }
          />
        </div>
        <div className="search-list-wrapper">
          <Switch
            connecting={
              connecting && (
                <Loader
                  active
                  inline="centered"
                  size="small"
                />
              )
            }
            error={error && <ErrorSegment caption={error} />}
          >
            <Table
              className="unstackable"
              size="large"
            >
              <Table.Header>
                <Table.Row>
                  <Table.HeaderCell className="search-list-action">
                    <Icon name="info circle" />
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-phrase">
                    Search
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-files">
                    Files
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-locked">
                    Locked
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-responses">
                    Responses
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-started">
                    Ended
                  </Table.HeaderCell>
                  <Table.HeaderCell className="search-list-action" />
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {Object.values(searches)
                  .sort((a, b) => new Date(b.startedAt) - new Date(a.startedAt))
                  .map((search) => (
                    <SearchListRow
                      key={search.id}
                      onRemove={onRemove}
                      onStop={onStop}
                      search={search}
                    />
                  ))}
              </Table.Body>
            </Table>
          </Switch>
        </div>
      </Card.Content>
    </Card>
  );
};

export default SearchList;
