# E2E Test Implementation

This directory contains Playwright-based end-to-end tests for slskdn.

## Quick Start

```bash
# Install dependencies
npm install

# Install Playwright browsers
npx playwright install chromium

# Run all tests
npm test

# Run specific test
npx playwright test specs/smoke.spec.ts
```

## Project Structure

- `playwright.config.ts`: Playwright configuration
- `fixtures/`: Shared test utilities
- `specs/`: Test specifications
- `harness/`: Node launcher and multi-peer management

## Test Categories

Tests are organized by user journey:

- `smoke.spec.ts`: Basic functionality (login, page loads)
- `library.spec.ts`: Library indexing and browsing
- `search.spec.ts`: Search functionality
- `sharing.spec.ts`: Contacts, groups, collections, sharing
- `streaming.spec.ts`: Streaming and playback
- `backfill.spec.ts`: Download and backfill
- `policy.spec.ts`: Permissions and policy enforcement

## Writing Tests

### Basic Test Structure

```typescript
import { test, expect } from '@playwright/test';
import { MultiPeerHarness } from '../harness/MultiPeerHarness';

test.describe('Feature Name', () => {
  let harness: MultiPeerHarness;
  
  test.beforeAll(async () => {
    harness = new MultiPeerHarness();
    await harness.startNode('alice', 'test-data/slskdn-test-fixtures/music');
  });
  
  test.afterAll(async () => {
    await harness.stopAll();
  });
  
  test('should do something', async ({ page }) => {
    const alice = harness.getNode('alice');
    await page.goto(`${alice.apiUrl}/`);
    // ... test steps
  });
});
```

### Using Helpers

```typescript
import { login, waitForHealth } from '../fixtures/helpers';
import { selectors } from '../fixtures/selectors';

test('should login', async ({ page }) => {
  const node = harness.getNode('alice');
  await login(page, node.apiUrl, 'admin', 'admin');
  await expect(page.locator(selectors.nav.contacts)).toBeVisible();
});
```

## Configuration

### Environment Variables

- `SLSKDN_TEST_NO_CONNECT`: Set to `true` to disable Soulseek (CI determinism)
- `SLSKDN_TEST_KEEP_ARTIFACTS`: Set to `1` to keep temp directories after tests
- `SLSKDN_TEST_TIMEOUT`: Test timeout in seconds (default: 300)

### Playwright Configuration

See `playwright.config.ts` for:
- Browser selection (Chromium by default)
- Timeout settings
- Screenshot/video on failure
- Test retries

## CI Integration

E2E tests run in GitHub Actions. See `.github/workflows/e2e-tests.yml`.

Tests run with:
- `SLSKDN_TEST_NO_CONNECT=true` (deterministic)
- Artifact upload on failure
- Parallel execution (if sharding enabled)

## Debugging

### Run in Headed Mode

```bash
npx playwright test --headed
```

### Debug Mode

```bash
DEBUG=pw:api npx playwright test
```

### Keep Artifacts

```bash
SLSKDN_TEST_KEEP_ARTIFACTS=1 npm test
# Check /tmp/slskdn-test-*/ for logs and data
```

### Single Test Debugging

```bash
npx playwright test -g "test name" --debug
```

## Adding data-testid Attributes

To make tests more reliable, add `data-testid` attributes to UI components:

```jsx
// Example: Contacts.jsx
<Button data-testid="contacts-create-invite" onClick={...}>
  Create Invite
</Button>
```

Then reference in `fixtures/selectors.ts`:

```typescript
export const selectors = {
  contacts: {
    createInvite: '[data-testid="contacts-create-invite"]'
  }
};
```
