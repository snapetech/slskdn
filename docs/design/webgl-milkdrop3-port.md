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

- [x] Parse `.milk` presets into a typed model.
- [x] Implement the first expression evaluation slice for core preset variables.
- [x] Add a minimal WebGL2 renderer skeleton driven by parsed/evaluated preset state.
- [x] Add the first feedback texture ping-pong pipeline.
- [x] Add first preset-driven warp uniforms for zoom, rotation, and translation.
- [x] Add first waveform line-strip primitive pass from audio samples.
- [x] Add first parsed shape outline primitive pass.
- [x] Add first custom shape init/frame equation evaluation.
- [x] Add first filled, bordered, alpha-blended, and additive custom shape rendering.
- [x] Add first custom shape second-color gradients and thick-outline handling.
- [x] Add first custom wave init/frame/point equation rendering.
- [x] Add first custom wave dots and spectrum modes.
- [x] Add first analyzer-backed `get_fft` / `get_fft_hz` expression helpers.
- [x] Add first CPU-evaluated per-pixel warp grid renderer.
- [x] Add first motion-vector primitive renderer.
- [x] Add an explicit player UI engine switch for the native WebGL renderer.
- [x] Add a browser/WebGL canvas pixel smoke test for the native renderer.
- [x] Add first local `.milk` / `.milk2` preset import for the native renderer.
- [x] Add native preset runtime-error surfacing and bad-import cleanup.
- [x] Add more common NSEEL math helpers for imported preset compatibility.
- [x] Add first import-time native preset compatibility reporting.
- [x] Add browser-local native preset library and multi-file preset import.
- [x] Add first local native preset library management affordances.
- [x] Add first inline bitwise/shift/logical expression operator support.
- [x] Add first safe `warp_shader` / `comp_shader` translation and execution subset.
- [x] Add first curated native preset fixture pack with golden parser and compatibility coverage.
- [x] Add first procedural textured-shape render path.
- [x] Add first local image texture asset import path for native presets.
- [x] Add first `.milk2` double-preset simultaneous render/composite path.
- [x] Add first sprite/image primitive parse and render path.
- [x] Scope imported texture/image assets to the presets that reference them.
- [x] Add first folder import affordance for native preset packs.
- [x] Add first native renderer-set crossfade scheduler and `.milk2` composite-alpha control.
- [x] Add first `.shape` and `.wave` fragment import/export path.
- [x] Add first beat and timed automatic preset change modes.
- [x] Add first favorites, history, next, and random controls for the browser-local native preset bank.
- [x] Add first search/filter control for the browser-local native preset bank.
- [x] Add first browser-local native preset playlists.
- [x] Add renderer-wide q1-q64 initialization and q-register propagation across evaluated stages.
- [x] Add first shader-uniform binding for q-register and audio variables in translated warp/comp shaders.
- [x] Add first shader-side `get_fft()` and `get_fft_hz()` support for translated warp/comp shaders.
- [x] Add safe straight-line temp declarations and common HLSL helper aliases to translated shaders.
- [x] Add viewport uniforms and generated coordinate helpers to translated shaders.
- [x] Add safe `shader_body { ... }` wrapper unwrapping for translated shaders.
- [x] Add first named-texture sampler binding for translated shaders.
- [x] Add first simple ret-only shader conditional translation.
- [x] Add safe declared-temp reassignment support in translated shaders.
- [x] Add first native primitive-field alias support for common custom wave, shape, and sprite names.
- [x] Add first classic `ob_*` and `ib_*` screen-border rendering.
- [x] Add first classic `wave_mode`, `wave_x/y`, `wave_a`, and `wave_smoothing` support.
- Render feedback, warp, comp, simple waves, custom waves, shapes, borders, motion vectors, and basic textures in WebGL2.
- Use a curated compatibility fixture pack with golden parse snapshots and headless canvas smoke tests.

Current parser/VM scope:

- Parses classic `.milk` base values, global init/per-frame/per-pixel equations, warp/comp shader text, custom shapes, custom waves, and first-pass sprite/image primitives.
- Detects simple `.milk2` double-preset files and preserves both preset bodies.
- Preserves MilkDrop3 q1-q64 variables in parsed presets and initializes all q-registers in the renderer scope.
- Evaluates deterministic arithmetic, assignment, compound assignment, comparison, and core helper functions.
- Throws on unsupported syntax instead of silently mis-evaluating presets.
- Creates a WebGL2 program and draws a placeholder full-screen triangle from evaluated MilkDrop color variables. This is not yet a MilkDrop feedback renderer; it proves the parser/VM/render boundary can drive GPU output.
- Allocates two WebGL texture/framebuffer targets, writes each frame into the feedback target, blits that target to screen, then swaps read/write targets. The current pass uses `decay` as the feedback blend; warp/comp shader logic is still pending.
- Applies first-pass warp state from evaluated `zoom`, `rot`, `dx`, and `dy` variables while sampling the previous feedback texture. This is still a compatibility stepping stone, not translated preset warp shader execution.
- Converts incoming waveform samples into clip-space vertices and draws them as a WebGL `LINE_STRIP` into the feedback target before the screen blit. The first classic waveform pass supports horizontal, centered, vertical, and circular-ish modes plus `wave_x`, `wave_y`, `wave_a`, `wave_scale`, and `wave_smoothing`; higher-fidelity MilkDrop wave modes are still pending.
- Converts enabled parsed shape entries into closed polygon line strips using `x`, `y`, `rad`, `sides`, `ang`, and `r/g/b` values.
- Renders classic MilkDrop outer and inner screen borders from `ob_size/ob_r/g/b/a` and `ib_size/ib_r/g/b/a` as alpha-blended filled rings.
- Evaluates custom shape init/frame equations before drawing and persists shape-owned values plus q-registers without leaking global frame/audio variables into shape base values.
- Draws custom shapes as triangle-fan fills plus optional border line strips, including alpha blending and the parsed `additive` flag.
- Supports first-pass shape center-to-edge gradients through `r2/g2/b2/a2` and thick-outline line width hints. Textured shapes and full MilkDrop shape modes are still pending.
- Evaluates custom wave init/frame/point equations into WebGL line-strip vertices using audio samples as point inputs, with per-wave q-register persistence, color/alpha, additive blending, and thick line hints.
- Propagates q-register writes from global frame equations and evaluated custom wave, shape, and sprite stages back into the frame scope while still preventing non-q primitive-local frame/audio values from leaking into primitive base values.
- Supports first-pass custom wave dot rendering and spectrum-source sampling from frame frequency data.
- Honors common native custom wave aliases including `nSamples`, `bSpectrum`, `bUseDots`, `bDrawThick`, and `bAdditive`, plus shape/sprite aliases including `bTextured`, `bAdditive`, `bDrawThick`, `numSides`, and `texName`.
- Supports analyzer-backed `get_fft(pos)` and `get_fft_hz(freq)` expression helpers against renderer-provided frequency data. Full MilkDrop wave modes and shader-side FFT access are still pending.
- Supported translated warp/comp shaders can reference `q1`-`q64` plus `bass`, `bass_att`, `mid`, `mid_att`, `treb`, and `treb_att`; the renderer binds those values from the current frame scope as uniforms before each translated shader pass.
- Translated warp/comp shaders expose shader-side `get_fft(pos)` and `get_fft_hz(freq)` helpers backed by a 64-bin normalized FFT uniform array and current sample-rate uniform. They also expose signed 64-bin waveform access through `get_waveform(pos)`.
- Translated warp/comp shaders receive viewport context through `resolution`, `pixelSize`, `aspect`, and `texsize`, and generated MilkDrop-style per-fragment coordinate helpers `x`, `y`, `rad`, and `ang`.
- Translated warp/comp shaders can sample up to four named preset texture samplers through `tex`/`tex2D`; imported texture assets are matched with the same alias rules used by shape/sprite rendering, and missing sampler assets fall back to the procedural checker texture.
- Rebinds WebGL vertex attributes before each fullscreen, warp-grid, wave, and shape draw so program switches cannot leave draw calls pointed at stale buffers.
- Draws presets with global `per_pixel` equations through a CPU-evaluated triangle grid. The grid evaluates MilkDrop `x`, `y`, `rad`, and `ang` values per vertex, converts local `dx/dy/zoom/rot` into source UVs, and samples the previous feedback texture through a dedicated grid shader. This is a compatibility stepping stone before GLSL translation of full warp shaders.
- Draws first-pass motion vectors from `mv_x`, `mv_y`, `mv_dx`, `mv_dy`, `mv_l`, and `mv_r/g/b/a` values as alpha-blended WebGL line segments.
- The player visualizer overlay can now switch between Butterchurn and the native `slskdN MilkDrop WebGL` engine. The native adapter uses the shared Web Audio visualizer tap, reads waveform/frequency data through its own analyser, and feeds curated smoke presets through the native renderer. Butterchurn remains the default engine while the native path matures.
- `npm run test:native-milkdrop-smoke` starts a local Vite server, loads the real native renderer modules in Chromium, renders a curated WebGL preset to a canvas, and fails if readback pixel statistics indicate a blank frame.
- Native mode exposes a local file import button for `.milk` and `.milk2` text presets. Imported preset text is loaded into the native renderer, the preset name is surfaced in the overlay, and the last imported preset is persisted in browser local storage for the next native-engine session.
- Native mode exposes a separate folder import affordance for preset packs. Browsers that support directory file inputs provide relative paths for presets and image assets, which feed the scoped texture lookup path without requiring users to flatten pack folders manually.
- Native mode accepts standalone `.shape` and `.wave` fragments, merges them into the active native preset, reserializes the merged preset for browser-local persistence, and can export the first active custom shape or wave back to a fragment file.
- Native mode summarizes active custom shape and wave fragments, lets users choose which fragment to export, and can remove a selected fragment from the active preset while persisting the edited preset locally.
- Native mode includes first safe parameter editing for common global values such as decay, zoom, rotation, and waveform color/alpha. Edits rebuild the active renderer, persist as an edited browser-local preset, and can be exported as preset text.
- Native mode can randomize the same bounded visual parameter set to create a local mashup/edit, exposes pointer position/delta/button state as MilkDrop-style mouse variables, and has a compact debug snapshot overlay for preset format, primitive counts, shader sections, and active title.
- Native mode includes first browser-local FPS caps plus a debug frame-time readout so users can trade smoothness for lower GPU load.
- Native render errors are caught at the animation-frame boundary, surfaced in the visualizer overlay with the underlying unsupported-function/syntax detail, and clear the persisted imported preset so a bad preset does not fail every future native-engine session.
- The expression VM now supports additional common NSEEL helpers and constants used by imported presets: `pi`, `e`, `acos`, `asin`, `atan`, `atan2`, `tan`, `log`, `log10`, `exp`, `sign`, `sigmoid`, `rand`, and bitwise helper functions `band`, `bor`, `bxor`, and `bnot`.
- The expression VM also supports inline `&`, `|`, `^`, `~`, `!`, `<<`, `>>`, `&&`, and `||` operators so presets that use operator syntax instead of helper functions do not get rejected.
- Imported native presets are now compatibility-scanned before they replace the active renderer. The report identifies unsupported equation functions across global, shape, and wave equations, and flags `warp_shader` / `comp_shader` sections only when the shader body is outside the current safe translator subset.
- `.milk2` imports compatibility-scan every preserved preset body before storing or rendering the file. Compatible double presets now instantiate one renderer per preset body, draw the primary body normally, and blend secondary bodies over it. Secondary presets default to half opacity and can opt into `blend_alpha` / `blendalpha` / `composite_alpha` values plus `blend_mode` / `composite_mode` aliases for alpha, additive, screen, or multiply final compositing. Primary preset `transition_seconds` / `transition_time` / `blend_time` aliases can set the renderer-set transition duration when the caller does not override it.
- Native preset switches and imported preset loads now use a renderer-set transition scheduler. The default crossfade keeps the outgoing renderer alive while fading it down, the incoming set fades up with an eased progress curve, and expired outgoing renderers are disposed after the transition. Presets and callers can also select `cut`, `fade`, or `overlay` transition modes through `transition_mode` aliases or engine options.
- Native mode has automatic preset change controls. Beat mode uses low-frequency spectrum energy to count detected bass beats before advancing, timed mode advances on an interval, and the selected mode plus beat/interval settings are persisted in browser storage.
- Native imports support multi-select batches. Compatible presets are added to a capped browser-local library and can be reloaded from a compact overlay selector; incompatible presets are skipped with a count and sample filenames instead of aborting the whole batch.
- The browser-local native preset library now has first preset-bank controls. Users can favorite the active imported preset, filter the selector to favorites, move forward through the imported bank, jump back through recent manual selections, and pick a random imported preset without dropping back to built-in native presets.
- Native preset-bank search filters imported presets by title or filename, persists the current query locally, and scopes next/random navigation to the filtered set so users can treat a search result as a lightweight playlist.
- Native preset playlists persist named browser-local lists of imported preset IDs. Users can save the current visible bank as a playlist, switch the active bank to a playlist, clear the playlist scope without deleting it, or delete the active playlist; search/favorites still compose on top of the active playlist.
- Native preset playlist editing includes browser-local active-playlist rename support so users can refine saved banks without recreating them.
- Native mode has clear-library and remove-selected affordances for pruning imported presets from this browser without requiring manual local-storage cleanup.
- The first shader translator accepts simple shader bodies that assign `ret = ...` using GLSL-like expressions, `tex2D(sampler_main, uv)` sampling, `saturate`, and `lerp`. It also accepts safe `shader_body { ... }` wrappers, straight-line `float/float2/float3/float4` temp declarations, reassignment of declared temp variables, simple `if (...) ret = ...; else ret = ...;` conditionals translated to ternaries, and aliases common HLSL helpers such as `frac`, `fmod`, `rsqrt`, and `atan2` to GLSL equivalents. Supported `warp_shader` bodies run in the feedback pass; supported `comp_shader` bodies run during the screen composite. Loops, complex control flow, matrix types, assignment to undeclared or non-local variables, general HLSL, and unknown texture/sampler forms remain explicitly unsupported.
- The first procedural textured-shape path renders parsed `textured`, `texture`, `tex`, or `tex_name` shape references through a generated checker texture and texture-coordinate shader. This proves the texture pipeline without bundling external preset assets yet.
- Native preset imports can include small local image files selected in the same file picker batch. Those images are stored with the imported preset by filename/basename/stem and passed into the renderer as named texture assets. Preset texture references are normalized across quotes, path separators, basename, and stem so common pack layouts like `textures/cover.png` still resolve when the user imports `cover.png`; missing texture names fall back to the procedural checker.
- Multi-preset imports scope image assets per preset instead of attaching the whole selected image batch to every imported preset. The import path also indexes browser-provided relative paths (`webkitRelativePath`) when available, so directory-style pack paths can resolve while keeping unrelated images out of browser-local preset storage.
- Oversized, unreadable, or unsupported files selected during native preset import are reported in the visualizer overlay instead of being ignored silently. Texture assets are capped at 1 MB while this browser-local path matures.
- First-pass `spriteNN_` primitives parse base values plus init/frame equations, compatibility-check sprite equations, and render enabled sprites as textured quads. Sprites use imported texture assets by image/texture filename aliases and fall back to the procedural checker when an image is missing.
- The first curated fixture pack covers a classic primitive/textured-shape/sprite preset, a supported shader subset preset, a simple `.milk2` double-preset file, a MilkDrop3-style q-register coverage double preset, a dense 40-shape/20-wave primitive-count probe, and an unsupported shader-control-flow preset. Tests lock golden parser summaries and compatibility outcomes, and the browser smoke renders the textured classic fixture, shader fixture, `.milk2` double fixture, q-register fixture, and dense primitive fixture with per-fixture pixel statistics.
- `npm run test:native-milkdrop-compatibility` builds a compatibility matrix over the curated fixtures by default, or over supplied `.milk` / `.milk2` files and folders, reporting supported counts, scanned preset bodies, max shape/wave/sprite counts, q-register coverage, unsupported functions, and unsupported shader sections. This gives Phase 2 real-preset-pack work a repeatable measurement path before each renderer gap is closed.

### Phase 2: MilkDrop3 Feature Deltas

- [x] Add native compatibility matrix reporting for curated fixtures and local preset files/folders.
- [x] Add first high-count wave/shape compatibility metric coverage for real-pack pressure.
- [x] Add richer `.milk2` transition/composite controls beyond first secondary alpha support.
- [x] Add deeper q1-q64 compatibility coverage against real MilkDrop3 presets.
- [x] Add increased wave/shape count validation against real MilkDrop3 preset packs.
- [x] Add richer `.shape` and `.wave` library management beyond the first import/export affordances.
- [x] Add additional MilkDrop3 transition modes beyond the first smooth renderer-set crossfade.
- [x] Add richer beat-driven/random/history preset selection modes beyond the first beat/timed automation and local-bank navigation controls.
- [x] Add richer shader-side texture/audio access beyond the first 32-bin FFT uniform path.

### Phase 3: Editing And VJ Controls

- [x] Add richer playlist editing and richer favorites/history views.
- [x] Add visual parameter editing and safe save-as/export.
- [x] Add mashup/randomize controls.
- [x] Add mouse variables and user texture/image support.
- [x] Add debug variable overlays and shader error reporting.

### Phase 4: WebGPU And Native-Like Polish

- Add optional WebGPU renderer for heavier shaders and better texture pipelines.
- Add shader cache/precompile where browser APIs allow it.
- Add performance quality presets, FPS caps, and GPU load indicators.
- [x] Add first browser-local FPS caps and debug frame-time indicator.
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
