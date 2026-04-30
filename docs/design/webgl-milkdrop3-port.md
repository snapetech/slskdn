# WebGL MilkDrop3 Port Plan

## Context

slskdN already has an in-browser MilkDrop-compatible path through Butterchurn. That is useful, but it is not the same product target as MilkDrop3: MilkDrop3 adds double presets, larger shape/wave counts, q1-q64 variables, beat-driven preset changes, richer transitions, preset editing/mashups, mouse variables, FFT shader access, and a desktop-grade control surface.

The upstream MilkDrop3 repository is source-available under BSD-3-Clause in `code/LICENSE.txt`, but the implementation is a C++/Win32/Direct3D program. A direct browser port is not realistic because the renderer, windowing, input, text rendering, audio capture, and device lifecycle are tied to DirectX/Windows APIs. The portable path is a browser-native engine that uses MilkDrop3 source as a compatibility reference while implementing rendering against browser graphics APIs.

## Decision

Build a native slskdN visualizer engine target instead of making the external launcher the long-term answer.

The engine should be:

- Browser-native: WebGL2 first, WebGPU optional later.
- Hardware accelerated: all preset rendering, compositing, transitions, and texture feedback run on GPU.
- Preset-compatible: start with `.milk`, then add MilkDrop3 `.milk2` double presets and feature deltas.
- Audio-native: consume the existing shared Web Audio graph, not system loopback capture.
- Extensible: separate parsing, expression VM, shader translation, render graph, preset library, transitions, and UI controls.
- Portable: no Windows-only runtime dependency; Linux/macOS/Windows browsers should all use the same engine.

The opt-in external visualizer launcher can remain as a bridge for people who want current MilkDrop3 immediately. It must not become the main architecture.

## Proposed Architecture

### Visualizer Engine Boundary

Create a browser-side interface that the player can host:

```ts
interface VisualizerEngine {
  loadPreset(preset: VisualizerPreset, blendSeconds?: number): Promise<void>;
  nextPreset(): Promise<void>;
  render(frame: VisualizerFrame): void;
  resize(width: number, height: number, pixelRatio: number): void;
  dispose(): void;
}
```

The current Butterchurn adapter should sit behind this boundary first. The MilkDrop3-compatible engine then replaces it incrementally without forcing player UI rewrites.

### Core Modules

- **Preset parser**: parse `.milk` and `.milk2`, including shapes, waves, warp shader, comp shader, textures, sprites, transition metadata, and MilkDrop3-specific variables.
- **Expression VM**: evaluate MilkDrop/NSEEL-style init, per-frame, per-vertex, custom wave, and custom shape equations. Start in JS/WASM; keep deterministic tests for preset expressions.
- **Audio features**: expose bass/mid/treb, waveform buffers, FFT bins, `get_fft(pos)`, and `get_fft_hz(freq)` from the existing Web Audio analyzer chain.
- **Shader translation**: translate supported MilkDrop shader text into WebGL2 GLSL ES. Track unsupported HLSL/DirectX-only constructs explicitly instead of silently failing.
- **Render graph**: implement the classic feedback texture pipeline, warp pass, comp pass, waveform/shape/sprite passes, text/overlay pass, and transition compositor.
- **Preset library**: indexed local preset packs, favorites, playlists, search/filter, “new/random/previous” history, and eventually user-added presets.
- **Control surface**: keyboard shortcuts where browsers allow them, toolbar actions, fullscreen, FPS cap, transition timing, beat-driven preset change modes, and debug overlays.

## Phases

### Phase 0: Foundation

- [x] Introduce the player visualizer engine boundary.
- [x] Keep Butterchurn as the default adapter.
- Add engine metadata to the UI so users know which engine is active.
- Add screenshot/pixel-stat smoke tests for canvas output.

### Phase 1: Classic Preset Renderer

- Parse `.milk` presets into a typed model.
- Implement expression evaluation for core preset variables.
- Render feedback, warp, comp, simple waves, custom waves, shapes, borders, motion vectors, and basic textures in WebGL2.
- Use a curated compatibility fixture pack with golden parse snapshots and headless canvas smoke tests.

### Phase 2: MilkDrop3 Feature Deltas

- Add q1-q64 support.
- Add increased wave/shape counts.
- Add `.shape` and `.wave` import/export.
- Add `.milk2` double-preset parsing and simultaneous render/composite.
- Add MilkDrop3 transition modes and smooth blending.
- Add beat-driven preset selection modes.
- Add `get_fft(pos)` and `get_fft_hz(freq)` shader/audio access.

### Phase 3: Editing And VJ Controls

- Add preset search, playlists, favorites, and history.
- Add visual parameter editing and safe save-as/export.
- Add mashup/randomize controls.
- Add mouse variables and user texture/image support.
- Add debug variable overlays and shader error reporting.

### Phase 4: WebGPU And Native-Like Polish

- Add optional WebGPU renderer for heavier shaders and better texture pipelines.
- Add shader cache/precompile where browser APIs allow it.
- Add performance quality presets, FPS caps, and GPU load indicators.
- Keep WebGL2 as the compatibility baseline.

## Risks

- Some MilkDrop3 presets use HLSL or DirectX-era assumptions that may need translation limits or compatibility shims.
- Browser autoplay and audio permissions mean the engine must stay tied to user-initiated player playback.
- Large preset packs can inflate bundles; preset packs must remain lazy-loaded chunks or user-managed assets.
- Full desktop parity is a long project. Shipping a stable compatibility core before editor/VJ features is the safest path.

## Validation

- Unit tests for parser and expression VM.
- Golden snapshots for parsed preset models.
- Headless Playwright screenshots with pixel statistics across desktop/mobile sizes.
- WebGL context loss/recovery smoke tests.
- Performance checks for FPS, GPU memory pressure, and bundle chunk sizes.
- Manual compatibility runs against a curated MilkDrop/MilkDrop3 preset matrix.
