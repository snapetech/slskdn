// <copyright file="mediacore.test.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import api from './api';
import * as mediacore from './mediacore';

vi.mock('./api', () => ({
  __esModule: true,
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('mediacore', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('uses the relative content-id stats path', async () => {
    api.get.mockResolvedValue({ data: { totalMappings: 0 } });

    await mediacore.getContentIdStats();

    expect(api.get).toHaveBeenCalledWith('/mediacore/contentid/stats');
  });

  it('uses the relative content-id resolve path', async () => {
    api.get.mockResolvedValue({ data: { contentId: 'cid-123' } });

    await mediacore.resolveContentId('artist:1');

    expect(api.get).toHaveBeenCalledWith(
      '/mediacore/contentid/resolve/artist%3A1',
    );
  });
});
