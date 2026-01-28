# E2E Test Results After CORS Fix

## ✅ CORS Fix Successful

The CORS configuration fix has **resolved the "Failed to fetch" errors**. Tests can now make cross-origin requests between nodes.

### Changes Made
1. **Enabled CORS in E2E node config** (`SlskdnNode.ts`)
2. **Added wildcard origin support** in backend (`Program.cs`)

## Current Test Status

### ✅ Fixed
- **CORS errors**: No more "Access-Control-Allow-Origin" blocking errors
- **Cross-origin streaming**: Tests can now fetch streams from different node origins

### ⚠️ Remaining Issues

#### 1. `concurrency_limit_blocks_excess_streams` - Concurrency Limiter Not Working
**Status**: Failing  
**Error**: Expected 429 (Too Many Requests) but got 206 (Partial Content)

**Issue**: The concurrency limiter should block the second request when `maxConcurrentStreams: 1`, but it's allowing it through.

**Possible Causes**:
- First stream completes/disposes before second request is made
- Limiter key mismatch between requests
- Timing issue - second request happens before first stream is fully established

**Test Code**:
```typescript
// First request: starts stream in browser context (kept open)
const firstStatus = await pageB.evaluate(async (url) => {
  const response = await fetch(url, { headers: { Range: 'bytes=0-' } });
  // ... keeps stream open
  return response.status;
}, streamUrl);

// Second request: should be blocked (429)
const secondResponse = await request.get(streamUrl, {
  headers: { Range: 'bytes=0-1' },
});
expect(secondResponse.status()).toBe(429); // ❌ Getting 206 instead
```

**Next Steps**:
- Verify the first stream is actually held open
- Check if limiter key is consistent between requests
- Add delay between requests to ensure first stream is established
- Verify `ReleaseOnDisposeStream` is working correctly

#### 2. `recipient_streams_video` - Share Row Not Found
**Status**: Failing  
**Error**: Expected `streamRowFound` to be `true` but got `false`

**Issue**: The test can't find the incoming share row in the UI after waiting 20 seconds.

**Possible Causes**:
- Share announcement not propagating to recipient node
- Share discovery timing issue
- UI not updating after share is available
- Test selector mismatch

**Test Code**:
```typescript
let streamRowFound = false;
for (let index = 0; index < 20; index++) {
  const row = pageC.getByTestId(`incoming-share-row-${collectionTitle}`).first();
  if ((await row.count()) > 0) {
    streamRowFound = true;
    break;
  }
  await pageC.waitForTimeout(1_000);
}
expect(streamRowFound).toBe(true); // ❌ Getting false
```

**Next Steps**:
- Verify `announceShareGrant` is being called
- Check if share is actually available via API before checking UI
- Increase timeout or add API-based verification
- Check if collection title matches exactly

## Summary

**CORS Fix**: ✅ **SUCCESS** - Cross-origin requests now work  
**Concurrency Test**: ⚠️ Needs investigation - limiter not blocking  
**Share Discovery Test**: ⚠️ Needs investigation - share not appearing in UI

The CORS fix was the main blocker. The remaining issues are functional test problems, not infrastructure issues.
