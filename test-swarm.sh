#!/bin/bash
# Interactive Swarm Test - scrolling output + input at bottom
# Requires: SLSK_USER, SLSK_PASS env vars

LOG="/tmp/slskd.log"
PORT_FILE="/tmp/slskd-port.txt"
FIFO="/tmp/slskd-cmd-$$"

[ -z "$SLSK_USER" ] || [ -z "$SLSK_PASS" ] && {
  echo "Set SLSK_USER and SLSK_PASS first"
  exit 1
}

cleanup() {
  rm -f "$FIFO"
  pkill -P $$ 2>/dev/null
  tput cnorm  # Show cursor
}
trap cleanup EXIT

get_port() { cat "$PORT_FILE" 2>/dev/null; }
api() {
  local port=$(get_port) method="$1" endpoint="$2" data="$3"
  local token=$(curl -s -X POST "http://localhost:$port/api/v0/session" \
    -H "Content-Type: application/json" -d '{"username":"slskd","password":"slskd"}' | jq -r '.token')
  [ "$method" = "GET" ] && curl -s "http://localhost:$port/api/v0/$endpoint" -H "Authorization: Bearer $token" \
    || curl -s -X POST "http://localhost:$port/api/v0/$endpoint" -H "Content-Type: application/json" \
         -H "Authorization: Bearer $token" -d "$data"
}

do_restart() {
  echo -e "\033[33m[SYSTEM] Restarting server...\033[0m"
  pkill -9 -f "dotnet.*slskd" 2>/dev/null; sleep 1
  for attempt in 1 2 3; do
    HTTP=$((30000 + RANDOM % 25000)); SLSK=$((30000 + RANDOM % 25000))
    cd /home/keith/Documents/Code/slskdn/src/slskd
    SLSKD_SLSK_USERNAME="$SLSK_USER" SLSKD_SLSK_PASSWORD="$SLSK_PASS" \
      SLSKD_HTTP_PORT=$HTTP SLSKD_SLSK_LISTEN_PORT=$SLSK SLSKD_NO_HTTPS=true \
      nohup dotnet run --configuration Release > "$LOG" 2>&1 &
    echo $HTTP > "$PORT_FILE"
    for i in {1..15}; do
      sleep 2
      grep -q "Connected to the Soulseek" "$LOG" && {
        echo -e "\033[32m[SYSTEM] Ready on port $HTTP\033[0m"
        return 0
      }
      grep -qE "Exception|failed" "$LOG" && break
    done
    pkill -9 -f "dotnet.*slskd" 2>/dev/null; sleep 1
  done
  echo -e "\033[31m[SYSTEM] Failed to start\033[0m"
  return 1
}

process_cmd() {
  local cmd="$1" args="$2"
  case "$cmd" in
    restart) do_restart ;;
    discover|d)
      [ -z "$args" ] && { echo -e "\033[31m[ERROR] Usage: discover <artist>\033[0m"; return; }
      echo -e "\033[36m[DISCOVER] Adding '$args' to pool...\033[0m"
      api POST "discovery/start" "{\"searchTerm\": \"$args\", \"enableHashVerification\": true}" | jq -r '.message // .' &
      ;;
    stop) api POST "discovery/stop" "{}" | jq -r '.message // .' ;;
    swarm)
      local size="${args%% *}" chunk="${args#* }"
      [ -z "$size" ] && size=43586375
      [ "$chunk" = "$size" ] && chunk=131072
      echo -e "\033[36m[SWARM] Starting download (size=$size, chunk=$chunk)...\033[0m"
      api POST "MultiSource/swarm" "{\"filename\":\"test.flac\",\"size\":$size,\"chunkSize\":$chunk,\"searchTimeout\":10000}" > /tmp/swarm-result.json 2>&1 &
      ;;
    status|s)
      echo -e "\033[33m[STATUS]\033[0m"
      api GET "discovery" | jq -c '{running:.isRunning,term:.currentSearchTerm,files:.stats.totalFiles,users:.stats.totalUsers}'
      api GET "discovery/no-partial-count" | jq -r '.message'
      ;;
    sources)
      [ -z "$args" ] && api GET "discovery/summaries?minSources=3" | jq -c '.summaries[:5][]' \
        || api GET "discovery/sources/by-size/$args?limit=10" | jq -c '.sources[]'
      ;;
    reset) api POST "discovery/reset-partial-flags" "{}" | jq -r '.message' ;;
    kill) pkill -9 -f "dotnet.*slskd" && echo "[SYSTEM] Killed" || echo "[SYSTEM] Not running" ;;
    help|h|"?")
      echo -e "\033[36m=== Commands (type while output scrolls) ===\033[0m"
      echo "  restart       - Restart server"
      echo "  discover <x>  - Add artist to discovery pool (e.g., 'discover metallica')"
      echo "  stop          - Stop discovery"
      echo "  swarm [size] [chunk] - Start swarm download"
      echo "  status        - Show stats"
      echo "  sources [size] - List sources"
      echo "  reset         - Reset partial-support flags"
      echo "  kill          - Kill server"
      echo "  quit          - Exit"
      ;;
    quit|q|exit) echo "[BYE]"; exit 0 ;;
    "") ;;
    *) echo -e "\033[31m[?] Unknown: $cmd (try 'help')\033[0m" ;;
  esac
}

interactive_loop() {
  echo -e "\033[32m╔════════════════════════════════════════════╗\033[0m"
  echo -e "\033[32m║   SWARM TEST - Type commands while running  ║\033[0m"
  echo -e "\033[32m╚════════════════════════════════════════════╝\033[0m"
  echo "Type 'help' for commands. Output scrolls above, type below."
  echo ""
  
  # Start log tailer in background
  tail -f "$LOG" 2>/dev/null | grep --line-buffered -E "SWARM|Discovery|\[INF\].*Connected|\[ERR\]" | \
    sed 's/^/  /' &
  TAIL_PID=$!
  
  # Input loop with prompt at bottom
  while true; do
    echo -ne "\033[36mswarm>\033[0m "
    read -r input
    cmd="${input%% *}"
    args="${input#* }"
    [ "$args" = "$cmd" ] && args=""
    process_cmd "$cmd" "$args"
  done
}

# Main
case "${1:-}" in
  "") interactive_loop ;;
  restart) do_restart ;;
  status) api GET "discovery" | jq '.' ;;
  *) echo "Run without args for interactive mode" ;;
esac
