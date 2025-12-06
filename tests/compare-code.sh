#!/bin/bash
#
# Code Comparison: Upstream slskd vs slskdn fixes
#

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

UPSTREAM="/home/keith/Documents/Code/slskd-upstream-pr"
SLSKDN="/home/keith/Documents/Code/slskdn"

echo "============================================================"
echo "CODE COMPARISON: Upstream slskd vs slskdn"
echo "============================================================"
echo

# Bug #31: async void in RoomService.cs
echo -e "${YELLOW}Bug #31: RoomService.cs - async void event handler${NC}"
echo "------------------------------------------------------------"
echo "UPSTREAM:"
grep -n "private async void Client_LoggedIn" "$UPSTREAM/src/slskd/Messaging/RoomService.cs" 2>/dev/null || echo "  (method not found or file missing)"
echo
echo "SLSKDN (check for try-catch):"
grep -A5 "private async void Client_LoggedIn" "$SLSKDN/src/slskd/Messaging/RoomService.cs" 2>/dev/null | head -10
echo

# Bug #32: transfers.js returns undefined
echo -e "${YELLOW}Bug #32: transfers.js - returns undefined vs []${NC}"
echo "------------------------------------------------------------"
echo "UPSTREAM (returns undefined):"
grep -A2 "return undefined" "$UPSTREAM/src/web/src/lib/transfers.js" 2>/dev/null || echo "  (pattern not found)"
echo
echo "SLSKDN (returns []):"
grep -A2 "return \[\]" "$SLSKDN/src/web/src/lib/transfers.js" 2>/dev/null || echo "  (pattern not found)"
echo

# Bug #33: searches.js returns undefined
echo -e "${YELLOW}Bug #33: searches.js - returns undefined vs []${NC}"
echo "------------------------------------------------------------"
echo "UPSTREAM (returns undefined):"
grep -A2 "return undefined" "$UPSTREAM/src/web/src/lib/searches.js" 2>/dev/null || echo "  (pattern not found)"
echo
echo "SLSKDN (returns []):"
grep -A2 "return \[\]" "$SLSKDN/src/web/src/lib/searches.js" 2>/dev/null || echo "  (pattern not found)"
echo

echo "============================================================"
echo "DIFF SUMMARY"
echo "============================================================"

echo -e "\n${YELLOW}transfers.js diff:${NC}"
diff -u "$UPSTREAM/src/web/src/lib/transfers.js" "$SLSKDN/src/web/src/lib/transfers.js" 2>/dev/null | grep -E "^[-+].*undefined|^[-+].*\[\]" | head -5

echo -e "\n${YELLOW}searches.js diff:${NC}"
diff -u "$UPSTREAM/src/web/src/lib/searches.js" "$SLSKDN/src/web/src/lib/searches.js" 2>/dev/null | grep -E "^[-+].*undefined|^[-+].*\[\]" | head -5

echo -e "\n${YELLOW}RoomService.cs diff (try-catch):${NC}"
diff -u "$UPSTREAM/src/slskd/Messaging/RoomService.cs" "$SLSKDN/src/slskd/Messaging/RoomService.cs" 2>/dev/null | grep -E "^[+-].*try|^[+-].*catch" | head -10

echo
echo -e "${GREEN}✓ Code comparison complete${NC}"

