#!/bin/bash
# Swarm Download Test Script
# Set SLSK_USER and SLSK_PASS env vars before running

LOG="/tmp/slskd.log"
PORT_FILE="/tmp/slskd-port.txt"
MARKER="SLSKD_MARKER=slskdn-test"

if [ -z "$SLSK_USER" ] || [ -z "$SLSK_PASS" ]; then
  echo "ERROR: Set SLSK_USER and SLSK_PASS environment variables"
  echo "  export SLSK_USER=your_username"
  echo "  export SLSK_PASS=your_password"
  exit 1
fi

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'

get_port() { cat "$PORT_FILE" 2>/dev/null || echo ""; }
get_token() {
  local port=$(get_port); [ -z "$port" ] && return
  curl -s -X POST "http://localhost:$port/api/v0/session" -H "Content-Type: application/json" \
    -d '{"username": "slskd", "password": "slskd"}' 2>/dev/null | jq -r '.token // empty'
}
api() {
  local method="$1" endpoint="$2" data="$3" port=$(get_port) token=$(get_token)
  [ -z "$port" ] && echo "Server not running" && return 1
  [ "$method" = "GET" ] && curl -s "http://localhost:$port/api/v0/$endpoint" -H "Authorization: Bearer $token" \
    || curl -s -X POST "http://localhost:$port/api/v0/$endpoint" -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d "$data"
}

do_restart() {
  echo -e "${YELLOW}Restarting server...${NC}"
  pkill -9 -f "$MARKER" 2>/dev/null || true; sleep 1
  for attempt in 1 2 3; do
    HTTP=$((30000 + RANDOM % 25000)); SLSK=$((30000 + RANDOM % 25000))
    cd /home/keith/Documents/Code/slskdn/src/slskd
    $MARKER SLSKD_SLSK_USERNAME="$SLSK_USER" SLSKD_SLSK_PASSWORD="$SLSK_PASS" \
      SLSKD_HTTP_PORT=$HTTP SLSKD_SLSK_LISTEN_PORT=$SLSK SLSKD_NO_HTTPS=true \
      dotnet run --configuration Release > "$LOG" 2>&1 &
    echo $HTTP > "$PORT_FILE"
    for i in {1..15}; do
      sleep 2
      grep -q "Connected to the Soulseek" "$LOG" 2>/dev/null && echo -e "${GREEN}READY: http://localhost:$HTTP${NC}" && return 0
      grep -qE "Exception|failed to start" "$LOG" 2>/dev/null && pkill -9 -f "$MARKER" 2>/dev/null && sleep 1 && break
      echo -n "."
    done
  done
  echo -e "${RED}FAILED${NC}"; return 1
}

do_swarm() {
  local size="${1:-43586375}" chunk="${2:-131072}"
  echo -e "${CYAN}Starting swarm (size: $size, chunk: $chunk)${NC}"
  api POST "MultiSource/swarm" "{\"filename\": \"test.flac\", \"size\": $size, \"chunkSize\": $chunk, \"searchTimeout\": 10000}" > /tmp/swarm-result.json 2>&1 &
  echo "Swarm started (PID: $!)"
}

do_discover() {
  [ -z "$1" ] && echo -e "${RED}Usage: discover <artist>${NC}" && return 1
  echo -e "${CYAN}Starting discovery: $1${NC}"
  api POST "discovery/start" "{\"searchTerm\": \"$1\", \"enableHashVerification\": true}" | jq -r '.message // .'
}

do_status() {
  pgrep -f "$MARKER" > /dev/null && echo -e "${GREEN}Server: RUNNING on port $(get_port)${NC}" || { echo -e "${RED}Server: NOT RUNNING${NC}"; return; }
  local token=$(get_token); [ -n "$token" ] && {
    echo ""; api GET "discovery" | jq '{running: .isRunning, term: .currentSearchTerm, files: .stats.totalFiles, users: .stats.totalUsers}'
    echo ""; api GET "discovery/no-partial-count" | jq -r '.message'
  }
}

show_help() {
  echo -e "${CYAN}=== Commands ===${NC}"
  echo "  restart          - Restart server"
  echo "  status           - Show status"
  echo "  discover <artist> - Start discovery"
  echo "  stop             - Stop discovery"
  echo "  sources [size]   - Show sources"
  echo "  swarm [size] [chunk] - Start swarm"
  echo "  reset            - Reset partial flags"
  echo "  logs             - Watch logs"
  echo "  kill             - Kill server"
  echo "  quit             - Exit"
}

interactive() {
  echo -e "${GREEN}=== Swarm Test ===${NC}"; show_help
  while true; do
    echo -ne "${CYAN}swarm>${NC} "; read -r cmd args
    case "$cmd" in
      restart) do_restart ;; status|s) do_status ;; discover|d) do_discover "$args" ;;
      stop) api POST "discovery/stop" "{}" | jq -r '.message // .' ;;
      sources) [ -z "$args" ] && api GET "discovery/summaries?minSources=3" | jq '.summaries[:10]' || api GET "discovery/sources/by-size/$args" | jq '.sources[]' ;;
      swarm) do_swarm $args ;; reset) api POST "discovery/reset-partial-flags" "{}" | jq -r '.message' ;;
      logs|l) tail -f "$LOG" | grep --line-buffered -E "SWARM|Discovery" ;;
      kill) pkill -9 -f "$MARKER" && echo "Killed" || echo "Not running" ;;
      help|h) show_help ;; quit|q) exit 0 ;; "") ;; *) echo -e "${RED}Unknown: $cmd${NC}" ;;
    esac
  done
}

case "${1:-}" in "") interactive ;; restart) do_restart ;; status) do_status ;; *) echo "Run without args for interactive mode" ;; esac
