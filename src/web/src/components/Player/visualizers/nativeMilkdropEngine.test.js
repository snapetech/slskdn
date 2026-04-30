import { describe, expect, it, vi } from 'vitest';
import { createNativeMilkdropEngine } from './nativeMilkdropEngine';
import { createMilkdropRenderer } from './milkdrop/milkdropRenderer';

const renderer = {
  dispose: vi.fn(),
  render: vi.fn(),
  resize: vi.fn(),
};

vi.mock('./milkdrop/milkdropRenderer', () => ({
  createMilkdropRenderer: vi.fn(() => renderer),
}));

const createAnalyser = () => ({
  fftSize: 0,
  frequencyBinCount: 4,
  getByteFrequencyData: vi.fn((data) => {
    data.set([0, 128, 255, 64]);
  }),
  getByteTimeDomainData: vi.fn((data) => {
    data.set([0, 128, 255, 128]);
  }),
});

describe('createNativeMilkdropEngine', () => {
  it('feeds waveform and spectrum frames into the native renderer', async () => {
    const analyser = createAnalyser();
    const audioNode = {
      connect: vi.fn(),
      disconnect: vi.fn(),
    };
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 12,
        sampleRate: 48000,
      },
      audioNode,
      canvas: { getContext: vi.fn() },
    });

    engine.render();
    engine.resize(320, 180);

    expect(engine.name).toBe('slskdN MilkDrop WebGL');
    expect(audioNode.connect).toHaveBeenCalledWith(analyser);
    expect(renderer.render).toHaveBeenCalledWith(
      expect.objectContaining({
        sampleRate: 48000,
        time: 12,
      }),
      { clearScreen: true, outputAlpha: 1 },
    );
    expect(renderer.render.mock.calls[0][0].samples.slice(0, 4)).toEqual([
      -1,
      0,
      127 / 128,
      0,
    ]);
    expect(renderer.render.mock.calls[0][0].spectrum).toEqual(
      expect.objectContaining({ length: 4 }),
    );
    expect(renderer.resize).toHaveBeenCalledWith(320, 180);
  });

  it('cycles presets by replacing the renderer and disposes audio taps', async () => {
    const analyser = createAnalyser();
    const audioNode = {
      connect: vi.fn(),
      disconnect: vi.fn(),
    };
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 0,
        sampleRate: 44100,
      },
      audioNode,
      canvas: { getContext: vi.fn() },
    });

    const nextName = engine.nextPreset();
    engine.dispose();

    expect(nextName).toBe('slskdN native waveform smoke');
    expect(renderer.dispose).toHaveBeenCalled();
    expect(audioNode.disconnect).toHaveBeenCalledWith(analyser);
  });

  it('loads imported preset text through the native renderer', async () => {
    const analyser = createAnalyser();
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 0,
        sampleRate: 44100,
      },
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });

    const presetName = engine.loadPresetText(`
      name=Imported fixture
      wave_r=1
    `, 'imported.milk');

    expect(presetName).toBe('Imported fixture');
    expect(renderer.dispose).toHaveBeenCalled();
  });

  it('passes imported texture assets into the native renderer', async () => {
    const analyser = createAnalyser();
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 0,
        sampleRate: 44100,
      },
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    createMilkdropRenderer.mockClear();

    const textureAssets = {
      'cover.png': {
        dataUrl: 'data:image/png;base64,fixture',
      },
    };
    engine.loadPresetText(`
      name=Textured fixture
      shape00_enabled=1
      shape00_texture=cover.png
    `, 'textured.milk', { textureAssets });

    expect(createMilkdropRenderer).toHaveBeenCalledWith(expect.objectContaining({
      textureAssets,
    }));
  });

  it('renders compatible .milk2 imports as blended double presets', async () => {
    const analyser = createAnalyser();
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 3,
        sampleRate: 44100,
      },
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.render.mockClear();
    renderer.resize.mockClear();
    createMilkdropRenderer.mockClear();

    const presetName = engine.loadPresetText(`
      [preset00]
      name=Double primary
      wave_r=1
      [preset01]
      name=Double secondary
      wave_b=1
    `, 'double.milk2');
    engine.render();
    engine.resize(640, 360);

    expect(presetName).toBe('Double primary + Double secondary');
    expect(createMilkdropRenderer).toHaveBeenCalledTimes(2);
    expect(renderer.render).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ sampleRate: 44100, time: 3 }),
      { clearScreen: true, outputAlpha: 1 },
    );
    expect(renderer.render).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ sampleRate: 44100, time: 3 }),
      { clearScreen: false, outputAlpha: 0.5 },
    );
    expect(renderer.resize).toHaveBeenCalledTimes(2);
    expect(renderer.resize).toHaveBeenCalledWith(640, 360);
  });

  it('inspects imported preset compatibility without replacing the renderer', async () => {
    const analyser = createAnalyser();
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 0,
        sampleRate: 44100,
      },
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.dispose.mockClear();

    const result = engine.inspectPresetText(`
      name=Inspected fixture
      wave_r=1
    `, 'inspected.milk');

    expect(result).toEqual({ title: 'Inspected fixture' });
    expect(renderer.dispose).not.toHaveBeenCalled();
  });

  it('rejects imported presets with unsupported native features before replacing the renderer', async () => {
    const analyser = createAnalyser();
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 0,
        sampleRate: 44100,
      },
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.dispose.mockClear();

    expect(() => engine.loadPresetText(`
      per_frame_1=q1=megabuf(0);
      comp_shader=for (;;) { ret = vec3(1); }
    `, 'unsupported.milk')).toThrow(
      'unsupported functions: megabuf; shader translation pending: comp_shader',
    );
    expect(renderer.dispose).not.toHaveBeenCalled();
  });

  it('rejects .milk2 imports when the secondary preset is unsupported', async () => {
    const analyser = createAnalyser();
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 0,
        sampleRate: 44100,
      },
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.dispose.mockClear();

    expect(() => engine.inspectPresetText(`
      [preset00]
      name=Compatible primary
      wave_r=1
      [preset01]
      name=Unsupported secondary
      comp_shader=while (true) { ret = vec3(1); }
    `, 'double.milk2')).toThrow(
      'preset 2: Native MilkDrop preset has shader translation pending: comp_shader.',
    );
    expect(renderer.dispose).not.toHaveBeenCalled();
  });
});
