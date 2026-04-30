import { bands, setEqGains } from './audioGraph';
import React, { useEffect, useState } from 'react';
import { Button, Dropdown, Icon, Popup } from 'semantic-ui-react';

const storageKey = 'slskdn.player.equalizer';

const presets = {
  Flat: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
  Classical: [0, 0, 0, 0, 0, 0, -2, -2, -2, -3],
  Dance: [5, 4, 2, 0, 0, -2, -2, 2, 4, 5],
  Metal: [4, 3, 2, 1, -1, -2, 0, 2, 4, 5],
  Rock: [4, 3, 2, 0, -1, -1, 1, 2, 3, 4],
  Vocal: [-2, -2, -1, 2, 4, 4, 3, 1, 0, -1],
};

const defaultState = {
  enabled: false,
  gains: presets.Flat,
  preset: 'Flat',
};

const readStoredState = () => {
  if (typeof window === 'undefined') return defaultState;

  try {
    const stored = JSON.parse(window.localStorage.getItem(storageKey));
    if (!stored || !Array.isArray(stored.gains)) return defaultState;
    return {
      enabled: stored.enabled === true,
      gains: bands.map((_, index) => Number(stored.gains[index]) || 0),
      preset: stored.preset || 'Custom',
    };
  } catch {
    return defaultState;
  }
};

const formatBand = (frequency) =>
  frequency >= 1000 ? `${frequency / 1000}k` : String(frequency);

const Equalizer = ({ audioElement }) => {
  const [state, setState] = useState(readStoredState);

  useEffect(() => {
    if (!audioElement) return;
    setEqGains(audioElement, state.enabled ? state.gains : presets.Flat);
  }, [audioElement, state.enabled, state.gains]);

  useEffect(() => {
    window.localStorage.setItem(storageKey, JSON.stringify(state));
  }, [state]);

  const presetOptions = Object.keys(presets).map((name) => ({
    key: name,
    text: name,
    value: name,
  }));

  return (
    <div className="player-eq" data-testid="player-equalizer">
      <div className="player-panel-header">
        <strong>EQ</strong>
        <div className="player-panel-actions">
          <Dropdown
            compact
            data-testid="player-eq-preset"
            onChange={(_, { value }) =>
              setState((existing) => ({
                ...existing,
                gains: presets[value],
                preset: value,
              }))
            }
            options={presetOptions}
            selection
            value={state.preset in presets ? state.preset : 'Flat'}
          />
          <Popup
            content={
              state.enabled
                ? 'Bypass the equalizer without losing your slider settings.'
                : 'Enable the equalizer for this browser player.'
            }
            trigger={
              <Button
                aria-label={state.enabled ? 'Disable equalizer' : 'Enable equalizer'}
                compact
                data-testid="player-eq-toggle"
                icon
                onClick={() =>
                  setState((existing) => ({
                    ...existing,
                    enabled: !existing.enabled,
                  }))
                }
                primary={state.enabled}
                size="mini"
              >
                <Icon name="sliders horizontal" />
              </Button>
            }
          />
        </div>
      </div>
      <div className="player-eq-bands">
        {bands.map((frequency, index) => (
          <label className="player-eq-band" key={frequency}>
            <input
              aria-label={`${formatBand(frequency)} equalizer gain`}
              data-testid={`player-eq-slider-${frequency}`}
              disabled={!state.enabled}
              max="12"
              min="-12"
              onChange={(event) => {
                const gain = Number(event.target.value);
                setState((existing) => ({
                  enabled: existing.enabled,
                  gains: existing.gains.map((value, gainIndex) =>
                    gainIndex === index ? gain : value,
                  ),
                  preset: 'Custom',
                }));
              }}
              step="1"
              type="range"
              value={state.gains[index]}
            />
            <span>{formatBand(frequency)}</span>
          </label>
        ))}
      </div>
    </div>
  );
};

export default Equalizer;
