#!/bin/bash
# Multi-Source Download Test Script
# Tests parallel downloads from multiple Soulseek peers

set -e

API_URL="http://localhost:54321"
RESULTS_FILE="/tmp/multisource-results.json"
MIN_SOURCES=3
MAX_SOURCES=10

# Artists to test (popular, should have many seeds)
ARTISTS=(
    "radiohead"
    "pink floyd"
    "nirvana"
    "queen"
    "led zeppelin"
)

get_token() {
    curl -s -X POST "$API_URL/api/v0/session" \
        -H "Content-Type: application/json" \
        -d '{"username":"slskd","password":"slskd"}' | jq -r '.token'
}

search_artist() {
    local artist="$1"
    local token="$2"
    
    echo "Searching: $artist flac..." >&2
    
    local search_id=$(curl -s -X POST "$API_URL/api/v0/searches" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json" \
        -d "{\"searchText\":\"$artist flac\",\"responseLimit\":2000,\"searchTimeout\":45000}" | jq -r '.id')
    
    echo "$search_id"
}

wait_for_search() {
    local search_id="$1"
    local token="$2"
    local max_wait=60
    
    for i in $(seq 1 $max_wait); do
        local state=$(curl -s "$API_URL/api/v0/searches/$search_id" \
            -H "Authorization: Bearer $token" | jq -r '.state')
        
        if [[ "$state" == *"Completed"* ]]; then
            return 0
        fi
        sleep 1
    done
    return 1
}

find_best_candidate() {
    local search_id="$1"
    local token="$2"
    
    local responses=$(curl -s "$API_URL/api/v0/searches/$search_id/responses" \
        -H "Authorization: Bearer $token")
    
    # Find files with MIN_SOURCES to MAX_SOURCES identical sources
    echo "$responses" | jq --argjson min "$MIN_SOURCES" --argjson max "$MAX_SOURCES" '
        [.[] | .username as $user | .uploadSpeed as $speed | .hasFreeUploadSlot as $free |
         .files[] | select(.filename | test("\\.flac$"; "i")) | 
         {user: $user, speed: $speed, free: $free, file: .filename, size: .size}
        ]
        | group_by(.file | split("\\\\") | .[-1] | ascii_downcase + "|" + (.size | tostring))
        | map(select(([.[].user] | unique | length) >= $min and ([.[].user] | unique | length) <= $max))
        | map({
            filename: .[0].file | split("\\\\") | .[-1],
            size: .[0].size,
            sizeMB: (.[0].size / 1024 / 1024 | floor),
            sourceCount: ([.[].user] | unique | length),
            sources: [.[] | {user, speed, free, path: .file}] | unique_by(.user) | sort_by(-.speed)
        })
        | sort_by(-.sourceCount)
        | .[0]
    '
}

clear_downloads() {
    local token="$1"
    curl -s -X DELETE "$API_URL/api/v0/transfers/downloads?remove=true" \
        -H "Authorization: Bearer $token" > /dev/null
}

queue_downloads() {
    local token="$1"
    local sources_json="$2"
    
    echo "$sources_json" | jq -r '.[] | "\(.user)|\(.path)|\(.size)"' | while IFS='|' read -r user path size; do
        curl -s -X POST "$API_URL/api/v0/transfers/downloads/$user" \
            -H "Authorization: Bearer $token" \
            -H "Content-Type: application/json" \
            -d "[{\"filename\":\"$path\",\"size\":$size}]" > /dev/null
        echo "  Queued: $user" >&2
    done
}

monitor_downloads() {
    local token="$1"
    local expected="$2"
    local max_time=300  # 5 minutes max
    local start_time=$(date +%s)
    
    echo ""
    echo "=== REALTIME DOWNLOAD PROGRESS ==="
    echo ""
    
    while true; do
        local now=$(date +%s)
        local elapsed=$((now - start_time))
        
        if [ $elapsed -gt $max_time ]; then
            echo "TIMEOUT after ${max_time}s"
            break
        fi
        
        # Get all download statuses
        local status=$(curl -s "$API_URL/api/v0/transfers/downloads" \
            -H "Authorization: Bearer $token")
        
        # Clear screen and show status
        printf "\033[2J\033[H"
        echo "═══════════════════════════════════════════════════════════════════"
        echo "  MULTI-SOURCE DOWNLOAD TEST - Elapsed: ${elapsed}s"
        echo "═══════════════════════════════════════════════════════════════════"
        echo ""
        
        # Show each download with progress bar
        echo "$status" | jq -r '
            .[] | .directories[].files[] | 
            "\(.username)|\(.state)|\(.percentComplete)|\(.averageSpeed)|\(.bytesTransferred)|\(.size)"
        ' | while IFS='|' read -r user state pct speed bytes total; do
            local pct_int=${pct%.*}
            local speed_kb=$((${speed%.*} / 1024))
            local bar_len=$((pct_int / 5))
            local bar=$(printf '█%.0s' $(seq 1 $bar_len 2>/dev/null) 2>/dev/null || echo "")
            local empty=$((20 - bar_len))
            local empty_bar=$(printf '░%.0s' $(seq 1 $empty 2>/dev/null) 2>/dev/null || echo "")
            
            if [[ "$state" == *"Succeeded"* ]]; then
                printf "  ✅ %-15s [████████████████████] 100%% DONE\n" "$user"
            elif [[ "$state" == *"Rejected"* ]] || [[ "$state" == *"Errored"* ]]; then
                printf "  ❌ %-15s [--------------------] FAILED: %s\n" "$user" "$state"
            else
                printf "  ⏳ %-15s [%-20s] %3d%% @ %d KB/s\n" "$user" "${bar}${empty_bar}" "$pct_int" "$speed_kb"
            fi
        done
        
        echo ""
        echo "═══════════════════════════════════════════════════════════════════"
        
        # Check if all complete
        local completed=$(echo "$status" | jq '[.[] | .directories[].files[] | select(.state | contains("Completed"))] | length')
        local total=$(echo "$status" | jq '[.[] | .directories[].files[]] | length')
        
        if [ "$completed" -ge "$expected" ] || [ "$completed" -ge "$total" ]; then
            echo ""
            echo "ALL DOWNLOADS COMPLETE!"
            break
        fi
        
        sleep 2
    done
    
    # Return final status
    curl -s "$API_URL/api/v0/transfers/downloads" -H "Authorization: Bearer $token"
}

run_test() {
    local artist="$1"
    local token="$2"
    local test_num="$3"
    
    echo ""
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  TEST $test_num: $artist"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    
    # Search
    echo "Step 1: Searching..."
    local search_id=$(search_artist "$artist" "$token")
    echo "  Search ID: $search_id"
    
    echo "Step 2: Waiting for results (up to 60s)..."
    if ! wait_for_search "$search_id" "$token"; then
        echo "  ERROR: Search timed out"
        return 1
    fi
    
    local search_info=$(curl -s "$API_URL/api/v0/searches/$search_id" -H "Authorization: Bearer $token")
    local resp_count=$(echo "$search_info" | jq '.responseCount')
    local file_count=$(echo "$search_info" | jq '.fileCount')
    echo "  Found: $resp_count responses, $file_count files"
    
    # Find best candidate
    echo "Step 3: Finding best multi-source candidate ($MIN_SOURCES-$MAX_SOURCES sources)..."
    local candidate=$(find_best_candidate "$search_id" "$token")
    
    if [ "$candidate" == "null" ] || [ -z "$candidate" ]; then
        echo "  ERROR: No suitable candidate found"
        return 1
    fi
    
    local filename=$(echo "$candidate" | jq -r '.filename')
    local size_mb=$(echo "$candidate" | jq '.sizeMB')
    local source_count=$(echo "$candidate" | jq '.sourceCount')
    
    echo "  Best: $filename ($size_mb MB) with $source_count sources"
    
    # Show sources
    echo ""
    echo "Step 4: Sources (top 10 by speed):"
    echo "$candidate" | jq -r '.sources[:10][] | "  - \(.user) @ \((.speed / 1024) | floor) KB/s \(if .free then "✓free" else "queued" end)"'
    
    # Clear and queue
    echo ""
    echo "Step 5: Queueing downloads..."
    clear_downloads "$token"
    
    local sources=$(echo "$candidate" | jq '.sources[:10]')
    local num_sources=$(echo "$sources" | jq 'length')
    queue_downloads "$token" "$sources"
    
    # Monitor
    echo ""
    echo "Step 6: Monitoring $num_sources parallel downloads..."
    sleep 2
    local final_status=$(monitor_downloads "$token" "$num_sources")
    
    # Summarize
    echo ""
    echo "Step 7: Results Summary"
    echo "$final_status" | jq -r '
        [.[] | .directories[].files[]] |
        {
            total: length,
            succeeded: [.[] | select(.state | contains("Succeeded"))] | length,
            failed: [.[] | select(.state | contains("Rejected") or contains("Errored"))] | length,
            fastest: ([.[] | select(.state | contains("Succeeded"))] | sort_by(-.averageSpeed) | .[0] | "\(.username) @ \((.averageSpeed / 1024) | floor) KB/s"),
            total_time: ([.[] | select(.state | contains("Succeeded")) | .elapsedTime] | .[0])
        }
    '
    
    # Save result
    local result=$(echo "$final_status" | jq --arg artist "$artist" --arg file "$filename" '{
        artist: $artist,
        file: $file,
        sources: [.[] | .directories[].files[] | {user: .username, state, speed: .averageSpeed, time: .elapsedTime}]
    }')
    
    echo "$result" >> "$RESULTS_FILE"
    
    return 0
}

main() {
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║       MULTI-SOURCE SOULSEEK DOWNLOAD TEST                        ║"
    echo "║       Testing $MIN_SOURCES-$MAX_SOURCES sources per file                           ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    
    # Get token
    echo "Authenticating..."
    TOKEN=$(get_token)
    if [ -z "$TOKEN" ] || [ "$TOKEN" == "null" ]; then
        echo "ERROR: Failed to authenticate"
        exit 1
    fi
    echo "  OK"
    
    # Clear results file
    echo "[]" > "$RESULTS_FILE"
    
    # Run tests
    local test_num=1
    for artist in "${ARTISTS[@]}"; do
        if run_test "$artist" "$TOKEN" "$test_num"; then
            echo ""
            echo "Test $test_num PASSED"
        else
            echo ""
            echo "Test $test_num SKIPPED (no suitable candidate)"
        fi
        
        test_num=$((test_num + 1))
        
        # Pause between tests
        echo ""
        echo "Pausing 5s before next test..."
        sleep 5
    done
    
    # Final summary
    echo ""
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║                    FINAL RESULTS SUMMARY                         ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    cat "$RESULTS_FILE" | jq '.'
}

# Run if not sourced
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi

