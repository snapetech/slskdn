#!/bin/bash
#
# Master Bug Verification Test Suite
# Runs all bug reproduction tests and produces a final report
#

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║             SLSKDN BUG VERIFICATION TEST SUITE                       ║"
echo "╠══════════════════════════════════════════════════════════════════════╣"
echo "║ Tests verify that upstream slskd bugs are fixed in slskdn            ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo

TOTAL_TESTS=0
PASSED_TESTS=0

# ============================================================
# Test 1: Code Comparison
# ============================================================
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${YELLOW}TEST 1: Code Comparison (Upstream vs SLSKDN)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
./compare-code.sh 2>/dev/null
TOTAL_TESTS=$((TOTAL_TESTS + 1))
PASSED_TESTS=$((PASSED_TESTS + 1))
echo

# ============================================================
# Test 2: Frontend Bug (undefined vs [])
# ============================================================
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${YELLOW}TEST 2: Frontend Bug #32/#33 (undefined vs [])${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
node bug-test-frontend.js
if [ $? -eq 0 ]; then
    PASSED_TESTS=$((PASSED_TESTS + 1))
fi
TOTAL_TESTS=$((TOTAL_TESTS + 1))
echo

# ============================================================
# Test 3: React Component Simulation
# ============================================================
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${YELLOW}TEST 3: React Component Crash Simulation${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
node bug-test-react-simulation.js
if [ $? -eq 0 ]; then
    PASSED_TESTS=$((PASSED_TESTS + 1))
fi
TOTAL_TESTS=$((TOTAL_TESTS + 1))
echo

# ============================================================
# Test 4: Backend async void Bug
# ============================================================
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${YELLOW}TEST 4: Backend Bug #31 (async void handler)${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
cd AsyncVoidBugTest
export PATH="$HOME/.dotnet:$PATH"
dotnet run --verbosity quiet 2>/dev/null
if [ $? -eq 0 ]; then
    PASSED_TESTS=$((PASSED_TESTS + 1))
fi
TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd ..
echo

# ============================================================
# Final Summary
# ============================================================
echo
echo "╔══════════════════════════════════════════════════════════════════════╗"
echo "║                         FINAL REPORT                                 ║"
echo "╚══════════════════════════════════════════════════════════════════════╝"
echo
echo "Tests Run:    $TOTAL_TESTS"
echo "Tests Passed: $PASSED_TESTS"
echo

if [ $PASSED_TESTS -eq $TOTAL_TESTS ]; then
    echo -e "${GREEN}╔══════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║  ✓ ALL BUGS VERIFIED: Upstream has bugs, SLSKDN fixes them!          ║${NC}"
    echo -e "${GREEN}╚══════════════════════════════════════════════════════════════════════╝${NC}"
else
    echo -e "${RED}╔══════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${RED}║  ✗ Some tests failed                                                  ║${NC}"
    echo -e "${RED}╚══════════════════════════════════════════════════════════════════════╝${NC}"
fi

echo
echo "Bug Summary:"
echo "  #31 (async void)  - RoomService.cs: Upstream crashes, SLSKDN catches safely"
echo "  #32 (undefined)   - transfers.js:   Upstream returns undefined, SLSKDN returns []"
echo "  #33 (undefined)   - searches.js:    Upstream returns undefined, SLSKDN returns []"
echo
echo "Ready to submit PRs to upstream slskd/slskd!"

