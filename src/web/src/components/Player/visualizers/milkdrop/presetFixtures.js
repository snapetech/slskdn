export const nativeMilkdropFixturePack = [
  {
    description: 'Classic feedback, warp grid, vectors, textured shape, and spectrum dots.',
    id: 'classic-primitives',
    supported: true,
    source: `
      name=Fixture Classic Primitives
      decay=0.82
      wave_r=0.25
      wave_g=0.7
      wave_b=0.95
      wave_scale=1.4
      meshx=3
      meshy=2
      per_frame_1=rot=0.015*sin(time);
      per_pixel_1=dx=0.02*sin((x+time)*6.283);
      per_pixel_2=dy=0.02*cos((y+time)*6.283);
      mv_x=4
      mv_y=3
      mv_l=0.2
      mv_a=0.45
      shape00_enabled=1
      shape00_textured=1
      shape00_sides=5
      shape00_rad=0.22
      shape00_x=0.5
      shape00_y=0.5
      shape00_r=0.9
      shape00_g=0.85
      shape00_b=0.15
      shape00_a=0.45
      sprite00_enabled=1
      sprite00_image=fixture-logo.png
      sprite00_x=0.22
      sprite00_y=0.78
      sprite00_w=0.08
      sprite00_h=0.08
      sprite00_a=0.35
      wavecode_0_enabled=1
      wavecode_0_samples=32
      wavecode_0_spectrum=1
      wavecode_0_dots=1
      wavecode_0_r=0.8
      wavecode_0_g=1
      wavecode_0_b=0.3
      wavecode_0_a=0.9
      wavecode_0_per_point1=x=i;
      wavecode_0_per_point2=y=0.1+sample*0.65;
    `,
  },
  {
    description: 'First shader subset with supported warp and comp ret assignments.',
    id: 'shader-subset',
    supported: true,
    source: `
      name=Fixture Shader Subset
      decay=0.78
      wave_r=0.6
      wave_g=0.25
      wave_b=0.9
      warp_shader=ret = tex2D(sampler_main, uv).rgb * vec3(0.8, 0.95, 1.0);
      comp_shader=ret = saturate(vec3(uv.x, uv.y, 0.45 + 0.35 * sin(time)));
      shape00_enabled=1
      shape00_sides=6
      shape00_rad=0.14
      shape00_a=0.22
    `,
  },
  {
    description: 'Simple MilkDrop3-style double preset parse fixture.',
    id: 'milk2-double',
    supported: true,
    source: `
      [preset00]
      name=Fixture Double Left
      zoom=1
      per_frame_1=q33=sin(time);
      [preset01]
      name=Fixture Double Right
      blend_alpha=0.65
      zoom=0.9
      per_frame_1=q34=cos(time);
    `,
  },
  {
    description: 'Unsupported shader control flow should be rejected cleanly.',
    id: 'unsupported-control-flow-shader',
    supported: false,
    expectedError: 'shader translation pending: comp_shader',
    source: `
      name=Fixture Unsupported Shader
      wave_r=1
      comp_shader=for (;;) { ret = vec3(1.0); }
    `,
  },
];

export const getNativeMilkdropFixture = (id) =>
  nativeMilkdropFixturePack.find((fixture) => fixture.id === id);
