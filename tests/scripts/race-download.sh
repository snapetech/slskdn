#!/bin/bash
# RACE MODE DOWNLOAD - Use pre-searched pool
# Pool must exist in /tmp/flat.json from prior search

API="http://localhost:54321"

get_token() {
    curl -s -X POST "$API/api/v0/session" \
        -H "Content-Type: application/json" \
        -d '{"username":"slskd","password":"slskd"}' | jq -r '.token'
}

show_pool() {
    echo "=== POOL: /tmp/flat.json ==="
    echo "Files: $(jq 'length' /tmp/flat.json)"
    echo ""
    echo "Top files by source count:"
    jq 'group_by(.size) | map({size: .[0].size, mb: (.[0].size/1048576|floor), users: ([.[].user]|unique|length), sample: (.[0].path|split("\\\\")[-1])}) | sort_by(-.users) | .[0:10]' /tmp/flat.json
}

queue_race() {
    local size=$1
    local token=$(get_token)
    
    local sources=$(jq --argjson size "$size" '[.[] | select(.size == $size)] | unique_by(.user) | sort_by(-.speed)' /tmp/flat.json)
    local num=$(echo "$sources" | jq 'length')
    
    echo ""
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  RACE MODE: QUEUEING $num SOURCES FOR SIZE $size"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    
    echo "$sources" | jq -c '.[]' | while read -r src; do
        local user=$(echo "$src" | jq -r '.user')
        local path=$(echo "$src" | jq -r '.path')
        local sz=$(echo "$src" | jq -r '.size')
        local speed=$(echo "$src" | jq -r '(.speed / 1024) | floor')
        
        local body=$(jq -n --arg p "$path" --argjson s "$sz" '[{filename: $p, size: $s}]')
        local result=$(curl -s -X POST "$API/api/v0/transfers/downloads/$user" \
            -H "Authorization: Bearer $token" \
            -H "Content-Type: application/json" \
            -d "$body" 2>/dev/null)
        
        if echo "$result" | jq -e '.enqueued[0]' > /dev/null 2>&1; then
            echo "✓ $user @ ${speed} KB/s"
        else
            echo "✗ $user"
        fi
    done
    
    echo ""
    echo "RACE STARTED! First to complete wins."
}

monitor() {
    local size=$1
    local token=$(get_token)
    local start=$(date +%s)
    
    while true; do
        local now=$(date +%s)
        local elapsed=$((now - start))
        
        local status=$(curl -s "$API/api/v0/transfers/downloads" -H "Authorization: Bearer $token")
        local items=$(echo "$status" | jq --argjson size "$size" '[.[] | .directories[].files[] | select(.size == $size)]')
        
        echo ""
        echo "[$elapsed s] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        
        echo "$items" | jq -r 'sort_by(-.percentComplete) | .[0:10] | .[] |
            if .state == "Completed, Succeeded" then "✅ \(.username): DONE @ \((.averageSpeed/1024)|floor) KB/s"
            elif .state | contains("Rejected") or contains("Errored") then "❌ \(.username): FAILED"
            elif .state | contains("InProgress") then "⏳ \(.username): \(.percentComplete|floor)% @ \((.averageSpeed/1024)|floor) KB/s"
            else "⏸️  \(.username): \(.state)"
            end'
        
        local ok=$(echo "$items" | jq '[.[] | select(.state == "Completed, Succeeded")] | length')
        local fail=$(echo "$items" | jq '[.[] | select(.state | contains("Rejected") or contains("Errored"))] | length')
        local total=$(echo "$items" | jq 'length')
        
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        echo "$ok ✅  $fail ❌  of $total"
        
        [ "$ok" -ge 10 ] && echo "🎉 10+ COMPLETED!" && break
        [ "$elapsed" -gt 180 ] && echo "⏱️ Timeout" && break
        
        sleep 3
    done
}

case "$1" in
    pool) show_pool ;;
    race) queue_race "${2:-21721524}" ;;
    monitor) monitor "${2:-21721524}" ;;
    *)
        echo "Usage: $0 <command> [size]"
        echo ""
        echo "Commands:"
        echo "  pool     - Show available files in pool"
        echo "  race     - Queue all sources for SIZE (default: 21721524)"
        echo "  monitor  - Monitor downloads for SIZE"
        echo ""
        echo "Example:"
        echo "  $0 pool"
        echo "  $0 race 21721524"
        echo "  $0 monitor 21721524"
        ;;
esac

