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
  exportPresetFragment: vi.fn((type) => ({
    fileName: `active.${type}`,
    source: `[${type}]\nenabled=1\n`,
  })),
  inspectPresetText: vi.fn(() => ({ title: 'Imported native preset' })),
  loadPresetFragmentText: vi.fn((_source, fileName) => ({
    source: `name=Imported native preset\n; merged ${fileName}`,
    title: `Imported native preset + ${fileName}`,
  })),
  loadPresetText: vi.fn(() => 'Imported native preset'),
  nextPreset: vi.fn(() => 'Native next'),
  presetName: 'Native preset',
  render: vi.fn(),
  resize: vi.fn(),
  setPresetAutomation: vi.fn(),
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
    vi.unstubAllGlobals();
    window.localStorage.clear();
    HTMLCanvasElement.prototype.getContext = vi.fn(() => ({}));
    window.requestAnimationFrame = vi.fn(() => 1);
    window.cancelAnimationFrame = vi.fn();
    butterchurnEngine.dispose.mockClear();
    nativeEngine.dispose.mockClear();
    nativeEngine.exportPresetFragment.mockClear();
    nativeEngine.inspectPresetText.mockClear();
    nativeEngine.loadPresetFragmentText.mockClear();
    nativeEngine.loadPresetText.mockClear();
    nativeEngine.nextPreset.mockClear();
    nativeEngine.resize.mockClear();
    nativeEngine.render.mockReset();
    nativeEngine.render.mockImplementation(() => {});
    nativeEngine.setPresetAutomation.mockClear();
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
      { textureAssets: {} },
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
        { textureAssets: {} },
      );
    });

    fireEvent.click(screen.getByTestId('visualizer-clear-native-preset-library'));

    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toBeNull();
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPresetLibrary')).toBeNull();
    expect(screen.queryByTestId('visualizer-native-preset-library')).not.toBeInTheDocument();
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
      { textureAssets: {} },
    );
    expect(screen.getByText(/Imported 2; skipped 1: shader.milk/)).toBeInTheDocument();
  });

  it('imports native .shape and .wave fragments into the active preset', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPreset',
      JSON.stringify({
        fileName: 'base.milk',
        id: 'base',
        source: 'name=Base\nwave_r=1',
        title: 'Base',
      }),
    );

    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
        'name=Base\nwave_r=1',
        'base.milk',
        { textureAssets: undefined },
      );
    });

    const input = document.querySelector('input[type="file"]');
    const shapeFile = new File(['sides=6\nrad=0.2'], 'hex.shape', {
      type: 'text/plain',
    });
    const waveFile = new File(['samples=32\nper_point_1=x=i;'], 'scope.wave', {
      type: 'text/plain',
    });
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: createFileList([shapeFile, waveFile]),
    });
    fireEvent.change(input);

    await waitFor(() => {
      expect(nativeEngine.loadPresetFragmentText).toHaveBeenCalledWith(
        'sides=6\nrad=0.2',
        'hex.shape',
        { textureAssets: {} },
      );
    });
    expect(nativeEngine.loadPresetFragmentText).toHaveBeenCalledWith(
      'samples=32\nper_point_1=x=i;',
      'scope.wave',
      { textureAssets: {} },
    );
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toContain(
      'scope.wave',
    );
    const library = JSON.parse(
      window.localStorage.getItem('slskdn.player.nativeMilkdropPresetLibrary'),
    );
    expect(library[0]).toEqual(expect.objectContaining({
      id: 'base',
      title: 'Imported native preset + scope.wave',
    }));
  });

  it('exports native shape and wave fragments from the active preset', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    const createObjectUrl = vi.fn(() => 'blob:native-fragment');
    const revokeObjectUrl = vi.fn();
    Object.defineProperty(window.URL, 'createObjectURL', {
      configurable: true,
      value: createObjectUrl,
    });
    Object.defineProperty(window.URL, 'revokeObjectURL', {
      configurable: true,
      value: revokeObjectUrl,
    });
    const click = vi.fn();
    const createElement = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((name) => {
      const element = createElement(name);
      if (name === 'a') {
        element.click = click;
      }
      return element;
    });

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

    fireEvent.click(screen.getByTestId('visualizer-export-native-shape'));
    fireEvent.click(screen.getByTestId('visualizer-export-native-wave'));

    expect(nativeEngine.exportPresetFragment).toHaveBeenCalledWith('shape');
    expect(nativeEngine.exportPresetFragment).toHaveBeenCalledWith('wave');
    expect(createObjectUrl).toHaveBeenCalledTimes(2);
    expect(click).toHaveBeenCalledTimes(2);
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:native-fragment');
  });

  it('cycles and persists native automatic preset change modes', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');

    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(nativeEngine.setPresetAutomation).toHaveBeenCalledWith({ mode: 'off' });
    });

    fireEvent.click(screen.getByTestId('visualizer-native-automation'));
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPresetAutomation')).toBe(
      'beat',
    );
    await waitFor(() => {
      expect(nativeEngine.setPresetAutomation).toHaveBeenCalledWith({ mode: 'beat' });
    });

    fireEvent.click(screen.getByTestId('visualizer-native-automation'));
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPresetAutomation')).toBe(
      'timed',
    );
    await waitFor(() => {
      expect(nativeEngine.setPresetAutomation).toHaveBeenCalledWith({ mode: 'timed' });
    });
  });

  it('updates displayed native preset name when automation advances', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    let animationFrameCalled = false;
    window.requestAnimationFrame = vi.fn((callback) => {
      if (!animationFrameCalled) {
        animationFrameCalled = true;
        callback();
      }
      return 1;
    });
    nativeEngine.render.mockImplementationOnce(() => ({
      presetName: 'Native automated next',
    }));

    render(
      <Visualizer
        audioElement={{}}
        mode="fullwindow"
        onModeChange={vi.fn()}
      />,
    );

    expect(await screen.findByText(/Native automated next/)).toBeInTheDocument();
  });

  it('stores selected image assets with imported native presets', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    vi.stubGlobal('FileReader', class {
      readAsDataURL() {
        this.result = 'data:image/png;base64,fixture';
        this.onload();
      }
    });

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
    const presetFile = new File(
      ['name=Textured\nshape00_enabled=1\nshape00_texture=cover.png'],
      'textured.milk',
      { type: 'text/plain' },
    );
    const textureFile = new File(['fixture'], 'cover.png', { type: 'image/png' });
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: createFileList([presetFile, textureFile]),
    });
    fireEvent.change(input);

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
        'name=Textured\nshape00_enabled=1\nshape00_texture=cover.png',
        'textured.milk',
        {
          textureAssets: expect.objectContaining({
            'cover.png': expect.objectContaining({
              dataUrl: 'data:image/png;base64,fixture',
            }),
            cover: expect.objectContaining({
              dataUrl: 'data:image/png;base64,fixture',
            }),
          }),
        },
      );
    });
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toContain(
      'cover.png',
    );
  });

  it('stores only referenced image assets with each imported native preset', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    nativeEngine.inspectPresetText.mockImplementation((_source, fileName) => ({
      title: fileName.replace(/\.milk$/, ''),
    }));
    nativeEngine.loadPresetText.mockImplementation((_source, fileName) =>
      fileName.replace(/\.milk$/, ''));
    vi.stubGlobal('FileReader', class {
      readAsDataURL(file) {
        this.result = `data:${file.name}`;
        this.onload();
      }
    });

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
    const firstPreset = new File(
      ['name=First\nshape00_enabled=1\nshape00_texture=art/first.png'],
      'first.milk',
      { type: 'text/plain' },
    );
    const secondPreset = new File(
      ['name=Second\nsprite00_enabled=1\nsprite00_image=second.png'],
      'second.milk',
      { type: 'text/plain' },
    );
    const firstImage = new File(['first'], 'first.png', { type: 'image/png' });
    const secondImage = new File(['second'], 'second.png', { type: 'image/png' });
    Object.defineProperty(firstImage, 'webkitRelativePath', {
      configurable: true,
      value: 'pack/art/first.png',
    });
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: createFileList([firstPreset, secondPreset, firstImage, secondImage]),
    });
    fireEvent.change(input);

    await waitFor(() => {
      expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toContain(
        'second.milk',
      );
    });

    const activePreset = JSON.parse(
      window.localStorage.getItem('slskdn.player.nativeMilkdropPreset'),
    );
    expect(Object.keys(activePreset.textureAssets).sort()).toEqual(['second', 'second.png']);

    const library = JSON.parse(
      window.localStorage.getItem('slskdn.player.nativeMilkdropPresetLibrary'),
    );
    const firstEntry = library.find((preset) => preset.fileName === 'first.milk');
    expect(Object.keys(firstEntry.textureAssets).sort()).toEqual([
      'first',
      'first.png',
      'pack/art/first.png',
    ]);
    expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
      'name=Second\nsprite00_enabled=1\nsprite00_image=second.png',
      'second.milk',
      {
        textureAssets: expect.not.objectContaining({
          first: expect.anything(),
        }),
      },
    );
  });

  it('imports native preset folders with relative asset paths', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    nativeEngine.inspectPresetText.mockImplementation((_source, fileName) => ({
      title: fileName.replace(/\.milk$/, ''),
    }));
    nativeEngine.loadPresetText.mockImplementation((_source, fileName) =>
      fileName.replace(/\.milk$/, ''));
    vi.stubGlobal('FileReader', class {
      readAsDataURL(file) {
        this.result = `data:${file.name}`;
        this.onload();
      }
    });

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

    const folderInput = screen.getByTestId('visualizer-native-pack-input');
    expect(folderInput).toHaveAttribute('webkitdirectory');
    expect(folderInput).toHaveAttribute('directory');

    const clickSpy = vi.spyOn(folderInput, 'click').mockImplementation(() => {});
    fireEvent.click(screen.getByTestId('visualizer-import-native-preset-folder'));
    expect(clickSpy).toHaveBeenCalled();

    const presetFile = new File(
      ['name=Pack\nsprite00_enabled=1\nsprite00_image=assets/cover.png'],
      'pack.milk',
      { type: 'text/plain' },
    );
    const textureFile = new File(['cover'], 'cover.png', { type: 'image/png' });
    Object.defineProperty(presetFile, 'webkitRelativePath', {
      configurable: true,
      value: 'pack/presets/pack.milk',
    });
    Object.defineProperty(textureFile, 'webkitRelativePath', {
      configurable: true,
      value: 'pack/assets/cover.png',
    });
    Object.defineProperty(folderInput, 'files', {
      configurable: true,
      value: createFileList([presetFile, textureFile]),
    });
    fireEvent.change(folderInput);

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
        'name=Pack\nsprite00_enabled=1\nsprite00_image=assets/cover.png',
        'pack.milk',
        {
          textureAssets: expect.objectContaining({
            'pack/assets/cover.png': expect.objectContaining({
              dataUrl: 'data:cover.png',
            }),
            'cover.png': expect.objectContaining({
              dataUrl: 'data:cover.png',
            }),
            cover: expect.objectContaining({
              dataUrl: 'data:cover.png',
            }),
          }),
        },
      );
    });
  });

  it('reports skipped native texture assets during import', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');

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
    const presetFile = new File(['name=Textured\nshape00_texture=huge.png'], 'textured.milk', {
      type: 'text/plain',
    });
    const textureFile = new File(['fixture'], 'huge.png', { type: 'image/png' });
    Object.defineProperty(textureFile, 'size', {
      configurable: true,
      value: 1024 * 1024 + 1,
    });
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: createFileList([presetFile, textureFile]),
    });
    fireEvent.change(input);

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
        'name=Textured\nshape00_texture=huge.png',
        'textured.milk',
        { textureAssets: {} },
      );
    });
    expect(screen.getByText(/Skipped 1 texture asset: huge.png/)).toBeInTheDocument();
  });

  it('removes only the selected native preset from the local library', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPreset',
      JSON.stringify({
        fileName: 'first.milk',
        id: 'first',
        source: 'name=First\nwave_r=1',
        title: 'First',
      }),
    );
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPresetLibrary',
      JSON.stringify([
        {
          fileName: 'first.milk',
          id: 'first',
          source: 'name=First\nwave_r=1',
          title: 'First',
        },
        {
          fileName: 'second.milk',
          id: 'second',
          source: 'name=Second\nwave_r=0.5',
          title: 'Second',
        },
      ]),
    );

    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
        'name=First\nwave_r=1',
        'first.milk',
        { textureAssets: undefined },
      );
    });

    fireEvent.click(screen.getByTestId('visualizer-remove-native-preset'));

    const library = JSON.parse(
      window.localStorage.getItem('slskdn.player.nativeMilkdropPresetLibrary'),
    );
    expect(library.map((preset) => preset.id)).toEqual(['second']);
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPreset')).toBeNull();
    expect(screen.getByTestId('visualizer-native-preset-library')).toHaveValue('');
  });

  it('supports native preset favorites, history, next, and random library jumps', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPreset',
      JSON.stringify({
        fileName: 'first.milk',
        id: 'first',
        source: 'name=First\nwave_r=1',
        title: 'First',
      }),
    );
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPresetLibrary',
      JSON.stringify([
        {
          fileName: 'first.milk',
          id: 'first',
          source: 'name=First\nwave_r=1',
          title: 'First',
        },
        {
          fileName: 'second.milk',
          id: 'second',
          source: 'name=Second\nwave_r=0.5',
          title: 'Second',
        },
        {
          fileName: 'third.milk',
          id: 'third',
          source: 'name=Third\nwave_b=1',
          title: 'Third',
        },
      ]),
    );
    nativeEngine.loadPresetText.mockImplementation((_source, fileName) =>
      fileName.replace(/\.milk$/, ''));
    const randomSpy = vi.spyOn(Math, 'random').mockReturnValue(0.9);

    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
        'name=First\nwave_r=1',
        'first.milk',
        { textureAssets: undefined },
      );
    });

    fireEvent.click(screen.getByTestId('visualizer-toggle-native-favorite'));
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPresetFavorites')).toContain(
      'first',
    );

    fireEvent.click(screen.getByTestId('visualizer-next-preset'));
    await waitFor(() => {
      expect(screen.getByTestId('visualizer-native-preset-library')).toHaveValue('second');
    });
    expect(nativeEngine.nextPreset).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('visualizer-previous-native-preset'));
    await waitFor(() => {
      expect(screen.getByTestId('visualizer-native-preset-library')).toHaveValue('first');
    });

    fireEvent.click(screen.getByTestId('visualizer-random-native-preset'));
    await waitFor(() => {
      expect(screen.getByTestId('visualizer-native-preset-library')).toHaveValue('third');
    });
    expect(randomSpy).toHaveBeenCalled();

    fireEvent.click(screen.getByTestId('visualizer-toggle-native-favorites-only'));
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPresetLibraryMode')).toBe(
      'favorites',
    );
    expect(screen.getByRole('option', { name: '(favorite) First' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Second' })).not.toBeInTheDocument();
    randomSpy.mockRestore();
  });

  it('filters the native preset bank search and scopes native next navigation', async () => {
    window.localStorage.setItem('slskdn.player.visualizerEngine', 'native');
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPreset',
      JSON.stringify({
        fileName: 'first.milk',
        id: 'first',
        source: 'name=First\nwave_r=1',
        title: 'First',
      }),
    );
    window.localStorage.setItem(
      'slskdn.player.nativeMilkdropPresetLibrary',
      JSON.stringify([
        {
          fileName: 'first.milk',
          id: 'first',
          source: 'name=First\nwave_r=1',
          title: 'First',
        },
        {
          fileName: 'second.milk',
          id: 'second',
          source: 'name=Second\nwave_r=0.5',
          title: 'Second',
        },
        {
          fileName: 'third-grid.milk',
          id: 'third',
          source: 'name=Third Grid\nwave_b=1',
          title: 'Third Grid',
        },
      ]),
    );
    nativeEngine.loadPresetText.mockImplementation((_source, fileName) =>
      fileName.replace(/\.milk$/, ''));

    render(
      <Visualizer
        audioElement={{}}
        mode="inline"
        onModeChange={vi.fn()}
      />,
    );

    await waitFor(() => {
      expect(nativeEngine.loadPresetText).toHaveBeenCalledWith(
        'name=First\nwave_r=1',
        'first.milk',
        { textureAssets: undefined },
      );
    });

    fireEvent.change(screen.getByTestId('visualizer-native-preset-search'), {
      target: { value: 'grid' },
    });
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPresetSearch')).toBe('grid');
    expect(screen.getByRole('option', { name: 'Third Grid' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Second' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('visualizer-next-preset'));
    await waitFor(() => {
      expect(screen.getByTestId('visualizer-native-preset-library')).toHaveValue('third');
    });

    fireEvent.change(screen.getByTestId('visualizer-native-preset-search'), {
      target: { value: 'missing' },
    });
    expect(screen.getByText('No matches')).toBeInTheDocument();
    expect(screen.getByTestId('visualizer-next-preset')).toBeDisabled();

    fireEvent.click(screen.getByTestId('visualizer-clear-native-preset-search'));
    expect(window.localStorage.getItem('slskdn.player.nativeMilkdropPresetSearch')).toBeNull();
    expect(screen.getByTestId('visualizer-native-preset-search')).toHaveValue('');
    expect(screen.getByRole('option', { name: 'Second' })).toBeInTheDocument();
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
