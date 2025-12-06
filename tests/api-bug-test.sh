#!/bin/bash
#
# API Bug Test: Tests bug behavior via actual API calls
#
# This script tests the undefined vs [] behavior by making
# API calls that could return unexpected data.
#

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

SLSKDN_PORT="${SLSKDN_PORT:-5099}"
SLSKD_PORT="${SLSKD_PORT:-5030}"

echo "============================================================"
echo "API BUG TEST"
echo "============================================================"
echo -e "SLSKDN API: http://localhost:${SLSKDN_PORT}"
echo -e "SLSKD API:  http://localhost:${SLSKD_PORT} (if running)"
echo "============================================================"
echo

# Function to test API endpoint
test_api() {
    local name="$1"
    local url="$2"
    local expected_behavior="$3"
    
    echo -e "${CYAN}Testing: ${name}${NC}"
    echo "  URL: $url"
    
    response=$(curl -s -w "\n%{http_code}" "$url" 2>/dev/null)
    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | sed '$d')
    
    if [ "$http_code" = "000" ]; then
        echo -e "  ${YELLOW}⚠ Server not reachable${NC}"
        return 2
    elif [ "$http_code" = "401" ]; then
        echo -e "  ${YELLOW}⚠ Unauthorized (need auth token)${NC}"
        return 2
    elif [ "$http_code" = "404" ]; then
        echo -e "  ${YELLOW}⚠ Not found (invalid endpoint for test)${NC}"
        # A 404 with valid JSON that's not an array would trigger the bug
        if echo "$body" | grep -q "^\["; then
            echo -e "  ${GREEN}✓ Returns array${NC}"
        elif echo "$body" | grep -q "^{"; then
            echo -e "  ${YELLOW}⚠ Returns object (would trigger bug in upstream)${NC}"
        fi
        return 0
    elif [ "$http_code" = "200" ]; then
        if echo "$body" | grep -q "^\["; then
            echo -e "  ${GREEN}✓ Returns array (normal case)${NC}"
        else
            echo -e "  ${YELLOW}⚠ Returns non-array: $body${NC}"
        fi
        return 0
    else
        echo -e "  ${RED}✗ HTTP $http_code${NC}"
        return 1
    fi
}

echo -e "${YELLOW}=== Testing SLSKDN (port $SLSKDN_PORT) ===${NC}"
echo

# Test transfers endpoint
test_api "Transfers/Downloads" "http://localhost:${SLSKDN_PORT}/api/v0/transfers/downloads"
test_api "Transfers/Uploads" "http://localhost:${SLSKDN_PORT}/api/v0/transfers/uploads"

# Test with invalid search ID (triggers non-array response)
test_api "Search Responses (invalid ID)" "http://localhost:${SLSKDN_PORT}/api/v0/searches/invalid-id/responses"

echo
echo -e "${YELLOW}=== Testing upstream SLSKD (port $SLSKD_PORT) if available ===${NC}"
echo

# Check if upstream is running
if curl -s "http://localhost:${SLSKD_PORT}/api/v0/application" > /dev/null 2>&1; then
    test_api "Transfers/Downloads" "http://localhost:${SLSKD_PORT}/api/v0/transfers/downloads"
    test_api "Search Responses (invalid ID)" "http://localhost:${SLSKD_PORT}/api/v0/searches/invalid-id/responses"
else
    echo -e "${YELLOW}⚠ Upstream SLSKD not running on port ${SLSKD_PORT}${NC}"
    echo "  To test upstream, run slskd on port ${SLSKD_PORT}"
fi

echo
echo "============================================================"
echo "BUG EXPLANATION"
echo "============================================================"
echo "When the API returns a non-array (like an error object or 404):"
echo
echo "UPSTREAM (vulnerable):"
echo "  - transfers.js/searches.js return 'undefined'"
echo "  - Calling code does: result.map(...) or result.length"
echo "  - This crashes: Cannot read property 'map' of undefined"
echo
echo "SLSKDN (fixed):"
echo "  - Returns empty array [] instead of undefined"
echo "  - Calling code safely iterates empty array"
echo "  - No crash, graceful handling"
echo
echo -e "${GREEN}✓ Test complete${NC}"

