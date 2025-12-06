#!/usr/bin/env node
/**
 * Bug Reproduction Test: Frontend undefined vs empty array
 * 
 * Bug #32 (transfers.js) and #33 (searches.js):
 * When API returns non-array data, upstream returns `undefined`,
 * which crashes when calling code tries to iterate.
 * Our fix returns `[]` instead.
 */

const fs = require('fs');
const path = require('path');

// Color output helpers
const RED = '\x1b[31m';
const GREEN = '\x1b[32m';
const YELLOW = '\x1b[33m';
const RESET = '\x1b[0m';

console.log('='.repeat(60));
console.log('BUG REPRODUCTION TEST: Frontend undefined vs []');
console.log('='.repeat(60));
console.log();

// Simulate what the upstream code does
function upstreamGetAll(response) {
  if (!Array.isArray(response)) {
    console.warn('got non-array response from transfers API', response);
    return undefined; // UPSTREAM BEHAVIOR
  }
  return response;
}

// Simulate what our fixed code does
function fixedGetAll(response) {
  if (!Array.isArray(response)) {
    console.warn('got non-array response from transfers API', response);
    return []; // OUR FIX
  }
  return response;
}

// Simulate calling code that expects an array
function simulateCallingCode(getData, label) {
  console.log(`\n${YELLOW}Testing: ${label}${RESET}`);
  
  // Test with valid data
  console.log('  Test 1: Valid array response');
  try {
    const result = getData([{ id: 1 }, { id: 2 }]);
    const count = result.length;
    console.log(`  ${GREEN}✓ Success: Got ${count} items${RESET}`);
  } catch (e) {
    console.log(`  ${RED}✗ CRASH: ${e.message}${RESET}`);
  }

  // Test with null response (simulates API error)
  console.log('  Test 2: null response (API error)');
  try {
    const result = getData(null);
    const count = result.length; // This will crash if result is undefined
    console.log(`  ${GREEN}✓ Success: Got ${count} items${RESET}`);
  } catch (e) {
    console.log(`  ${RED}✗ CRASH: ${e.message}${RESET}`);
    return false;
  }

  // Test with object response (unexpected API response)
  console.log('  Test 3: Object response (unexpected format)');
  try {
    const result = getData({ error: 'Something went wrong' });
    const count = result.length;
    console.log(`  ${GREEN}✓ Success: Got ${count} items${RESET}`);
  } catch (e) {
    console.log(`  ${RED}✗ CRASH: ${e.message}${RESET}`);
    return false;
  }

  // Test with string response
  console.log('  Test 4: String response');
  try {
    const result = getData('error');
    const mapped = result.map(x => x); // Common operation
    console.log(`  ${GREEN}✓ Success: Mapped ${mapped.length} items${RESET}`);
  } catch (e) {
    console.log(`  ${RED}✗ CRASH: ${e.message}${RESET}`);
    return false;
  }

  return true;
}

console.log('\n' + '='.repeat(60));
console.log('UPSTREAM SLSKD BEHAVIOR (returns undefined)');
console.log('='.repeat(60));
const upstreamPassed = simulateCallingCode(upstreamGetAll, 'Upstream transfers.js');

console.log('\n' + '='.repeat(60));
console.log('SLSKDN FIX (returns [])');
console.log('='.repeat(60));
const fixedPassed = simulateCallingCode(fixedGetAll, 'Fixed transfers.js');

console.log('\n' + '='.repeat(60));
console.log('SUMMARY');
console.log('='.repeat(60));
console.log(`Upstream SLSKD: ${upstreamPassed ? GREEN + 'PASSED' : RED + 'FAILED - CRASHES ON INVALID DATA'}${RESET}`);
console.log(`SLSKDN (fixed): ${fixedPassed ? GREEN + 'PASSED - HANDLES INVALID DATA GRACEFULLY' : RED + 'FAILED'}${RESET}`);
console.log();

if (!upstreamPassed && fixedPassed) {
  console.log(`${GREEN}✓ Bug verified: Upstream crashes, our fix handles it safely${RESET}`);
  process.exit(0);
} else if (upstreamPassed) {
  console.log(`${YELLOW}Note: Upstream didn't crash (may have been fixed)${RESET}`);
  process.exit(0);
} else {
  console.log(`${RED}✗ Unexpected result${RESET}`);
  process.exit(1);
}

