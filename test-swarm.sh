#!/bin/bash
# Interactive Swarm Test - Menu-driven file browser + background discovery
# Requires: SLSK_USER, SLSK_PASS env vars

set -e

LOG="/tmp/slskd.log"
PORT_FILE="/tmp/slskd-port.txt"
DISCOVERY_ARTISTS="/tmp/slskd-discovery-artists.txt"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

[ -z "$SLSK_USER" ] || [ -z "$SLSK_PASS" ] && {
  echo -e "${RED}Set SLSK_USER and SLSK_PASS first${NC}"
  echo "  export SLSK_USER=your_username"
  echo "  export SLSK_PASS=your_password"
  exit 1
}

cleanup() {
  [ -n "$DISCOVERY_PID" ] && kill $DISCOVERY_PID 2>/dev/null
  [ -n "$LOG_PID" ] && kill $LOG_PID 2>/dev/null
}
trap cleanup EXIT

get_port() { cat "$PORT_FILE" 2>/dev/null; }
get_token() {
  local port=$(get_port)
  curl -s -X POST "http://localhost:$port/api/v0/session" \
    -H "Content-Type: application/json" \
    -d '{"username":"slskd","password":"slskd"}' 2>/dev/null | jq -r '.token // empty'
}

api() {
  local method="$1" endpoint="$2" data="$3"
  local port=$(get_port) token=$(get_token)
  [ -z "$token" ] && return 1
  if [ "$method" = "GET" ]; then
    curl -s "http://localhost:$port/api/v0/$endpoint" -H "Authorization: Bearer $token"
  else
    curl -s -X POST "http://localhost:$port/api/v0/$endpoint" \
      -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d "$data"
  fi
}

start_server() {
  echo -e "${YELLOW}Starting server...${NC}"
  pkill -9 -f "dotnet.*slskd" 2>/dev/null || true; sleep 1
  for attempt in 1 2 3; do
    HTTP=$((30000 + RANDOM % 25000)); SLSK=$((30000 + RANDOM % 25000))
    cd /home/keith/Documents/Code/slskdn/src/slskd
    SLSKD_SLSK_USERNAME="$SLSK_USER" SLSKD_SLSK_PASSWORD="$SLSK_PASS" \
      SLSKD_HTTP_PORT=$HTTP SLSKD_SLSK_LISTEN_PORT=$SLSK SLSKD_NO_HTTPS=true \
      nohup dotnet run --configuration Release > "$LOG" 2>&1 &
    echo $HTTP > "$PORT_FILE"
    for i in {1..15}; do
      sleep 2
      grep -q "Connected to the Soulseek" "$LOG" 2>/dev/null && {
        echo -e "${GREEN}Server ready on port $HTTP${NC}"
        return 0
      }
      grep -qE "Exception|failed" "$LOG" 2>/dev/null && break
      echo -n "."
    done
    pkill -9 -f "dotnet.*slskd" 2>/dev/null; sleep 1
  done
  echo -e "${RED}Failed to start server${NC}"
  return 1
}

# Background discovery - rotates through artists
background_discovery() {
  while true; do
    [ -f "$DISCOVERY_ARTISTS" ] || { sleep 5; continue; }
    while read -r artist; do
      [ -z "$artist" ] && continue
      api POST "discovery/start" "{\"searchTerm\": \"$artist\"}" > /dev/null 2>&1
      sleep 270  # 4.5 minutes per artist
    done < "$DISCOVERY_ARTISTS"
  done
}

add_artist() {
  echo -ne "${CYAN}Enter artist name: ${NC}"
  read -r artist
  [ -z "$artist" ] && return
  echo "$artist" >> "$DISCOVERY_ARTISTS"
  sort -u "$DISCOVERY_ARTISTS" -o "$DISCOVERY_ARTISTS"
  echo -e "${GREEN}Added '$artist' to discovery pool${NC}"
  api POST "discovery/start" "{\"searchTerm\": \"$artist\"}" | jq -r '.message // "Started"'
}

# Get unique artists from discovery DB
get_artists() {
  api GET "discovery/sources/by-filename?pattern=.flac&limit=10000" 2>/dev/null | \
    jq -r '.sources[].filename' | \
    sed 's/\\/\//g' | \
    awk -F'/' '{for(i=1;i<=NF;i++) if(tolower($i) ~ /^[a-z]/ && length($i)>2) {print $i; break}}' | \
    sort | uniq -c | sort -rn | head -50
}

# Get albums for an artist
get_albums() {
  local artist="$1"
  api GET "discovery/sources/by-filename?pattern=$artist&limit=5000" 2>/dev/null | \
    jq -r '.sources[].filename' | \
    sed 's/\\/\//g' | grep -i "$artist" | \
    awk -F'/' '{for(i=1;i<=NF;i++) if($i ~ /\[.*\]|\(.*\)|[0-9]{4}/) {print $i; break}}' | \
    sort | uniq -c | sort -rn | head -30
}

# Get tracks with size counts for an album
get_tracks() {
  local pattern="$1"
  api GET "discovery/summaries?minSources=2" 2>/dev/null | \
    jq -r '.summaries[] | select(.sampleFilename | test("'"$pattern"'"; "i")) | "\(.sourceCount)|\(.size)|\(.sampleFilename)"' | \
    sort -t'|' -k1 -rn | head -30
}

# Main menu - browse artists
menu_artists() {
  clear
  echo -e "${BOLD}${CYAN}╔════════════════════════════════════════════════╗${NC}"
  echo -e "${BOLD}${CYAN}║         SWARM TEST - Select Artist             ║${NC}"
  echo -e "${BOLD}${CYAN}╚════════════════════════════════════════════════╝${NC}"
  echo -e "${YELLOW}Press / to add artist, q to quit${NC}"
  echo ""
  
  # Get stats
  local stats=$(api GET "discovery" 2>/dev/null)
  local files=$(echo "$stats" | jq -r '.stats.totalFiles // 0')
  local users=$(echo "$stats" | jq -r '.stats.totalUsers // 0')
  local flagged=$(api GET "discovery/no-partial-count" 2>/dev/null | jq -r '.usersWithoutPartialSupport // 0')
  echo -e "  ${GREEN}Pool: $files files from $users users | $flagged flagged${NC}"
  echo ""
  
  # Get top file sizes as proxy for "artists"
  echo -e "${BOLD}Top files by source count:${NC}"
  echo ""
  
  local i=1
  api GET "discovery/summaries?minSources=3" 2>/dev/null | \
    jq -r '.summaries[] | select(.size > 10000000) | "\(.sourceCount)|\(.size)|\(.sampleFilename)"' | \
    head -20 | while IFS='|' read -r count size filename; do
      local name=$(basename "$filename" | sed 's/\.[^.]*$//')
      local mb=$((size / 1000000))
      printf "  ${CYAN}%2d)${NC} [%2d sources] %3dMB  %s\n" "$i" "$count" "$mb" "${name:0:50}"
      ((i++))
    done
  
  echo ""
  echo -ne "${CYAN}Select (1-20), / to add artist, q to quit: ${NC}"
}

# Run swarm on selected file
run_swarm() {
  local size="$1"
  local filename="$2"
  
  echo ""
  echo -e "${BOLD}${GREEN}Starting SWARM download${NC}"
  echo -e "  File: $filename"
  echo -e "  Size: $size bytes"
  echo -e "  Chunk: 128KB"
  echo ""
  
  # Get sources for this size
  local sources=$(api GET "discovery/sources/by-size/$size?limit=50" 2>/dev/null | jq '.sourceCount')
  echo -e "  ${CYAN}Found $sources sources${NC}"
  echo ""
  echo -e "${YELLOW}=== SWARM Activity ===${NC}"
  
  # Start log watcher
  tail -f "$LOG" 2>/dev/null | grep --line-buffered -E "SWARM|Marked" &
  LOG_PID=$!
  
  # Run swarm
  local result=$(curl -s --max-time 300 -X POST "http://localhost:$(get_port)/api/v0/MultiSource/swarm" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer $(get_token)" \
    -d "{\"filename\": \"$filename\", \"size\": $size, \"chunkSize\": 131072, \"searchTimeout\": 15000}")
  
  kill $LOG_PID 2>/dev/null
  
  echo ""
  echo -e "${BOLD}=== Result ===${NC}"
  echo "$result" | jq '.' 2>/dev/null || echo "$result"
  
  echo ""
  echo -e "${YELLOW}Press Enter to continue...${NC}"
  read -r
}

# Interactive file selector
interactive_select() {
  while true; do
    clear
    echo -e "${BOLD}${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}║              SWARM TEST - File Browser                      ║${NC}"
    echo -e "${BOLD}${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    # Get stats
    local stats=$(api GET "discovery" 2>/dev/null)
    local files=$(echo "$stats" | jq -r '.stats.totalFiles // 0')
    local users=$(echo "$stats" | jq -r '.stats.totalUsers // 0')
    local running=$(echo "$stats" | jq -r '.isRunning // false')
    local term=$(echo "$stats" | jq -r '.currentSearchTerm // "none"')
    local flagged=$(api GET "discovery/no-partial-count" 2>/dev/null | jq -r '.usersWithoutPartialSupport // 0')
    
    echo -e "  ${GREEN}Discovery: $files files, $users users${NC}"
    [ "$running" = "true" ] && echo -e "  ${YELLOW}Searching: $term${NC}"
    echo -e "  ${RED}Flagged (no partial): $flagged users${NC}"
    echo ""
    echo -e "${YELLOW}  /  = Add artist    r = Reset flags    q = Quit${NC}"
    echo ""
    echo -e "${BOLD}Files with 3+ sources (select to swarm):${NC}"
    echo ""
    
    # Store summaries in temp file for selection
    api GET "discovery/summaries?minSources=3" 2>/dev/null | \
      jq -r '.summaries[] | select(.size > 1000000) | "\(.sourceCount)|\(.size)|\(.sampleFilename)"' | \
      head -20 > /tmp/swarm-menu.txt
    
    local i=1
    while IFS='|' read -r count size filename; do
      local name=$(echo "$filename" | sed 's/\\/\//g' | xargs -d'\n' basename 2>/dev/null | sed 's/\.[^.]*$//')
      local mb=$((size / 1000000))
      printf "  ${CYAN}%2d)${NC} [%2d src] %4dMB  %s\n" "$i" "$count" "$mb" "${name:0:55}"
      ((i++))
    done < /tmp/swarm-menu.txt
    
    echo ""
    echo -ne "${CYAN}Select (1-$((i-1))), /, r, or q: ${NC}"
    read -r choice
    
    case "$choice" in
      /) add_artist ;;
      r) 
        api POST "discovery/reset-partial-flags" "{}" | jq -r '.message'
        sleep 2
        ;;
      q) exit 0 ;;
      [0-9]*)
        local line=$(sed -n "${choice}p" /tmp/swarm-menu.txt)
        if [ -n "$line" ]; then
          local size=$(echo "$line" | cut -d'|' -f2)
          local filename=$(echo "$line" | cut -d'|' -f3)
          run_swarm "$size" "$filename"
        fi
        ;;
    esac
  done
}

# Main
main() {
  echo -e "${BOLD}${GREEN}SWARM TEST${NC}"
  echo ""
  
  # Check if server is running
  if ! api GET "session" > /dev/null 2>&1; then
    start_server || exit 1
  else
    echo -e "${GREEN}Server already running on port $(get_port)${NC}"
  fi
  
  # Initialize artist file if needed
  [ ! -f "$DISCOVERY_ARTISTS" ] && echo "daft punk" > "$DISCOVERY_ARTISTS"
  
  # Start background discovery
  background_discovery &
  DISCOVERY_PID=$!
  
  # Run interactive menu
  interactive_select
}

main "$@"
