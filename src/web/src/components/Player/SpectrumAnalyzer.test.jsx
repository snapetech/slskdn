import { getFrequencyBars, getScopePoints } from './SpectrumAnalyzer';

describe('SpectrumAnalyzer helpers', () => {
  it('aggregates frequency bins into logarithmic bars', () => {
    const data = new Uint8Array(1024);
    data[2] = 120;
    data[3] = 120;
    data[900] = 240;

    const bars = getFrequencyBars(data, 16);

    expect(bars).toHaveLength(16);
    expect(bars.some((value) => value > 0)).toBe(true);
    expect(bars[0]).toBeGreaterThan(0);
    expect(bars[15]).toBeGreaterThan(0);
  });

  it('centers and expands quiet scope samples without exceeding the canvas', () => {
    const data = new Uint8Array([126, 128, 130, 129]);
    const points = getScopePoints(data, 120, 80);

    expect(points).toHaveLength(4);
    expect(points[1].y).toBe(40);
    expect(Math.min(...points.map((point) => point.y))).toBeGreaterThanOrEqual(0);
    expect(Math.max(...points.map((point) => point.y))).toBeLessThanOrEqual(80);
    expect(Math.abs(points[0].y - 40)).toBeGreaterThan(0.5);
  });
});
