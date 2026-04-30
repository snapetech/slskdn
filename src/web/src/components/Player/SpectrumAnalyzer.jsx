import { resumeAudioGraph } from './audioGraph';
import React, { useCallback, useEffect, useRef } from 'react';

const minScopeGainPeak = 0.02;
const scopeTargetPeak = 0.78;
const maxScopeGain = 4;
const scopeDisplaySamples = 192;
const scopeReadIntervalMs = 45;

export const getFrequencyBars = (data, barCount) => {
  if (!data?.length || barCount <= 0) return [];

  const maxBin = data.length - 1;
  const minBin = Math.min(2, maxBin);
  const minLog = Math.log10(minBin + 1);
  const maxLog = Math.log10(maxBin + 1);

  return Array.from({ length: barCount }, (_, index) => {
    const startRatio = index / barCount;
    const endRatio = (index + 1) / barCount;
    const startBin = Math.floor(10 ** (minLog + (maxLog - minLog) * startRatio) - 1);
    const endBin = Math.max(
      startBin + 1,
      Math.floor(10 ** (minLog + (maxLog - minLog) * endRatio) - 1),
    );
    const clampedStart = Math.max(0, Math.min(maxBin, startBin));
    const clampedEnd = Math.max(clampedStart, Math.min(maxBin, endBin));
    let total = 0;
    let samples = 0;

    for (let bin = clampedStart; bin <= clampedEnd; bin += 1) {
      total += data[bin];
      samples += 1;
    }

    return samples ? total / samples : 0;
  });
};

export const getScopePoints = (data, width, height, previous = null) => {
  if (!data?.length || width <= 0 || height <= 0) return [];

  const midpoint = height / 2;
  const bucketSize = Math.max(1, Math.floor(data.length / scopeDisplaySamples));
  const normalized = [];

  for (let start = 0; start < data.length; start += bucketSize) {
    let total = 0;
    let samples = 0;
    const end = Math.min(data.length, start + bucketSize);

    for (let index = start; index < end; index += 1) {
      total += (data[index] - 128) / 128;
      samples += 1;
    }

    normalized.push(samples ? total / samples : 0);
  }

  const peak = normalized.reduce(
    (max, value) => Math.max(max, Math.abs(value)),
    0,
  );
  const gain = peak > minScopeGainPeak
    ? Math.min(maxScopeGain, Math.max(1, scopeTargetPeak / peak))
    : 1;

  return normalized.map((value, index) => {
    const prior = previous?.[index]?.normalized ?? value;
    const smoothed = prior * 0.72 + value * 0.28;

    return {
      normalized: smoothed,
      x: (index / (normalized.length - 1 || 1)) * width,
      y: midpoint - smoothed * gain * midpoint * 0.9,
    };
  });
};

export const getScopeLinePoints = (points) =>
  points.map(({ x, y }) => ({ x, y }));

const drawFrequencyBars = (ctx, data, width, height, options = {}) => {
  const { alpha = 1, maxHeightRatio = 1, topOffset = 0 } = options;
  const barCount = Math.min(72, Math.max(18, Math.floor(width / 6)));
  const bars = getFrequencyBars(data, barCount);
  const barWidth = width / bars.length;

  bars.forEach((value, index) => {
    const barHeight = (value / 255) * height * maxHeightRatio;
    const hue = 132 - (index / bars.length) * 92;
    ctx.fillStyle = `hsla(${hue}, 74%, 52%, ${alpha})`;
    ctx.fillRect(
      index * barWidth,
      topOffset + height - barHeight,
      Math.max(1, barWidth - 1),
      barHeight,
    );
  });
};

const drawScopeLine = (ctx, points, width, height, options = {}) => {
  const {
    alpha = 1,
    baselineAlpha = 0.18,
    lineWidth = 2,
  } = options;
  const linePoints = getScopeLinePoints(points);

  ctx.strokeStyle = `rgba(139, 212, 80, ${baselineAlpha})`;
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(0, height / 2);
  ctx.lineTo(width, height / 2);
  ctx.stroke();

  ctx.shadowBlur = 8;
  ctx.shadowColor = `rgba(139, 212, 80, ${alpha * 0.45})`;
  ctx.strokeStyle = `rgba(180, 242, 116, ${alpha})`;
  ctx.lineWidth = lineWidth;
  ctx.beginPath();
  linePoints.forEach(({ x, y }, index) => {
    if (index === 0) ctx.moveTo(x, y);
    else ctx.lineTo(x, y);
  });
  ctx.stroke();
  ctx.shadowBlur = 0;
};

const SpectrumAnalyzer = ({ audioElement, className = '', mode }) => {
  const canvasRef = useRef(null);
  const rafRef = useRef(null);
  const scopeLastReadRef = useRef(0);
  const scopePointsRef = useRef(null);

  const draw = useCallback(
    (analyser, timestamp = 0) => {
      const canvas = canvasRef.current;
      if (!canvas) return;

      const rect = canvas.getBoundingClientRect();
      const width = Math.max(1, Math.floor(rect.width));
      const height = Math.max(1, Math.floor(rect.height));
      if (canvas.width !== width) canvas.width = width;
      if (canvas.height !== height) canvas.height = height;

      const ctx = canvas.getContext('2d');
      ctx.clearRect(0, 0, width, height);
      ctx.fillStyle = '#050608';
      ctx.fillRect(0, 0, width, height);

      if (mode === 'scope') {
        let points = scopePointsRef.current;
        const shouldRead = !points || !timestamp
          || timestamp - scopeLastReadRef.current >= scopeReadIntervalMs;

        if (shouldRead) {
          const data = new Uint8Array(analyser.fftSize);
          analyser.getByteTimeDomainData(data);
          points = getScopePoints(data, width, height, scopePointsRef.current);
          scopePointsRef.current = points;
          scopeLastReadRef.current = timestamp;
        }

        drawScopeLine(ctx, points, width, height);
      } else {
        const data = new Uint8Array(analyser.frequencyBinCount);
        analyser.getByteFrequencyData(data);
        drawFrequencyBars(ctx, data, width, height);
      }

      rafRef.current = window.requestAnimationFrame((nextTimestamp) =>
        draw(analyser, nextTimestamp));
    },
    [mode],
  );

  useEffect(() => {
    if (!audioElement || mode === 'off') return undefined;
    let cancelled = false;

    resumeAudioGraph(audioElement).then((graph) => {
      if (cancelled || !graph) return;
      draw(graph.analyser);
    });

    return () => {
      cancelled = true;
      if (rafRef.current) {
        window.cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
      scopeLastReadRef.current = 0;
      scopePointsRef.current = null;
    };
  }, [audioElement, draw, mode]);

  if (mode === 'off') return null;

  return (
    <canvas
      aria-label={mode === 'scope' ? 'Oscilloscope' : 'Spectrum analyzer'}
      className={['player-spectrum', className].filter(Boolean).join(' ')}
      data-testid="player-spectrum"
      ref={canvasRef}
    />
  );
};

export default SpectrumAnalyzer;
