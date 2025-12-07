#!/bin/bash
# SWARM TEST - Smart pool management + live chunk visualization
# Refreshes pool if stale, caches results, runs swarm with live output

API="http://localhost:54321"
POOL="/tmp/flat.json"
POOL_MAX_AGE=300  # 5 minutes - refresh if older

get_token() {
    curl -s -X POST "$API/api/v0/session" \
        -H "Content-Type: application/json" \
        -d '{"username":"slskd","password":"slskd"}' 2>/dev/null | jq -r '.token // empty'
}

check_server() {
    local token=$(get_token)
    if [ -z "$token" ]; then
        echo "❌ Server not running at $API"
        return 1
    fi
    echo "✅ Server connected"
    return 0
}

pool_age() {
    if [ ! -f "$POOL" ]; then
        echo "999999"
        return
    fi
    local now=$(date +%s)
    local mod=$(stat -c %Y "$POOL" 2>/dev/null || echo "0")
    echo $((now - mod))
}

refresh_pool() {
    local search_term="${1:-daft punk random access memories flac}"
    local timeout="${2:-45}"
    local token=$(get_token)
    
    echo ""
    echo "🔄 Refreshing pool: '$search_term' (${timeout}s timeout)"
    echo ""
    
    # Search and build pool
    local result=$(curl -s --max-time $((timeout + 10)) -X POST "$API/api/v0/searches" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json" \
        -d "{\"searchText\": \"$search_term\", \"filterResponses\": true}" 2>/dev/null)
    
    local search_id=$(echo "$result" | jq -r '.id // empty')
    
    if [ -z "$search_id" ]; then
        echo "❌ Search failed"
        return 1
    fi
    
    echo "Search ID: $search_id"
    echo "Waiting ${timeout}s for results..."
    sleep "$timeout"
    
    # Get results
    local responses=$(curl -s "$API/api/v0/searches/$search_id/responses" \
        -H "Authorization: Bearer $token" 2>/dev/null)
    
    local count=$(echo "$responses" | jq 'length')
    echo "Got $count responses"
    
    # Flatten to pool format
    echo "$responses" | jq '[.[] | .username as $user | .uploadSpeed as $speed | .files[] | {user: $user, speed: $speed, path: .filename, size: .size}]' > "$POOL"
    
    local pool_size=$(jq 'length' "$POOL")
    echo "✅ Pool saved: $pool_size files"
    
    # Show top targets
    echo ""
    echo "Top targets by source count:"
    jq 'group_by(.size) | map({size: .[0].size, mb: (.[0].size/1048576|floor), users: ([.[].user]|unique|length), sample: (.[0].path|split("\\\\")[-1])}) | sort_by(-.users) | .[0:5] | .[] | "  [\(.users) sources] \(.sample) (\(.mb) MB)"' -r "$POOL"
}

run_swarm() {
    local size=$1
    local chunk_kb=${2:-128}
    local chunk_size=$((chunk_kb * 1024))
    local token=$(get_token)
    
    # Get sources from pool
    local sources=$(jq --argjson size "$size" '[.[] | select(.size == $size)] | unique_by(.user) | sort_by(-.speed) | .[0:40] | map({username: .user, fullPath: .path})' "$POOL")
    local num_sources=$(echo "$sources" | jq 'length')
    local num_chunks=$(( (size + chunk_size - 1) / chunk_size ))
    local first_path=$(echo "$sources" | jq -r '.[0].fullPath')
    
    echo ""
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  SWARM DOWNLOAD                                                   ║"
    echo "╠═══════════════════════════════════════════════════════════════════╣"
    echo "║  Size: $((size / 1024 / 1024)) MB | Chunks: $num_chunks × ${chunk_kb}KB | Sources: $num_sources"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    
    if [ "$num_sources" -lt 3 ]; then
        echo "❌ Not enough sources ($num_sources). Need at least 3."
        echo "Try: $0 refresh"
        return 1
    fi
    
    # Build request
    local request=$(jq -n \
        --arg filename "$first_path" \
        --argjson fileSize "$size" \
        --argjson chunkSize "$chunk_size" \
        --argjson sources "$sources" \
        '{filename: $filename, fileSize: $fileSize, chunkSize: $chunkSize, sources: $sources}')
    
    # Start log tail in background for live view
    echo "📡 Live chunk activity:"
    echo "────────────────────────────────────────────────────────────────────"
    
    # Tail log filtered for chunk activity
    tail -n0 -f /tmp/slskd.log 2>/dev/null | while read -r line; do
        if echo "$line" | grep -qE "Chunk [0-9]+ DONE|SWARM COMPLETE|SWARM FAILED"; then
            # Extract key info
            if echo "$line" | grep -q "DONE"; then
                user=$(echo "$line" | grep -oP '\[\K[^\]]+' | head -1)
                chunk=$(echo "$line" | grep -oP 'Chunk \K[0-9]+')
                speed=$(echo "$line" | grep -oP '\K[0-9.]+(?= KB/s)')
                completed=$(echo "$line" | grep -oP '\K[0-9]+(?=/)')
                total=$(echo "$line" | grep -oP '/\K[0-9]+')
                pct=$((completed * 100 / total))
                bar=$(printf '█%.0s' $(seq 1 $((pct / 5))))$(printf '░%.0s' $(seq 1 $((20 - pct / 5))))
                echo -e "\r[$bar] $pct% | ✅ $user chunk $chunk @ ${speed} KB/s    "
            elif echo "$line" | grep -q "SWARM COMPLETE"; then
                echo ""
                echo "🎉 SWARM COMPLETE!"
                break
            elif echo "$line" | grep -q "SWARM FAILED"; then
                echo ""
                echo "💥 SWARM FAILED"
                break
            fi
        fi
    done &
    TAIL_PID=$!
    
    # Make the API call
    local result=$(curl -s --max-time 300 -X POST "$API/api/v0/multisource/download" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json" \
        -d "$request" 2>/dev/null)
    
    # Kill tail
    kill $TAIL_PID 2>/dev/null
    wait $TAIL_PID 2>/dev/null
    
    echo ""
    echo "════════════════════════════════════════════════════════════════════"
    echo ""
    
    # Show result
    local success=$(echo "$result" | jq -r '.success')
    local error=$(echo "$result" | jq -r '.error // "none"')
    local sources_used=$(echo "$result" | jq -r '.sourcesUsed')
    local time_ms=$(echo "$result" | jq -r '.totalTimeMs')
    local output_path=$(echo "$result" | jq -r '.outputPath // empty')
    
    if [ "$success" == "true" ]; then
        local speed_mbps=$(echo "scale=2; $size / 1024 / 1024 / ($time_ms / 1000)" | bc 2>/dev/null || echo "?")
        echo "✅ SUCCESS!"
        echo "   Sources used: $sources_used"
        echo "   Time: ${time_ms}ms"
        echo "   Speed: ${speed_mbps} MB/s"
        echo "   Output: $output_path"
        
        # Verify FLAC integrity if applicable
        if [[ "$output_path" == *".flac" ]] && command -v flac >/dev/null; then
            echo ""
            echo "🔍 Verifying FLAC integrity..."
            if flac -t "$output_path" 2>&1 | grep -q "ok"; then
                echo "   ✅ FLAC verification PASSED"
            else
                echo "   ❌ FLAC verification FAILED"
                flac -t "$output_path" 2>&1 | head -5
            fi
        fi
        
        echo ""
        echo "Chunk distribution:"
        echo "$result" | jq -r '.chunks | group_by(.username) | map({user: .[0].username, count: length}) | sort_by(-.count) | .[] | "   \(.user): \(.count) chunks"'
    else
        echo "❌ FAILED: $error"
        echo "   Sources attempted: $num_sources"
    fi
}

show_targets() {
    if [ ! -f "$POOL" ]; then
        echo "No pool. Run: $0 refresh"
        return 1
    fi
    
    local age=$(pool_age)
    echo "Pool age: ${age}s (max: ${POOL_MAX_AGE}s)"
    echo ""
    echo "Available targets:"
    jq 'group_by(.size) | map({size: .[0].size, mb: (.[0].size/1048576|floor), users: ([.[].user]|unique|length), sample: (.[0].path|split("\\\\")[-1])}) | sort_by(-.users) | .[0:15] | .[] | "[\(.users) src] \(.sample) (\(.mb) MB) - size: \(.size)"' -r "$POOL"
}

auto_test() {
    # Check server
    check_server || return 1
    
    # Check pool freshness
    local age=$(pool_age)
    echo "Pool age: ${age}s"
    
    if [ "$age" -gt "$POOL_MAX_AGE" ]; then
        echo "Pool stale, refreshing..."
        refresh_pool "daft punk random access memories flac" 30
    fi
    
    # Pick best target (most sources)
    local best=$(jq 'group_by(.size) | map({size: .[0].size, users: ([.[].user]|unique|length)}) | sort_by(-.users) | .[0]' "$POOL")
    local size=$(echo "$best" | jq -r '.size')
    local users=$(echo "$best" | jq -r '.users')
    
    echo ""
    echo "Best target: $size bytes with $users sources"
    
    # Run swarm
    run_swarm "$size" 128
}

case "$1" in
    refresh)
        check_server || exit 1
        refresh_pool "${2:-daft punk random access memories flac}" "${3:-45}"
        ;;
    targets)
        show_targets
        ;;
    swarm)
        check_server || exit 1
        if [ -z "$2" ]; then
            echo "Usage: $0 swarm SIZE [CHUNK_KB]"
            echo "       $0 swarm 21721524 128"
            exit 1
        fi
        run_swarm "$2" "${3:-128}"
        ;;
    auto)
        auto_test
        ;;
    *)
        echo "SWARM TEST - Multi-source chunked downloads"
        echo ""
        echo "Usage: $0 <command> [args]"
        echo ""
        echo "Commands:"
        echo "  refresh [TERM] [SEC]  - Refresh pool with search (default: daft punk, 45s)"
        echo "  targets               - Show available download targets"
        echo "  swarm SIZE [CHUNK_KB] - Run swarm download (default: 128KB chunks)"
        echo "  auto                  - Auto-refresh if stale, pick best target, run"
        echo ""
        echo "Examples:"
        echo "  $0 refresh                    # Refresh pool"
        echo "  $0 targets                    # Show targets"
        echo "  $0 swarm 21721524 64          # 64KB chunks"
        echo "  $0 auto                       # Full auto test"
        ;;
esac

