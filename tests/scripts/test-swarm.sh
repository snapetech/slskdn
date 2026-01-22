#!/bin/bash
# SWARM TEST - One-click interactive test suite
# Requires: SLSK_USER and SLSK_PASS env vars

LOG="/tmp/slskd.log"
PORT_FILE="/tmp/slskd-port.txt"
ARTIST_QUEUE="/tmp/slskd-artist-queue.txt"
DISCOVERY_START="/tmp/slskd-discovery-start.txt"
BG_JOBS="/tmp/slskd-bg-jobs.txt"
CYCLE_SECONDS=270

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; DIM='\033[2m'; NC='\033[0m'

die() { echo -e "${RED}$1${NC}"; exit 1; }

[ -z "$SLSK_USER" ] && die "Missing SLSK_USER. Run: export SLSK_USER=your_username"
[ -z "$SLSK_PASS" ] && die "Missing SLSK_PASS. Run: export SLSK_PASS=your_password"

cleanup() { 
  [ -n "$TAIL_PID" ] && kill $TAIL_PID 2>/dev/null
  [ -n "$SWARM_PID" ] && kill $SWARM_PID 2>/dev/null
}
trap cleanup EXIT

get_port() { cat "$PORT_FILE" 2>/dev/null; }

get_token() {
  local port=$(get_port)
  [ -z "$port" ] && return 1
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

server_ok() { [ -n "$(get_token)" ]; }

start_server() {
  echo -e "${YELLOW}Starting server...${NC}"
  
  # Kill ALL dotnet/slskd processes aggressively
  pkill -9 -f "dotnet.*slskd" 2>/dev/null
  pkill -9 -f "slskd" 2>/dev/null
  pkill -9 -f "VBCSCompiler" 2>/dev/null
  sleep 3
  
  # Double-check
  if pgrep -f "slskd" > /dev/null 2>&1; then
    echo -e "${RED}Could not kill existing slskd processes. Trying harder...${NC}"
    killall -9 dotnet slskd 2>/dev/null
    sleep 2
  fi
  
  for attempt in 1 2 3; do
    local HTTP=$((30000 + RANDOM % 25000))
    local SLSK=$((30000 + RANDOM % 25000))
    echo -e "  Attempt $attempt: HTTP=$HTTP SLSK=$SLSK"
    
    cd /home/keith/Documents/Code/slskdn/src/slskd
    SLSKD_SLSK_USERNAME="$SLSK_USER" SLSKD_SLSK_PASSWORD="$SLSK_PASS" \
    SLSKD_HTTP_PORT=$HTTP SLSKD_SLSK_LISTEN_PORT=$SLSK SLSKD_NO_HTTPS=true \
    nohup dotnet run --configuration Release &> "$LOG" &
    
    echo $HTTP > "$PORT_FILE"
    
    for i in {1..20}; do
      sleep 2
      grep -q "Logged in to the Soulseek" "$LOG" 2>/dev/null && echo -e "${GREEN}  ✓ Ready${NC}" && return 0
      grep -qE "AddressInUse|Exception.*failed" "$LOG" 2>/dev/null && break
      echo -n "."
    done
    pkill -9 -f "dotnet.*slskd" 2>/dev/null; sleep 1
  done
  echo -e "${RED}Failed${NC}"; tail -20 "$LOG"; return 1
}

queue_artist() {
  local artist="$1"
  [ -z "$artist" ] && return
  grep -qiF "$artist" "$ARTIST_QUEUE" 2>/dev/null && echo -e "${YELLOW}'$artist' already queued${NC}" && sleep 1 && return
  echo "$artist" >> "$ARTIST_QUEUE"
  echo -e "${GREEN}Queued '$artist'${NC}"; sleep 1
}

rotate_artist() {
  [ ! -f "$ARTIST_QUEUE" ] || [ ! -s "$ARTIST_QUEUE" ] && return 1
  local next=$(head -1 "$ARTIST_QUEUE")
  [ -z "$next" ] && return 1
  tail -n +2 "$ARTIST_QUEUE" > /tmp/aq-tmp.txt 2>/dev/null
  echo "$next" >> /tmp/aq-tmp.txt
  mv /tmp/aq-tmp.txt "$ARTIST_QUEUE"
  api POST "discovery/stop" "{}" > /dev/null 2>&1; sleep 1
  api POST "discovery/start" "{\"searchText\": \"$next\"}" > /dev/null 2>&1
  date +%s > "$DISCOVERY_START"
  echo "$next"
}

check_rotation() {
  local running=$(api GET "discovery" 2>/dev/null | jq -r '.isRunning // false')
  if [ "$running" != "true" ]; then
    rotate_artist > /dev/null; return
  fi
  [ ! -f "$DISCOVERY_START" ] && date +%s > "$DISCOVERY_START"
  local elapsed=$(($(date +%s) - $(cat "$DISCOVERY_START" 2>/dev/null || echo 0)))
  [ "$elapsed" -ge "$CYCLE_SECONDS" ] && rotate_artist > /dev/null
}

handle_input() {
  [[ "$1" == /* ]] && { queue_artist "${1:1}"; return 0; }
  return 1
}

extract_song_name() {
  basename "$1" | sed 's/\.[^.]*$//' | sed 's/^[0-9]*[.-]\s*//'
}

count_artist_files() {
  api GET "discovery/summaries?minSources=1" 2>/dev/null | \
    jq -r --arg a "$1" '[.summaries[] | select(.sampleFilename | ascii_downcase | contains($a | ascii_downcase))] | length'
}

# Run swarm with q/b controls
run_swarm() {
  local size="$1" filename="$2"
  local search_term=$(extract_song_name "$filename")
  local result_file="/tmp/swarm-result-$$.txt"
  
  clear
  echo -e "${BOLD}${GREEN}╔══════════════════════════════════════════════════════════════╗${NC}"
  echo -e "${BOLD}${GREEN}║                    SWARM DOWNLOAD                            ║${NC}"
  echo -e "${BOLD}${GREEN}╚══════════════════════════════════════════════════════════════╝${NC}"
  echo ""
  echo -e "  Song:   ${CYAN}$search_term${NC}"
  echo -e "  Size:   ${CYAN}$((size / 1000000))MB${NC} ($size bytes)"
  echo ""
  echo -e "  ${DIM}q = cancel/back   b = background   e = exit${NC}"
  echo ""
  echo -e "${YELLOW}═══ Live Activity ═══${NC}"
  
  # Start log tail
  tail -f "$LOG" 2>/dev/null | grep --line-buffered -E "SWARM|Chunk|worker" | sed 's/^/  /' &
  TAIL_PID=$!
  
  # Start swarm in background
  # Use discovery DB for sources (much faster, more complete)
  local json=$(jq -n --arg fn "$search_term" --argjson sz "$size" \
    '{filename: $fn, size: $sz, chunkSize: 131072, useDiscoveryDb: true}')
  
  (curl -s --max-time 300 -X POST "http://localhost:$(get_port)/api/v0/MultiSource/swarm" \
    -H "Content-Type: application/json" -H "Authorization: Bearer $(get_token)" \
    -d "$json" > "$result_file" 2>&1) &
  SWARM_PID=$!
  
  # Wait for completion or user input
  while kill -0 $SWARM_PID 2>/dev/null; do
    read -t 1 -n 1 key
    case "$key" in
      q|Q)
        kill $SWARM_PID 2>/dev/null
        kill $TAIL_PID 2>/dev/null; TAIL_PID=""
        echo ""
        echo -e "${RED}Cancelled${NC}"
        sleep 1
        return
        ;;
      e|E)
        kill $SWARM_PID 2>/dev/null
        kill $TAIL_PID 2>/dev/null
        echo -e "${GREEN}Bye!${NC}"
        exit 0
        ;;
      b|B)
        # Background it
        echo "$SWARM_PID|$search_term|$size|$result_file" >> "$BG_JOBS"
        kill $TAIL_PID 2>/dev/null; TAIL_PID=""
        SWARM_PID=""  # Don't kill on exit
        echo ""
        echo -e "${YELLOW}Backgrounded. Check 'b' from main menu.${NC}"
        sleep 1
        return
        ;;
    esac
  done
  
  kill $TAIL_PID 2>/dev/null; TAIL_PID=""
  
  echo ""
  echo -e "${YELLOW}═══ Result ═══${NC}"
  cat "$result_file" 2>/dev/null | jq '{success, sourcesUsed, error}' 2>/dev/null || cat "$result_file"
  rm -f "$result_file"
  echo ""
  read -p "Press Enter..."
}

# Show backgrounded jobs
show_bg_jobs() {
  clear
  echo -e "${BOLD}${CYAN}Backgrounded Downloads${NC}"
  echo ""
  
  [ ! -f "$BG_JOBS" ] || [ ! -s "$BG_JOBS" ] && echo -e "  ${DIM}No backgrounded jobs${NC}" && sleep 2 && return
  
  local i=1
  > /tmp/bg-active.txt
  while IFS='|' read -r pid name size result_file; do
    if kill -0 $pid 2>/dev/null; then
      printf "  ${CYAN}%d)${NC} [RUNNING] %s (%dMB)\n" "$i" "$name" "$((size/1000000))"
      echo "$pid|$name|$size|$result_file" >> /tmp/bg-active.txt
    elif [ -f "$result_file" ]; then
      local status=$(cat "$result_file" | jq -r '.success // "unknown"' 2>/dev/null)
      printf "  ${CYAN}%d)${NC} [%s] %s\n" "$i" "$status" "$name"
      echo "done|$name|$size|$result_file" >> /tmp/bg-active.txt
    fi
    ((i++))
  done < "$BG_JOBS"
  
  mv /tmp/bg-active.txt "$BG_JOBS" 2>/dev/null
  
  echo ""
  echo -ne "${CYAN}Select to view, or Enter to go back: ${NC}"
  read -r choice
  
  if [[ "$choice" =~ ^[0-9]+$ ]]; then
    local line=$(sed -n "${choice}p" "$BG_JOBS" 2>/dev/null)
    if [ -n "$line" ]; then
      local result_file=$(echo "$line" | cut -d'|' -f4)
      echo ""
      echo -e "${YELLOW}Result:${NC}"
      cat "$result_file" 2>/dev/null | jq . 2>/dev/null || cat "$result_file"
      echo ""
      read -p "Press Enter..."
    fi
  fi
}

artist_menu() {
  while true; do
    check_rotation
    
    clear
    echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}║              SWARM TEST - SELECT ARTIST                      ║${NC}"
    echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    local stats=$(api GET "discovery" 2>/dev/null)
    local files=$(echo "$stats" | jq -r '.stats.totalFiles // 0')
    local users=$(echo "$stats" | jq -r '.stats.totalUsers // 0')
    local running=$(echo "$stats" | jq -r '.isRunning // false')
    local term=$(echo "$stats" | jq -r '.currentSearchTerm // "-"')
    
    local remaining=$((CYCLE_SECONDS - ($(date +%s) - $(cat "$DISCOVERY_START" 2>/dev/null || echo 0))))
    [ "$remaining" -lt 0 ] && remaining=0
    
    local bg_count=$(wc -l < "$BG_JOBS" 2>/dev/null || echo 0)
    
    local bg_str=""
    [ "$bg_count" -gt 0 ] && bg_str="   ${CYAN}BG Jobs:${NC} $bg_count"
    echo -e "  ${GREEN}Pool:${NC} $files files, $users users$bg_str"
    [ "$running" = "true" ] && echo -e "  ${YELLOW}Discovering:${NC} $term ($((remaining/60))m$((remaining%60))s)"
    echo ""
    echo -e "  ${DIM}/Artist = queue   n = next   b = bg jobs   l = logs   e = exit${NC}"
    echo ""
    
    echo -e "${BOLD}Artists:${NC}"
    echo ""
    local i=1
    > /tmp/artists.txt
    while read -r artist; do
      [ -z "$artist" ] && continue
      local count=$(count_artist_files "$artist")
      printf "  ${CYAN}%2d)${NC} [%5d files] %s\n" "$i" "$count" "$artist"
      echo "$artist" >> /tmp/artists.txt
      ((i++))
    done < "$ARTIST_QUEUE"
    
    [ "$i" -eq 1 ] && echo -e "  ${DIM}No artists. Type /ArtistName${NC}"
    
    echo ""
    echo -ne "${CYAN}Select [1-$((i-1))], /Artist, n, b, l, e: ${NC}"
    read -r choice
    [ -z "$choice" ] && continue
    
    handle_input "$choice" && continue
    
    case "$choice" in
      n|N) rotate_artist > /dev/null ;;
      b|B) show_bg_jobs ;;
      l|L) tail -40 "$LOG" 2>/dev/null; read -p "Enter..." ;;
      e|E) echo -e "${GREEN}Bye!${NC}"; exit 0 ;;
      [0-9]*)
        local artist=$(sed -n "${choice}p" /tmp/artists.txt 2>/dev/null)
        [ -n "$artist" ] && files_menu "$artist"
        ;;
    esac
  done
}

files_menu() {
  local artist="$1"
  
  while true; do
    check_rotation
    
    clear
    echo -e "${BOLD}${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BOLD}${CYAN}║  FILES: $artist${NC}"
    echo -e "${BOLD}${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    local bg_count=$(wc -l < "$BG_JOBS" 2>/dev/null || echo 0)
    [ "$bg_count" -gt 0 ] && echo -e "  ${CYAN}Background jobs:${NC} $bg_count"
    echo ""
    echo -e "  ${DIM}/Artist = queue   b = bg jobs   q = back   e = exit${NC}"
    echo ""
    
    # FILTER: Only FLAC files > 1MB with 3+ sources
    api GET "discovery/summaries?minSources=3" 2>/dev/null | \
      jq -r --arg a "$artist" '
        .summaries[] | 
        select(.sampleFilename | ascii_downcase | contains($a | ascii_downcase)) |
        select(.sampleFilename | test("\\.flac$"; "i")) |
        select(.size > 1000000) |
        "\(.sourceCount)|\(.size)|\(.sampleFilename)"
      ' 2>/dev/null | sort -t'|' -k1 -rn | head -20 > /tmp/artist-files.txt
    
    local i=1
    while IFS='|' read -r count size filename; do
      # Convert backslashes to forward slashes, then get basename without .flac
      local name=$(echo "$filename" | sed 's/\\/\//g' | xargs -d'\n' basename | sed 's/\.flac$//')
      printf "  ${CYAN}%2d)${NC} [%2d src] %4dMB  %s\n" "$i" "$count" "$((size/1000000))" "$name"
      ((i++))
    done < /tmp/artist-files.txt
    
    [ "$i" -eq 1 ] && echo -e "  ${DIM}No FLAC files with 3+ sources found.${NC}"
    
    echo ""
    echo -ne "${CYAN}Select [1-$((i-1))], /Artist, b, q, e: ${NC}"
    read -r choice
    [ -z "$choice" ] && continue
    
    handle_input "$choice" && continue
    
    case "$choice" in
      b|B) show_bg_jobs ;;
      q|Q) return ;;
      e|E) echo -e "${GREEN}Bye!${NC}"; exit 0 ;;
      [0-9]|[0-9][0-9])
        local line=$(sed -n "${choice}p" /tmp/artist-files.txt 2>/dev/null)
        if [ -n "$line" ]; then
          local sz=$(echo "$line" | cut -d'|' -f2)
          local fn=$(echo "$line" | cut -d'|' -f3)
          run_swarm "$sz" "$fn"
        fi
        ;;
    esac
  done
}

# ═══════════════════════════════════════════════════════════════
# MAIN
# ═══════════════════════════════════════════════════════════════

clear
echo -e "${BOLD}${GREEN}SWARM TEST SUITE${NC}"
echo ""

server_ok || start_server || exit 1
echo -e "${GREEN}✓ Server on port $(get_port)${NC}"

[ ! -f "$ARTIST_QUEUE" ] || [ ! -s "$ARTIST_QUEUE" ] && cat > "$ARTIST_QUEUE" << 'EOF'
daft punk
metallica
cher
EOF

> "$BG_JOBS"  # Clear bg jobs on start

echo -e "${YELLOW}Starting discovery...${NC}"
rotate_artist > /dev/null
sleep 1

artist_menu
