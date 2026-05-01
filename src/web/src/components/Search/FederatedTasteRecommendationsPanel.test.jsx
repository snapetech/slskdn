// <copyright file="FederatedTasteRecommendationsPanel.test.jsx" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import FederatedTasteRecommendationsPanel from './FederatedTasteRecommendationsPanel';
import {
  fetchTasteRecommendations,
  previewTasteRecommendationGraph,
  promoteTasteRecommendationToWishlist,
  subscribeTasteRecommendationReleaseRadar,
} from '../../lib/tasteRecommendations';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { vi } from 'vitest';

vi.mock('../../lib/tasteRecommendations', () => ({
  fetchTasteRecommendations: vi.fn(),
  previewTasteRecommendationGraph: vi.fn(),
  promoteTasteRecommendationToWishlist: vi.fn(),
  subscribeTasteRecommendationReleaseRadar: vi.fn(),
}));

const recommendation = {
  reasons: ['trusted overlap'],
  score: 0.82,
  sourceActors: ['pod-a'],
  trustedSourceCount: 2,
  workRef: {
    creator: 'Taste Artist',
    id: 'work-1',
    title: 'Taste Track',
  },
};

describe('FederatedTasteRecommendationsPanel', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    fetchTasteRecommendations.mockResolvedValue({
      data: {
        candidateCount: 1,
        minimumTrustedSources: 2,
        recommendations: [recommendation],
        trustedActorCount: 3,
      },
    });
    previewTasteRecommendationGraph.mockResolvedValue({
      data: {
        edgeCount: 3,
        nodeCount: 4,
      },
    });
    promoteTasteRecommendationToWishlist.mockResolvedValue({
      data: {
        message: 'Promoted to Wishlist.',
      },
    });
    subscribeTasteRecommendationReleaseRadar.mockResolvedValue({
      data: {
        message: 'Subscribed to Release Radar.',
      },
    });
  });

  it('loads privacy-filtered recommendations and direct handoff actions', async () => {
    render(<FederatedTasteRecommendationsPanel />);

    fireEvent.click(screen.getByRole('button', { name: 'Load Recommendations' }));

    expect(await screen.findByText('Taste Artist - Taste Track')).toBeInTheDocument();
    expect(screen.getByText('trusted overlap')).toBeInTheDocument();
    expect(
      screen.getByRole('button', {
        name: 'Promote Taste Track taste recommendation to Wishlist',
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', {
        name: 'Subscribe Taste Track taste recommendation to Release Radar',
      }),
    ).toBeInTheDocument();
  });

  it('promotes recommendations to wishlist, release radar, and graph preview handoffs', async () => {
    render(<FederatedTasteRecommendationsPanel />);

    fireEvent.click(screen.getByRole('button', { name: 'Load Recommendations' }));

    expect(await screen.findByText('Taste Artist - Taste Track')).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Promote Taste Track taste recommendation to Wishlist',
      }),
    );
    await waitFor(() =>
      expect(promoteTasteRecommendationToWishlist).toHaveBeenCalledWith(
        expect.objectContaining({
          workRef: recommendation.workRef,
        }),
      ),
    );

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Subscribe Taste Track taste recommendation to Release Radar',
      }),
    );
    await waitFor(() =>
      expect(subscribeTasteRecommendationReleaseRadar).toHaveBeenCalledWith(
        expect.objectContaining({
          scope: 'trusted',
          workRef: recommendation.workRef,
        }),
      ),
    );

    fireEvent.click(
      screen.getByRole('button', {
        name: 'Preview Taste Track taste recommendation graph',
      }),
    );
    await waitFor(() =>
      expect(previewTasteRecommendationGraph).toHaveBeenCalledWith({
        workRef: recommendation.workRef,
      }),
    );
    expect(screen.getByText('Graph preview: 4 nodes, 3 edges.')).toBeInTheDocument();
  });

  it('keeps source actors hidden unless explicitly revealed', async () => {
    render(<FederatedTasteRecommendationsPanel />);

    fireEvent.click(screen.getByRole('button', { name: 'Load Recommendations' }));

    expect(await screen.findByText('Taste Artist - Taste Track')).toBeInTheDocument();
    expect(screen.queryByText('pod-a')).not.toBeInTheDocument();

    fireEvent.click(
      screen.getByLabelText('Reveal federated recommendation source actors'),
    );
    fireEvent.click(screen.getByRole('button', { name: 'Load Recommendations' }));

    await waitFor(() =>
      expect(fetchTasteRecommendations).toHaveBeenLastCalledWith(
        expect.objectContaining({
          includeSourceActors: true,
        }),
      ),
    );
    expect(screen.getByText('pod-a')).toBeInTheDocument();
  });
});
