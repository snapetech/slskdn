import {
  parseFiltersFromString,
  serializeFiltersToString,
} from '../../../lib/searches';
import React, { useEffect, useState } from 'react';
import { Button, Form, Header, Modal, Segment } from 'semantic-ui-react';

const SearchFilterModal = ({ filterString, onChange, trigger }) => {
  const [open, setOpen] = useState(false);
  const [filters, setFilters] = useState({});

  useEffect(() => {
    if (open) {
      setFilters(parseFiltersFromString(filterString || ''));
    }
  }, [filterString, open]);

  const handleChange = (key, value) => {
    setFilters((previous) => ({ ...previous, [key]: value }));
  };

  const handleSave = () => {
    const newString = serializeFiltersToString(filters);
    onChange(newString);
    setOpen(false);
  };

  return (
    <Modal
      onClose={() => setOpen(false)}
      onOpen={() => setOpen(true)}
      open={open}
      size="small"
      trigger={trigger}
    >
      <Header
        content="Advanced Search Filters"
        icon="filter"
      />
      <Modal.Content>
        <Form>
          <Segment>
            <Header size="tiny">Audio Properties</Header>
            <Form.Group widths="equal">
              <Form.Input
                label="Min Bitrate (kbps)"
                min={0}
                onChange={(event) =>
                  handleChange(
                    'minBitRate',
                    Number.parseInt(event.target.value, 10) || 0,
                  )
                }
                placeholder="e.g. 320"
                type="number"
                value={filters.minBitRate || ''}
              />
              <Form.Input
                label="Min Bit Depth"
                min={0}
                onChange={(event) =>
                  handleChange(
                    'minBitDepth',
                    Number.parseInt(event.target.value, 10) || 0,
                  )
                }
                placeholder="e.g. 16 or 24"
                type="number"
                value={filters.minBitDepth || ''}
              />
            </Form.Group>
            <Form.Group inline>
              <Form.Checkbox
                checked={filters.isCBR || false}
                label="CBR"
                onChange={(_, { checked }) => handleChange('isCBR', checked)}
              />
              <Form.Checkbox
                checked={filters.isVBR || false}
                label="VBR"
                onChange={(_, { checked }) => handleChange('isVBR', checked)}
              />
              <Form.Checkbox
                checked={filters.isLossless || false}
                label="Lossless"
                onChange={(_, { checked }) =>
                  handleChange('isLossless', checked)
                }
              />
              <Form.Checkbox
                checked={filters.isLossy || false}
                label="Lossy"
                onChange={(_, { checked }) => handleChange('isLossy', checked)}
              />
            </Form.Group>
          </Segment>

          <Segment>
            <Header size="tiny">File Properties</Header>
            <Form.Group widths="equal">
              <Form.Input
                label="Min Size (bytes)"
                min={0}
                onChange={(event) =>
                  handleChange(
                    'minFileSize',
                    Number.parseInt(event.target.value, 10) || 0,
                  )
                }
                placeholder="e.g. 1048576 (1MB)"
                type="number"
                value={filters.minFileSize || ''}
              />
              <Form.Input
                label="Max Size (bytes)"
                min={0}
                onChange={(event) =>
                  handleChange(
                    'maxFileSize',
                    event.target.value
                      ? Number.parseInt(event.target.value, 10)
                      : Number.MAX_SAFE_INTEGER,
                  )
                }
                placeholder="e.g. 104857600 (100MB)"
                type="number"
                value={
                  filters.maxFileSize === Number.MAX_SAFE_INTEGER
                    ? ''
                    : filters.maxFileSize
                }
              />
            </Form.Group>
            <Form.Group widths="equal">
              <Form.Input
                label="Min Duration (seconds)"
                min={0}
                onChange={(event) =>
                  handleChange(
                    'minLength',
                    Number.parseInt(event.target.value, 10) || 0,
                  )
                }
                placeholder="e.g. 180 (3 min)"
                type="number"
                value={filters.minLength || ''}
              />
              <Form.Input
                label="Min Files in Folder"
                min={0}
                onChange={(event) =>
                  handleChange(
                    'minFilesInFolder',
                    Number.parseInt(event.target.value, 10) || 0,
                  )
                }
                placeholder="e.g. 8"
                type="number"
                value={filters.minFilesInFolder || ''}
              />
            </Form.Group>
          </Segment>

          <Segment>
            <Header size="tiny">Keywords</Header>
            <Form.Input
              label="Must Include (space separated)"
              onChange={(event) =>
                handleChange('include', event.target.value.split(' '))
              }
              placeholder="remix instrumental"
              value={(filters.include || []).join(' ')}
            />
            <Form.Input
              label="Must Exclude (space separated)"
              onChange={(event) =>
                handleChange('exclude', event.target.value.split(' '))
              }
              placeholder="live demo"
              value={(filters.exclude || []).join(' ')}
            />
          </Segment>
        </Form>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={() => setOpen(false)}>Cancel</Button>
        <Button
          onClick={handleSave}
          primary
        >
          Apply Filters
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

export default SearchFilterModal;
