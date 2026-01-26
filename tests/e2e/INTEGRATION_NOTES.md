# E2E Test Integration Notes

## Integration with Today's Fixes (2026-01-26)

### API Versioning

All Sharing controllers now use API versioning:
- `ShareGroupsController`: `[Route("api/v{version:apiVersion}/sharegroups")]` with `[ApiVersion("0")]`
- `CollectionsController`: `[Route("api/v{version:apiVersion}/collections")]` with `[ApiVersion("0")]`
- `SharesController`: `[Route("api/v{version:apiVersion}/shares")]` with `[ApiVersion("0")]`

**Impact on E2E tests**: Tests should use `/api/v0/...` paths (which is what `apiBaseUrl` provides). The frontend already uses relative paths like `/sharegroups`, which get combined with `apiBaseUrl = ${rootUrl}/api/v0` to produce the correct versioned URL.

### Frontend API Routes

Fixed double-prefixing issue:
- `identity.js` and `collections.js` now use relative paths (e.g., `/profile/invite`, `/sharegroups`)
- `apiBaseUrl` in `config.js` already includes `/api/v0`
- Result: Correct URLs like `http://localhost:5030/api/v0/sharegroups`

**Impact on E2E tests**: Tests should verify that API calls use correct paths. The `buildApiUrl()` guard function in `api.js` will throw in development if paths include `/api` prefix.

### Error Handling

Improved error messages:
- 404 with `/api/v0/api/v0` pattern shows route mismatch warning
- Empty body responses are logged
- Better status code mapping (401/403/404/500)

**Impact on E2E tests**: Tests can verify error messages are helpful and specific.

## Test Data Requirements

E2E tests use fixtures from `test-data/slskdn-test-fixtures/`:
- `music/`: Audio files for library indexing tests
- `book/`: Text files for sharing tests
- `tv/`: Video/image files for streaming tests

Ensure these directories exist and contain test data before running E2E tests.

## Adding data-testid Attributes

To make E2E tests more reliable, add `data-testid` attributes to UI components:

```jsx
// Example: ShareGroups.jsx
<Button 
  data-testid="sharegroups-create-group"
  primary 
  onClick={() => this.setState({ createModalOpen: true })}
>
  <Icon name="plus" />
  Create Group
</Button>
```

Then reference in `fixtures/selectors.ts`:

```typescript
export const selectors = {
  shareGroups: {
    createGroup: '[data-testid="sharegroups-create-group"]'
  }
};
```

## Known Issues

1. **Login form selectors**: The actual login form may use different IDs. Update `selectors.login` based on actual UI.
2. **Navigation selectors**: Update `selectors.nav` based on actual navigation structure.
3. **Modal selectors**: Share group creation modal may need additional selectors.

## Next Steps

1. Add `data-testid` attributes to all interactive UI elements
2. Update selectors in `fixtures/selectors.ts` to match actual UI
3. Expand test coverage to all user journeys
4. Add CI job to run E2E tests on PRs
