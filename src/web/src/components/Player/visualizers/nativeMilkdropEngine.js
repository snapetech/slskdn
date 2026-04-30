import { createMilkdropRenderer } from './milkdrop/milkdropRenderer';
import {
  analyzeMilkdropPresetCompatibility,
  getMilkdropCompatibilityError,
} from './milkdrop/presetCompatibility';
import { parseMilkdropPreset } from './milkdrop/presetParser';

const nativePresets = [
  {
    name: 'slskdN native grid smoke',
    source: `
      name=slskdN native grid smoke
      decay=0.91
      wave_r=0.12
      wave_g=0.64
      wave_b=0.88
      wave_scale=1.2
      zoom=1
      rot=0
      per_frame_1=wave_r=0.35+0.25*bass_att;
      per_frame_2=wave_g=0.45+0.2*mid_att;
      per_frame_3=wave_b=0.55+0.2*treb_att;
      per_frame_4=rot=0.01*sin(time*0.7);
      per_frame_5=zoom=1+0.03*sin(time*0.5);
      per_pixel_1=dx=0.015*sin((x+time)*6.283);
      per_pixel_2=dy=0.015*cos((y+time)*6.283);
      mv_x=8
      mv_y=5
      mv_dx=0.2
      mv_dy=0.1
      mv_l=0.15
      mv_a=0.32
      shape00_enabled=1
      shape00_sides=5
      shape00_rad=0.18
      shape00_x=0.5
      shape00_y=0.5
      shape00_r=0.1
      shape00_g=0.9
      shape00_b=0.45
      shape00_a=0.35
      shape00_r2=0.9
      shape00_g2=0.8
      shape00_b2=0.2
      shape00_a2=0.18
      shape00_border_a=0.9
      shape00_per_frame1=ang=time*0.5;
      wavecode_0_enabled=1
      wavecode_0_samples=96
      wavecode_0_spectrum=1
      wavecode_0_dots=1
      wavecode_0_r=0.7
      wavecode_0_g=0.95
      wavecode_0_b=0.25
      wavecode_0_a=0.75
      wavecode_0_per_point1=x=i;
      wavecode_0_per_point2=y=0.08+sample*0.55;
    `,
  },
  {
    name: 'slskdN native waveform smoke',
    source: `
      name=slskdN native waveform smoke
      decay=0.88
      wave_r=0.85
      wave_g=0.34
      wave_b=0.18
      wave_scale=1.5
      per_frame_1=dx=0.02*sin(time*0.4);
      per_frame_2=dy=0.015*cos(time*0.3);
      per_frame_3=rot=0.02*sin(time*0.2);
      shape00_enabled=1
      shape00_sides=3
      shape00_rad=0.12+0.03*bass_att
      shape00_x=0.35
      shape00_y=0.55
      shape00_r=0.9
      shape00_g=0.2
      shape00_b=0.1
      shape00_a=0.28
      shape00_additive=1
      shape01_enabled=1
      shape01_sides=6
      shape01_rad=0.08+0.02*treb_att
      shape01_x=0.67
      shape01_y=0.45
      shape01_r=0.1
      shape01_g=0.55
      shape01_b=0.95
      shape01_a=0.35
      wavecode_0_enabled=1
      wavecode_0_samples=128
      wavecode_0_r=0.95
      wavecode_0_g=0.85
      wavecode_0_b=0.2
      wavecode_0_a=0.8
      wavecode_0_per_point1=x=i;
      wavecode_0_per_point2=y=0.5+sample*0.35;
    `,
  },
];

const createFrameReader = (audioContext, audioNode) => {
  const analyser = audioContext.createAnalyser();
  analyser.fftSize = 2048;
  audioNode.connect(analyser);

  const waveform = new Uint8Array(analyser.fftSize);
  const frequency = new Uint8Array(analyser.frequencyBinCount);

  return {
    disconnect: () => {
      try {
        audioNode.disconnect(analyser);
      } catch {
        // The shared audio graph may have been rebuilt or torn down first.
      }
    },
    read: () => {
      analyser.getByteTimeDomainData(waveform);
      analyser.getByteFrequencyData(frequency);
      return {
        samples: Array.from(waveform, (value) => (value - 128) / 128),
        spectrum: frequency,
      };
    },
  };
};

const getImportedPresetTitle = (preset, fileName) =>
  preset.metadata?.title || fileName || 'Imported preset';

const parseCompatiblePresetText = (source, fileName = '') => {
  const importedPreset = parseMilkdropPreset(source, {
    format: fileName.toLowerCase().endsWith('.milk2') ? 'milk2' : undefined,
  }).primary;
  const compatibilityError = getMilkdropCompatibilityError(
    analyzeMilkdropPresetCompatibility(importedPreset),
  );
  if (compatibilityError) {
    throw new Error(compatibilityError);
  }
  return importedPreset;
};

export const createNativeMilkdropEngine = async ({
  audioContext,
  audioNode,
  canvas,
}) => {
  let presetIndex = 0;
  let parsedPreset = parseMilkdropPreset(nativePresets[presetIndex].source).primary;
  let renderer = createMilkdropRenderer({ canvas, preset: parsedPreset });
  const frameReader = createFrameReader(audioContext, audioNode);

  const loadPreset = (index) => {
    presetIndex = index % nativePresets.length;
    parsedPreset = parseMilkdropPreset(nativePresets[presetIndex].source).primary;
    renderer.dispose();
    renderer = createMilkdropRenderer({ canvas, preset: parsedPreset });
    return nativePresets[presetIndex].name;
  };

  return {
    name: 'slskdN MilkDrop WebGL',
    presetName: nativePresets[presetIndex].name,
    dispose: () => {
      frameReader.disconnect();
      renderer.dispose();
    },
    loadPresetText: (source, fileName = '') => {
      const importedPreset = parseCompatiblePresetText(source, fileName);
      renderer.dispose();
      parsedPreset = importedPreset;
      renderer = createMilkdropRenderer({ canvas, preset: parsedPreset });
      return getImportedPresetTitle(parsedPreset, fileName);
    },
    inspectPresetText: (source, fileName = '') => {
      const importedPreset = parseCompatiblePresetText(source, fileName);
      return {
        title: getImportedPresetTitle(importedPreset, fileName),
      };
    },
    nextPreset: () => loadPreset(presetIndex + 1),
    render: () => {
      const frame = frameReader.read();
      renderer.render({
        ...frame,
        sampleRate: audioContext.sampleRate,
        time: audioContext.currentTime,
      });
    },
    resize: (width, height) => {
      renderer.resize(width, height);
    },
  };
};
