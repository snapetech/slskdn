// <copyright file="security.test.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import api from './api';
import * as security from './security';

vi.mock('./api', () => ({
  __esModule: true,
  default: {
    get: vi.fn(),
    put: vi.fn(),
  },
}));

describe('security', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('uses the relative security dashboard path', async () => {
    api.get.mockResolvedValue({ data: { ok: true } });

    await security.getDashboard();

    expect(api.get).toHaveBeenCalledWith('/security/dashboard');
  });

  it('uses the relative reputation path without double-prefixing', async () => {
    api.get.mockResolvedValue({ data: { score: 10 } });

    await security.getReputation('alice');

    expect(api.get).toHaveBeenCalledWith('/security/reputation/alice');
  });
});
