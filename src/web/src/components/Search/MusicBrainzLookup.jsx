import React, { useMemo, useState } from 'react';
import { Button, Form, Header, Input, List, Segment } from 'semantic-ui-react';
import { toast } from 'react-toastify';
import { resolveTarget } from '../../lib/musicBrainz';

const MusicBrainzLookup = ({ disabled }) => {
  const [releaseInput, setReleaseInput] = useState('');
  const [recordingInput, setRecordingInput] = useState('');
  const [discogsInput, setDiscogsInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [target, setTarget] = useState(null);

  const handleLookup = async () => {
    if (!releaseInput && !recordingInput && !discogsInput) {
      toast.error('Provide at least one MusicBrainz or Discogs identifier');
      return;
    }

    setLoading(true);

    try {
      const payload = {
        releaseId: releaseInput.trim() || undefined,
        recordingId: recordingInput.trim() || undefined,
        discogsReleaseId: discogsInput.trim() || undefined,
      };

      const response = await resolveTarget(payload);
      setTarget(response.data);

      toast.success(
        response.data.album
          ? `Loaded album ${response.data.album.title}`
          : `Loaded track ${response.data.track?.title}`,
      );
    } catch (error) {
      console.error(error);
      toast.error(error?.response?.data ?? error?.message ?? 'Failed to resolve target');
    } finally {
      setLoading(false);
    }
  };

  const summary = useMemo(() => {
    if (!target) {
      return null;
    }

    if (target.album) {
      return (
        <List>
          <List.Item>
            <List.Header>Album</List.Header>
            <List.Description>
              {target.album.title} · {target.album.artist} · {target.album.tracks?.length ?? 0} tracks
            </List.Description>
          </List.Item>
        </List>
      );
    }

    if (target.track) {
      return (
        <List>
          <List.Item>
            <List.Header>Track</List.Header>
            <List.Description>
              {target.track.title} · {target.track.artist} · {target.track.duration ? `${(target.track.duration / 60000).toFixed(2)} min` : 'unknown length'}
            </List.Description>
          </List.Item>
        </List>
      );
    }

    return null;
  }, [target]);

  return (
    <Segment raised className="musicbrainz-lookup-segment">
      <Header as="h4">MusicBrainz / Discogs Lookup</Header>
      <Form>
        <Form.Field>
          <Input
            label="MusicBrainz Release ID"
            placeholder="e.g. 1c3b3668-..."
            value={releaseInput}
            onChange={(event) => setReleaseInput(event.target.value)}
            disabled={disabled || loading}
          />
        </Form.Field>
        <Form.Field>
          <Input
            label="MusicBrainz Recording ID"
            placeholder="e.g. 8af4c1b9-..."
            value={recordingInput}
            onChange={(event) => setRecordingInput(event.target.value)}
            disabled={disabled || loading}
          />
        </Form.Field>
        <Form.Field>
          <Input
            label="Discogs Release/Master ID"
            placeholder="e.g. 123456"
            value={discogsInput}
            onChange={(event) => setDiscogsInput(event.target.value)}
            disabled={disabled || loading}
          />
        </Form.Field>
        <Button
          primary
          loading={loading}
          onClick={handleLookup}
          disabled={disabled || loading}
        >
          Resolve target
        </Button>
      </Form>
      {summary}
    </Segment>
  );
};

export default MusicBrainzLookup;


