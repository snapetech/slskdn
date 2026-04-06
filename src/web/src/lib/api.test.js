// <copyright file="api.test.js" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

import { getCsrfTokenFromCookieString } from './api';

describe('api csrf token selection', () => {
  it('prefers the current port scoped csrf token', () => {
    const token = getCsrfTokenFromCookieString(
      'XSRF-TOKEN-5031=https-token; XSRF-TOKEN-5030=http-token',
      '5030',
    );

    expect(token).toBe('http-token');
  });

  it('falls back to the legacy csrf token name', () => {
    const token = getCsrfTokenFromCookieString('XSRF-TOKEN=legacy-token', '');

    expect(token).toBe('legacy-token');
  });

  it('ignores the antiforgery cookie token name', () => {
    const token = getCsrfTokenFromCookieString(
      'XSRF-COOKIE-5030=cookie-token; XSRF-TOKEN-5030=request-token',
      '5030',
    );

    expect(token).toBe('request-token');
  });
});
