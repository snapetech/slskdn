import { describe, expect, it, vi } from 'vitest';
import {
  createNativeMilkdropEngine,
  getNativeMilkdropBeatUpdate,
  getNativeMilkdropTransitionAlphas,
  getNativeMilkdropTransitionProgress,
} from './nativeMilkdropEngine';
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
  it('eases native transition progress between renderer sets', () => {
    expect(getNativeMilkdropTransitionProgress(10, 2, 10)).toBe(0);
    expect(getNativeMilkdropTransitionProgress(10, 2, 11)).toBe(0.5);
    expect(getNativeMilkdropTransitionProgress(10, 2, 12)).toBe(1);
    expect(getNativeMilkdropTransitionProgress(10, 0, 10)).toBe(1);
  });

  it('maps native transition modes to incoming and outgoing alphas', () => {
    expect(getNativeMilkdropTransitionAlphas(0.25, 'crossfade')).toEqual({
      incoming: 0.25,
      outgoing: 0.75,
    });
    expect(getNativeMilkdropTransitionAlphas(0.25, 'fade_through_black')).toEqual({
      incoming: 0,
      outgoing: 0.5,
    });
    expect(getNativeMilkdropTransitionAlphas(0.75, 'fade')).toEqual({
      incoming: 0.5,
      outgoing: 0,
    });
    expect(getNativeMilkdropTransitionAlphas(0.5, 'overlay')).toEqual({
      incoming: 0.5,
      outgoing: 1,
    });
    expect(getNativeMilkdropTransitionAlphas(0.5, 'cut')).toEqual({
      incoming: 1,
      outgoing: 0,
    });
  });

  it('detects beat pulses from low-frequency spectrum energy', () => {
    const baseline = getNativeMilkdropBeatUpdate(
      {},
      [16, 18, 17, 19],
      1,
      { beatSensitivity: 1.35, minBeatIntervalSeconds: 0.25 },
    );
    const pulse = getNativeMilkdropBeatUpdate(
      baseline,
      [240, 230, 220, 210],
      1.4,
      { beatSensitivity: 1.35, minBeatIntervalSeconds: 0.25 },
    );

    expect(baseline.isBeat).toBe(false);
    expect(pulse.isBeat).toBe(true);
    expect(pulse.beatCount).toBe(1);
  });

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

    expect(engine.name).toBe('slskdN MilkDrop WebGL2');
    expect(audioNode.connect).toHaveBeenCalledWith(analyser);
    expect(renderer.render).toHaveBeenCalledWith(
      expect.objectContaining({
        sampleRate: 48000,
        time: 12,
      }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 1 },
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

  it('feeds mouse state into native preset render frames', async () => {
    const analyser = createAnalyser();
    const engine = await createNativeMilkdropEngine({
      audioContext: {
        createAnalyser: () => analyser,
        currentTime: 12,
        sampleRate: 48000,
      },
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.render.mockClear();

    engine.setMouseState({
      mouse_down: 1,
      mouse_dx: 0.2,
      mouse_dy: -0.1,
      mouse_x: 0.75,
      mouse_y: 0.25,
    });
    engine.render();

    expect(renderer.render).toHaveBeenCalledWith(
      expect.objectContaining({
        audio: expect.objectContaining({
          mouse_down: 1,
          mouse_dx: 0.2,
          mouse_dy: -0.1,
          mouse_x: 0.75,
          mouse_y: 0.25,
        }),
      }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 1 },
    );
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
      transition_seconds=2.5
      wave_r=1
      [preset01]
      name=Double secondary
      blend_alpha=0.75
      blend_mode=additive
      wave_b=1
    `, 'double.milk2', { blendSeconds: 0 });
    engine.render();
    engine.resize(640, 360);

    expect(presetName).toBe('Double primary + Double secondary');
    expect(createMilkdropRenderer).toHaveBeenCalledTimes(2);
    expect(renderer.render).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ sampleRate: 44100, time: 3 }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 1 },
    );
    expect(renderer.render).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ sampleRate: 44100, time: 3 }),
      { clearScreen: false, compositeMode: 'additive', outputAlpha: 0.75 },
    );
    expect(renderer.resize).toHaveBeenCalledTimes(2);
    expect(renderer.resize).toHaveBeenCalledWith(640, 360);
  });

  it('uses preset-defined .milk2 transition duration and composite aliases', async () => {
    const analyser = createAnalyser();
    const audioContext = {
      createAnalyser: () => analyser,
      currentTime: 10,
      sampleRate: 44100,
    };
    const engine = await createNativeMilkdropEngine({
      audioContext,
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.render.mockClear();

    engine.loadPresetText(`
      [preset00]
      name=Slow primary
      blend_time=4
      [preset01]
      name=Screen secondary
      composite_mode=screen
      composite_alpha=0.4
    `, 'slow-double.milk2');
    audioContext.currentTime = 12;
    engine.render();

    expect(renderer.render).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ sampleRate: 44100, time: 12 }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 0.5 },
    );
    expect(renderer.render).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ sampleRate: 44100, time: 12 }),
      { clearScreen: false, compositeMode: 'alpha', outputAlpha: 0.5 },
    );
    expect(renderer.render).toHaveBeenNthCalledWith(
      3,
      expect.objectContaining({ sampleRate: 44100, time: 12 }),
      { clearScreen: false, compositeMode: 'screen', outputAlpha: 0.2 },
    );
  });

  it('crossfades preset switches before disposing the outgoing renderer set', async () => {
    const analyser = createAnalyser();
    const audioContext = {
      createAnalyser: () => analyser,
      currentTime: 10,
      sampleRate: 44100,
    };
    const engine = await createNativeMilkdropEngine({
      audioContext,
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.render.mockClear();
    renderer.dispose.mockClear();

    engine.nextPreset({ blendSeconds: 2 });
    audioContext.currentTime = 11;
    engine.render();

    expect(renderer.render).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ sampleRate: 44100, time: 11 }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 0.5 },
    );
    expect(renderer.render).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ sampleRate: 44100, time: 11 }),
      { clearScreen: false, compositeMode: 'alpha', outputAlpha: 0.5 },
    );
    expect(renderer.dispose).not.toHaveBeenCalled();

    renderer.render.mockClear();
    audioContext.currentTime = 12.1;
    engine.render();

    expect(renderer.dispose).toHaveBeenCalled();
    expect(renderer.render).toHaveBeenCalledTimes(1);
    expect(renderer.render).toHaveBeenCalledWith(
      expect.objectContaining({ sampleRate: 44100, time: 12.1 }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 1 },
    );
  });

  it('uses preset-defined transition modes for imported presets', async () => {
    const analyser = createAnalyser();
    const audioContext = {
      createAnalyser: () => analyser,
      currentTime: 10,
      sampleRate: 44100,
    };
    const engine = await createNativeMilkdropEngine({
      audioContext,
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.render.mockClear();
    renderer.dispose.mockClear();

    engine.loadPresetText(`
      name=Fade mode fixture
      transition_seconds=2
      transition_mode=fade
      wave_r=1
    `, 'fade.milk');
    audioContext.currentTime = 10.5;
    engine.render();

    expect(renderer.render).toHaveBeenCalledTimes(1);
    expect(renderer.render.mock.calls[0][0]).toEqual(
      expect.objectContaining({ sampleRate: 44100, time: 10.5 }),
    );
    expect(renderer.render.mock.calls[0][1]).toEqual(
      expect.objectContaining({ clearScreen: true, compositeMode: 'alpha' }),
    );
    expect(renderer.render.mock.calls[0][1].outputAlpha).toBeCloseTo(0.6875);
    renderer.render.mockClear();

    audioContext.currentTime = 11.5;
    engine.render();

    expect(renderer.render).toHaveBeenCalledTimes(1);
    expect(renderer.render.mock.calls[0][0]).toEqual(
      expect.objectContaining({ sampleRate: 44100, time: 11.5 }),
    );
    expect(renderer.render.mock.calls[0][1]).toEqual(
      expect.objectContaining({ clearScreen: true, compositeMode: 'alpha' }),
    );
    expect(renderer.render.mock.calls[0][1].outputAlpha).toBeCloseTo(0.6875);
  });

  it('supports cut and overlay transition modes from caller options', async () => {
    const analyser = createAnalyser();
    const audioContext = {
      createAnalyser: () => analyser,
      currentTime: 10,
      sampleRate: 44100,
    };
    const engine = await createNativeMilkdropEngine({
      audioContext,
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });
    renderer.render.mockClear();
    renderer.dispose.mockClear();

    engine.nextPreset({ blendSeconds: 2, transitionMode: 'cut' });
    audioContext.currentTime = 11;
    engine.render();

    expect(renderer.dispose).toHaveBeenCalled();
    expect(renderer.render).toHaveBeenCalledTimes(1);
    expect(renderer.render).toHaveBeenCalledWith(
      expect.objectContaining({ sampleRate: 44100, time: 11 }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 1 },
    );
    renderer.render.mockClear();

    engine.nextPreset({ blendSeconds: 2, transitionMode: 'overlay' });
    audioContext.currentTime = 12;
    engine.render();

    expect(renderer.render).toHaveBeenNthCalledWith(
      1,
      expect.objectContaining({ sampleRate: 44100, time: 12 }),
      { clearScreen: true, compositeMode: 'alpha', outputAlpha: 1 },
    );
    expect(renderer.render).toHaveBeenNthCalledWith(
      2,
      expect.objectContaining({ sampleRate: 44100, time: 12 }),
      { clearScreen: false, compositeMode: 'alpha', outputAlpha: 0.5 },
    );
  });

  it('advances presets automatically on timed automation', async () => {
    const analyser = createAnalyser();
    const audioContext = {
      createAnalyser: () => analyser,
      currentTime: 0,
      sampleRate: 44100,
    };
    const engine = await createNativeMilkdropEngine({
      audioContext,
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });

    engine.setPresetAutomation({ mode: 'timed', timedIntervalSeconds: 5 });
    audioContext.currentTime = 4.9;
    expect(engine.render()).toBeNull();
    audioContext.currentTime = 5.1;
    expect(engine.render()).toEqual({ presetName: 'slskdN native waveform smoke' });
  });

  it('advances presets automatically after repeated detected beats', async () => {
    const spectrumFrames = [
      [16, 18, 17, 19],
      [240, 230, 220, 210],
      [18, 18, 17, 19],
      [245, 235, 225, 215],
    ];
    const analyser = {
      fftSize: 0,
      frequencyBinCount: 4,
      getByteFrequencyData: vi.fn((data) => {
        data.set(spectrumFrames.shift() || [18, 18, 17, 19]);
      }),
      getByteTimeDomainData: vi.fn((data) => {
        data.set([128, 128, 128, 128]);
      }),
    };
    const audioContext = {
      createAnalyser: () => analyser,
      currentTime: 0,
      sampleRate: 44100,
    };
    const engine = await createNativeMilkdropEngine({
      audioContext,
      audioNode: {
        connect: vi.fn(),
        disconnect: vi.fn(),
      },
      canvas: { getContext: vi.fn() },
    });

    engine.setPresetAutomation({
      beatSensitivity: 1.35,
      beatsPerPreset: 2,
      minBeatIntervalSeconds: 0.25,
      mode: 'beat',
    });
    audioContext.currentTime = 0.1;
    expect(engine.render()).toBeNull();
    audioContext.currentTime = 0.5;
    expect(engine.render()).toBeNull();
    audioContext.currentTime = 0.9;
    expect(engine.render()).toBeNull();
    audioContext.currentTime = 1.8;
    expect(engine.render()).toEqual({ presetName: 'slskdN native waveform smoke' });
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

  it('merges .shape and .wave fragments into the active preset and exports them', async () => {
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

    engine.loadPresetText(`
      name=Fragment base
      wave_r=1
    `, 'base.milk', { blendSeconds: 0 });
    const shapeResult = engine.loadPresetFragmentText(`
      sides=7
      rad=0.2
      per_frame_1=ang=time;
    `, 'star.shape', { blendSeconds: 0 });
    const waveResult = engine.loadPresetFragmentText(`
      samples=32
      per_point_1=x=i;
      per_point_2=y=sample;
    `, 'scope.wave', { blendSeconds: 0 });

    expect(shapeResult.title).toBe('Fragment base + star.shape');
    expect(shapeResult.source).toContain('shape00_sides=7');
    expect(shapeResult.source).toContain('shape00_per_frame_1=ang=time;');
    expect(waveResult.title).toBe('Fragment base + star.shape + scope.wave');
    expect(waveResult.source).toContain('wavecode_0_samples=32');
    expect(waveResult.source).toContain('wavecode_0_per_point_2=y=sample;');
    expect(createMilkdropRenderer).toHaveBeenLastCalledWith(expect.objectContaining({
      preset: expect.objectContaining({
        shapes: expect.arrayContaining([
          expect.objectContaining({
            baseValues: expect.objectContaining({ sides: 7 }),
          }),
        ]),
        waves: expect.arrayContaining([
          expect.objectContaining({
            baseValues: expect.objectContaining({ samples: 32 }),
          }),
        ]),
      }),
    }));
    expect(engine.exportPresetFragment('shape')).toEqual(expect.objectContaining({
      fileName: 'Fragment_base_star.shape_scope.wave.shape',
      source: expect.stringContaining('sides=7'),
    }));
    expect(engine.exportPresetFragment('wave')).toEqual(expect.objectContaining({
      fileName: 'Fragment_base_star.shape_scope.wave.wave',
      source: expect.stringContaining('samples=32'),
    }));
    expect(engine.exportPresetText()).toEqual(expect.objectContaining({
      fileName: 'Fragment_base_star.shape_scope.wave.milk',
      source: expect.stringContaining('wavecode_0_samples=32'),
    }));
    expect(engine.getPresetFragmentSummary()).toEqual({
      shapes: [{ index: 0, label: 'Shape 1: 7 sides' }],
      waves: [{ index: 0, label: 'Wave 1: 32 samples' }],
    });
  });

  it('removes selected shape and wave fragments from the active preset', async () => {
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

    engine.loadPresetText(`
      name=Editable fragments
      shape00_enabled=1
      shape00_sides=3
      shape01_enabled=1
      shape01_sides=8
      wavecode_0_enabled=1
      wavecode_0_samples=32
      wavecode_1_enabled=1
      wavecode_1_samples=64
    `, 'editable.milk', { blendSeconds: 0 });

    const shapeResult = engine.removePresetFragment('shape', 1, { blendSeconds: 0 });
    expect(shapeResult.title).toBe('Editable fragments - shape 2');
    expect(shapeResult.source).toContain('shape00_sides=3');
    expect(shapeResult.source).not.toContain('shape01_sides=8');
    expect(engine.getPresetFragmentSummary().shapes).toEqual([
      { index: 0, label: 'Shape 1: 3 sides' },
    ]);

    const waveResult = engine.removePresetFragment('wave', 0, { blendSeconds: 0 });
    expect(waveResult.title).toBe('Editable fragments - shape 2 - wave 1');
    expect(waveResult.source).toContain('wavecode_0_samples=64');
    expect(waveResult.source).not.toContain('wavecode_1_samples=64');
    expect(engine.removePresetFragment('wave', 3)).toBeNull();
  });

  it('updates supported global preset parameters and serializes the edited preset', async () => {
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

    engine.loadPresetText(`
      name=Editable globals
      zoom=1
      wave_r=0.2
    `, 'editable.milk', { blendSeconds: 0 });
    const result = engine.updatePresetBaseValue('zoom', 1.25, { blendSeconds: 0 });

    expect(result.title).toBe('Editable globals edited');
    expect(result.source).toContain('zoom=1.25');
    expect(result.values).toEqual(expect.objectContaining({
      wave_r: 0.2,
      zoom: 1.25,
    }));
    expect(engine.getPresetParameterSummary()).toEqual(expect.objectContaining({
      wave_r: 0.2,
      zoom: 1.25,
    }));
    expect(createMilkdropRenderer).toHaveBeenLastCalledWith(expect.objectContaining({
      preset: expect.objectContaining({
        baseValues: expect.objectContaining({ zoom: 1.25 }),
      }),
    }));
    expect(engine.updatePresetBaseValue('unsupported', 1)).toBeNull();
    expect(engine.updatePresetBaseValue('zoom', Number.NaN)).toBeNull();
  });

  it('randomizes editable parameters and exposes debug snapshots', async () => {
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

    engine.loadPresetText(`
      name=Debug fixture
      decay=0.9
      zoom=1
      warp_shader=ret = vec3(uv, 1.0);
      shape00_enabled=1
      shape00_sides=4
      wavecode_0_enabled=1
      wavecode_0_samples=32
    `, 'debug.milk', { blendSeconds: 0 });
    const result = engine.randomizePresetParameters({
      blendSeconds: 0,
      random: () => 1,
    });

    expect(result.title).toBe('Debug fixture randomized');
    expect(result.values).toEqual(expect.objectContaining({
      decay: 0.95,
      rot: 0.25,
      zoom: 1.25,
    }));
    expect(result.source).toContain('zoom=1.25');
    expect(engine.getPresetDebugSnapshot()).toEqual(expect.objectContaining({
      format: 'milk',
      presetCount: 1,
      shaderSections: { comp: false, warp: true },
      shapes: 1,
      title: 'Debug fixture randomized',
      waves: 1,
    }));
  });

  it('rejects unsupported fragment equations before replacing the active renderer', async () => {
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

    expect(() => engine.loadPresetFragmentText(`
      per_frame_1=rad=unknown_shape_call(time);
    `, 'bad.shape')).toThrow('unsupported functions: unknown_shape_call');
    expect(createMilkdropRenderer).not.toHaveBeenCalled();
  });
});
