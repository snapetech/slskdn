#!/usr/bin/env node
/**
 * Bug Reproduction: Simulates how React components use the API
 * 
 * This simulates the actual React component behavior from:
 * - Transfers.jsx (uses transfers.js getAll)
 * - SearchDetail.jsx (uses searches.js getResponses)
 */

const RED = '\x1b[31m';
const GREEN = '\x1b[32m';
const YELLOW = '\x1b[33m';
const CYAN = '\x1b[36m';
const RESET = '\x1b[0m';

console.log('='.repeat(70));
console.log('BUG REPRODUCTION: React Component Simulation');
console.log('='.repeat(70));
console.log();

// ============================================================
// Simulated API responses (what the backend might return)
// ============================================================
const apiResponses = {
  normal: [{ id: 1, filename: 'song.mp3' }, { id: 2, filename: 'track.flac' }],
  empty: [],
  serverError: { error: 'Internal Server Error', status: 500 },
  notFound: { error: 'Not Found', status: 404 },
  nullResponse: null,
  undefinedResponse: undefined,
  htmlError: '<!DOCTYPE html><html><body>502 Bad Gateway</body></html>',
};

// ============================================================
// UPSTREAM CODE (returns undefined)
// ============================================================
function upstreamGetAll(apiResponse) {
  if (!Array.isArray(apiResponse)) {
    console.warn('  [upstream] got non-array response');
    return undefined; // BUG: This will crash calling code
  }
  return apiResponse;
}

// ============================================================
// SLSKDN FIXED CODE (returns [])
// ============================================================
function slskdnGetAll(apiResponse) {
  if (!Array.isArray(apiResponse)) {
    console.warn('  [slskdn] got non-array response, returning []');
    return []; // FIX: Safe empty array
  }
  return apiResponse;
}

// ============================================================
// Simulated React Component (Transfers.jsx style)
// ============================================================
function simulateTransfersComponent(getAll, label) {
  console.log(`\n${CYAN}=== ${label} ===${RESET}`);
  
  const results = [];
  
  for (const [name, apiResponse] of Object.entries(apiResponses)) {
    console.log(`\n  ${YELLOW}Scenario: ${name}${RESET}`);
    
    try {
      // This simulates what Transfers.jsx does:
      const transfers = getAll(apiResponse);
      
      // Real component code does things like:
      // 1. transfers.map(t => <TransferRow key={t.id} />)
      // 2. transfers.filter(t => t.state === 'completed')
      // 3. transfers.length to show count
      
      // Test .map() - most common operation
      const mapped = transfers.map(t => t.id);
      console.log(`    ✓ .map() succeeded: ${mapped.length} items`);
      
      // Test .filter()
      const filtered = transfers.filter(t => t.id > 0);
      console.log(`    ✓ .filter() succeeded: ${filtered.length} matches`);
      
      // Test .length
      console.log(`    ✓ .length = ${transfers.length}`);
      
      results.push({ scenario: name, status: 'PASS' });
      
    } catch (error) {
      console.log(`    ${RED}✗ CRASH: ${error.message}${RESET}`);
      results.push({ scenario: name, status: 'CRASH', error: error.message });
    }
  }
  
  return results;
}

// ============================================================
// Run tests
// ============================================================

console.log(`${YELLOW}Testing UPSTREAM slskd behavior (returns undefined):${RESET}`);
const upstreamResults = simulateTransfersComponent(upstreamGetAll, 'UPSTREAM SLSKD');

console.log(`\n${'='.repeat(70)}`);

console.log(`\n${YELLOW}Testing SLSKDN fixed behavior (returns []):${RESET}`);
const slskdnResults = simulateTransfersComponent(slskdnGetAll, 'SLSKDN (FIXED)');

// ============================================================
// Summary
// ============================================================
console.log(`\n${'='.repeat(70)}`);
console.log('SUMMARY');
console.log('='.repeat(70));

const upstreamCrashes = upstreamResults.filter(r => r.status === 'CRASH').length;
const upstreamPasses = upstreamResults.filter(r => r.status === 'PASS').length;
const slskdnCrashes = slskdnResults.filter(r => r.status === 'CRASH').length;
const slskdnPasses = slskdnResults.filter(r => r.status === 'PASS').length;

console.log(`\n${YELLOW}UPSTREAM SLSKD:${RESET}`);
console.log(`  Passed: ${upstreamPasses}/${upstreamResults.length}`);
console.log(`  ${RED}Crashed: ${upstreamCrashes}/${upstreamResults.length}${RESET}`);
if (upstreamCrashes > 0) {
  console.log(`  Crash scenarios:`);
  upstreamResults.filter(r => r.status === 'CRASH').forEach(r => {
    console.log(`    - ${r.scenario}: ${r.error}`);
  });
}

console.log(`\n${YELLOW}SLSKDN (FIXED):${RESET}`);
console.log(`  ${GREEN}Passed: ${slskdnPasses}/${slskdnResults.length}${RESET}`);
console.log(`  Crashed: ${slskdnCrashes}/${slskdnResults.length}`);

console.log(`\n${'='.repeat(70)}`);
if (upstreamCrashes > 0 && slskdnCrashes === 0) {
  console.log(`${GREEN}✓ BUG VERIFIED: Upstream crashes on ${upstreamCrashes} scenarios, SLSKDN handles all safely${RESET}`);
} else if (upstreamCrashes === 0) {
  console.log(`${YELLOW}⚠ Upstream didn't crash (may have been fixed upstream)${RESET}`);
} else {
  console.log(`${RED}✗ Unexpected: Both crashed${RESET}`);
}
console.log('='.repeat(70));

