import '@testing-library/jest-dom';
import LoginForm, { getHttpsHintUrl } from './LoginForm';
import React from 'react';
import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';

vi.mock('./Shared/Footer', () => ({ default: () => <div>Footer</div> }));
vi.mock('./Shared/Logo', () => ({ default: ['LOGO'] }));

describe('LoginForm', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('builds an HTTPS hint when the page is loaded over HTTP', () => {
    expect(getHttpsHintUrl(new URL('http://kspls0:5030/'))).toBe(
      'https://kspls0:5031',
    );
  });

  it('returns no HTTPS hint when the page is already loaded over HTTPS', () => {
    expect(getHttpsHintUrl(new URL('https://kspls0:5031/'))).toBeNull();
  });

  it('renders the HTTPS hint in the login form when served over HTTP', () => {
    window.history.pushState({}, '', '/');

    render(
      <LoginForm
        loading={false}
        onLoginAttempt={vi.fn()}
      />,
    );

    const link = screen.getByRole('link', { name: 'https://localhost:5031' });
    expect(link).toHaveAttribute('href', 'https://localhost:5031');
    expect(screen.getByText('HTTPS Option')).toBeInTheDocument();
  });
});
