import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import Visualizer from './Visualizer';

const butterchurnEngine = {
  dispose: vi.fn(),
  nextPreset: vi.fn(() => 'Butterchurn next'),
  presetName: 'Butterchurn preset',
  render: vi.fn(),
  resize: vi.fn(),
  name: 'Butterchurn',
};

const nativeEngine = {
  dispose: vi.fn(),
  inspectPresetText: vi.fn(() => ({ title: 'Imported native preset' })),
  loadPresetText: vi.fn(() => 'Imported native preset'),
  nextPreset: vi.fn(() => 'Native next'),
  presetName: 'Native preset',
  render: vi.fn(),
  resize: vi.fn(),
  name: 'slskdN MilkDrop WebGL',
};

vi.mock('./audioGraph', () => ({
  resumeAudioGraph: vi.fn(() =>
    Promise.resolve({
      ctx: {},
      visualizerInput: {},
    })),
}));

vi.mock('./visualizers/butterchurnEngine', () => ({
  createButterchurnEngine: vi.fn(() => Promise.resolve(butterchurnEngine)),
}));

vi.mock('./visualizers/nativeMilkdropEngine', () => ({
  createNativeMilkdropEngine: vi.fn(() => Promise.resolve(nativeEngine)),
}));

const createFileList = (fileOrFiles) => {
  const files = Array.isArray(fileOrFiles) ? fileOrFiles : [fileOrFiles];
  files.item = (index) => files[index];
  return files;
};

describe('Visualizer', () => {
  beforeEach(() => {
    window.localStorage.clear();
    HTMLCanvasElement.prototype.getContext = vi.fn(() => ({}));
    window.requestAnimationFrame = vi.fn(() => 1);
    window.cancelAnimationFrame = vi.fn();
    butterchurnEngine.dispose.mockClear();
    nativeEngine.dispose.mockClear();
    nativeEngine.inspectPresetText.mockClear();
    nativeEngine.loadPresetText.mockClear();
    nativeEngine.resize.mockClear();
    nativeEngine.render.mockReset();
    nativeEngine.render.mockImplementation(() => {});
  });

  it('switches to the native engine and imports a local preset', async () => {
    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    fireEvent.click(await screen.findByTestId('visualizer-switch-engine'));

    await waitFor(() => {
      expect(window.localStorage.getItem('slskdn.player.visualizerEngine')).toBe('native');
    });

    const input = document.querySelector('input[type="file"]');
    const file = new File(['name=Imported native preset\nwave_r=1'], 'imported.milk', {
      type: 'text/plain',
    });
    const fileId = `${file.name}:${file.size}:${file.lastModified}`;
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: createFileList(file),
    });
    fireEvent.change(input);

    await waitFor(() => {
      expect(nativeEngine.inspectPresetText).toHaveBeenCalledWith(
        'name=Imported native preset\nwave_r=1',
        'imported.milk',
      );
    });
    expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
      'name=Imported native preset\nwave_r=1',
      'imported.milk',
    );
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toContain(
      'Imported native preset',
    );
    expect(
      window.localStorage.getItem('slskdn.player.nativeMilkdropPresetLibrary'),
    ).toContain('Imported native preset');

    fireEvent.change(screen.getByTestId('visualizer-native-preset-library'), {
      target: { value: fileId },
    });

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenLastCalledWith(
        'name=Imported native preset\nwave_r=1',
        'imported.milk',
      );
    });
  });

  it('imports compatible native preset batches and reports skipped files', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    nativeEngine.inspectPresetText.mockImplementation((source, fileName) => {
      if (source.includes('warp_shader')) {
        throw new Error('Native MilkDrop preset has shader translation pending: warp_shader.');
      }
      return { title: fileName.replace(/\.milk2?$/, '') };
    });
    nativeEngine.loadPresetText.mockImplementation((source, fileName) =>
      fileName.replace(/\.milk2?$/, ''));

    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(nativeEngine.resize).toHaveBeenCalled();
    });

    const input = document.querySelector('input[type="file"]');
    const firstFile = new File(['name=First\nwave_r=1'], 'first.milk', {
      type: 'text/plain',
    });
    const skippedFile = new File(['warp_shader=shader_body'], 'shader.milk', {
      type: 'text/plain',
    });
    const secondFile = new File(['name=Second\nwave_r=0.5'], 'second.milk', {
      type: 'text/plain',
    });
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: createFileList([firstFile, skippedFile, secondFile]),
    });
    fireEvent.change(input);

    await waitFor(() => {
      expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toContain(
        'second.milk',
      );
    });

    const library = JSON.parse(
      window.localStorage.getItem('slskdn.player.nativeMilkdropPresetLibrary'),
    );
    expect(library.map((preset) => preset.fileName)).toEqual(['second.milk', 'first.milk']);
    expect(nativeEngine.loadPresetText).toHaveBeenCalledTimes(1);
    expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
      'name=Second\nwave_r=0.5',
      'second.milk',
    );
    expect(screen.getByText(/Imported 2; skipped 1: shader.milk/)).toBeInTheDocument();
  });

  it('surfaces native render errors and clears the persisted imported preset', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPreset',
      JSON.stringify({
        fileName: 'bad.milk',
        source: 'per_frame_1=q1=rand(1);',
      }),
    );
    window.requestAnimationFrame = vi.fn((callback) => {
      callback();
      return 1;
    });
    nativeEngine.render.mockImplementationOnce(() => {
      throw new Error('Unsupported MilkDrop function: rand');
    });

    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    expect(
      await screen.findByText(/Native MilkDrop render failed. Unsupported MilkDrop function: rand/),
    ).toBeInTheDocument();
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toBeNull();
  });
});
