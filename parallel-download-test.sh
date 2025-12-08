#!/bin/bash
# Parallel Download Test with Auto-Replacement + Stall Detection
# - Replaces failed/rejected peers automatically
# - Detects stalled downloads (<20 KB/s for 30s) and replaces them
# - Tracks already-replaced downloads to avoid duplicate replacements

API="http://localhost:54321"
MAX_SOURCES=10
STALL_THRESHOLD_KBS=20
STALL_TIMEOUT_SEC=30

get_token() {
    curl -s -X POST "$API/api/v0/session" \
        -H "Content-Type: application/json" \
        -d '{"username":"slskd","password":"slskd"}' | jq -r '.token'
}

queue_download() {
    local token="$1" user="$2" path="$3" size="$4"
    local body=$(jq -n --arg p "$path" --argjson s "$size" '[{filename: $p, size: $s}]')
    curl -s -X POST "$API/api/v0/transfers/downloads/$user" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json" \
        -d "$body" | jq -e '.enqueued[0]' > /dev/null 2>&1
}

cancel_download() {
    local token="$1" id="$2"
    curl -s -X DELETE "$API/api/v0/transfers/downloads/$id?remove=true" \
        -H "Authorization: Bearer $token" > /dev/null
}

main() {
    local TOKEN=$(get_token)
    local TARGET_SIZE=${1:-43588734}  # Default: Get Lucky (41 MB)
    
    # Cleanup from previous runs
    rm -f /tmp/stall_* /tmp/completed_users.txt /tmp/replaced_ids.txt
    touch /tmp/replaced_ids.txt /tmp/completed_users.txt
    
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  PARALLEL DOWNLOAD WITH AUTO-REPLACE + STALL DETECTION           ║"
    echo "║  Stall: <${STALL_THRESHOLD_KBS} KB/s for ${STALL_TIMEOUT_SEC}s → replace                       ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    
    # Get ALL sources
    local ALL_SOURCES=$(jq --argjson size "$TARGET_SIZE" \
        '[.[] | select(.size == $size)] | unique_by(.user) | sort_by(-.speed)' \
        /tmp/flat.json)
    local TOTAL_AVAILABLE=$(echo "$ALL_SOURCES" | jq 'length')
    echo "Available sources: $TOTAL_AVAILABLE"
    
    # Track next unused source index
    echo "$MAX_SOURCES" > /tmp/unused_index.txt
    
    # Queue initial batch
    echo ""
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  QUEUEING INITIAL $MAX_SOURCES DOWNLOADS"
    echo "═══════════════════════════════════════════════════════════════════"
    
    for i in $(seq 0 $((MAX_SOURCES - 1))); do
        local src=$(echo "$ALL_SOURCES" | jq ".[$i]")
        local user=$(echo "$src" | jq -r '.user')
        local path=$(echo "$src" | jq -r '.path')
        local size=$(echo "$src" | jq -r '.size')
        local speed=$(echo "$src" | jq -r '(.speed / 1024) | floor')
        
        if queue_download "$TOKEN" "$user" "$path" "$size"; then
            echo "  ✓ $user @ ${speed} KB/s (reported)"
        else
            echo "  ✗ $user - queue failed"
        fi
    done
    
    echo ""
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  MONITORING (stall detection active)"
    echo "═══════════════════════════════════════════════════════════════════"
    
    local START=$(date +%s)
    local RETRY_COUNT=0
    
    while true; do
        sleep 3
        local NOW=$(date +%s)
        local ELAPSED=$((NOW - START))
        
        local STATUS=$(curl -s "$API/api/v0/transfers/downloads" -H "Authorization: Bearer $TOKEN")
        local ITEMS=$(echo "$STATUS" | jq --argjson size "$TARGET_SIZE" \
            '[.[] | .directories[].files[] | select(.size == $size)]')
        
        echo ""
        echo "[$ELAPSED s] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        
        local NEED_REPLACE=""
        
        # Process each download
        while IFS= read -r item; do
            [ -z "$item" ] && continue
            
            local user=$(echo "$item" | jq -r '.username')
            local state=$(echo "$item" | jq -r '.state')
            local pct=$(echo "$item" | jq -r '.percentComplete | floor')
            local speed=$(echo "$item" | jq -r '(.averageSpeed / 1024) | floor')
            local id=$(echo "$item" | jq -r '.id')
            
            # Skip if we already replaced this one
            if grep -q "^$id$" /tmp/replaced_ids.txt 2>/dev/null; then
                continue
            fi
            
            if [[ "$state" == "Completed, Succeeded" ]]; then
                echo "✅ $user: DONE @ ${speed} KB/s"
                grep -q "^$user$" /tmp/completed_users.txt 2>/dev/null || echo "$user" >> /tmp/completed_users.txt
                
            elif [[ "$state" == *"Rejected"* ]] || [[ "$state" == *"Errored"* ]]; then
                echo "❌ $user: FAILED → replacing"
                NEED_REPLACE="$NEED_REPLACE $user|$id|failed"
                echo "$id" >> /tmp/replaced_ids.txt
                
            elif [[ "$state" == *"InProgress"* ]]; then
                # Check for stall
                if [ "$speed" -lt "$STALL_THRESHOLD_KBS" ] && [ "$pct" -lt 95 ]; then
                    local stall_file="/tmp/stall_$user"
                    if [ -f "$stall_file" ]; then
                        local stall_start=$(cat "$stall_file")
                        local stall_duration=$((NOW - stall_start))
                        if [ "$stall_duration" -ge "$STALL_TIMEOUT_SEC" ]; then
                            echo "🐌 $user: ${pct}% @ ${speed} KB/s - STALLED ${stall_duration}s → replacing"
                            NEED_REPLACE="$NEED_REPLACE $user|$id|stalled"
                            echo "$id" >> /tmp/replaced_ids.txt
                            rm -f "$stall_file"
                        else
                            echo "⚠️  $user: ${pct}% @ ${speed} KB/s (slow ${stall_duration}s)"
                        fi
                    else
                        echo "$NOW" > "$stall_file"
                        echo "⏳ $user: ${pct}% @ ${speed} KB/s (watching...)"
                    fi
                else
                    rm -f "/tmp/stall_$user"
                    echo "⏳ $user: ${pct}% @ ${speed} KB/s"
                fi
                
            elif [[ "$state" == *"Queued"* ]]; then
                echo "⏸️  $user: queued"
            else
                echo "?  $user: $state"
            fi
        done < <(echo "$ITEMS" | jq -c '.[]')
        
        # Count results (excluding already-replaced)
        local OK=$(echo "$ITEMS" | jq '[.[] | select(.state == "Completed, Succeeded")] | length')
        local ACTIVE=$(echo "$ITEMS" | jq '[.[] | select(.state | contains("InProgress") or contains("Queued"))] | length')
        
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        echo "$OK ✅ complete,  $ACTIVE ⏳ active  (replacements: $RETRY_COUNT)"
        
        # Handle replacements
        local UNUSED_INDEX=$(cat /tmp/unused_index.txt)
        for replace_item in $NEED_REPLACE; do
            [ -z "$replace_item" ] && continue
            
            local failed_user=$(echo "$replace_item" | cut -d'|' -f1)
            local failed_id=$(echo "$replace_item" | cut -d'|' -f2)
            local reason=$(echo "$replace_item" | cut -d'|' -f3)
            
            # Cancel the failed download
            cancel_download "$TOKEN" "$failed_id"
            
            # Try to get an unused source first
            if [ "$UNUSED_INDEX" -lt "$TOTAL_AVAILABLE" ]; then
                local next_src=$(echo "$ALL_SOURCES" | jq ".[$UNUSED_INDEX]")
                local new_user=$(echo "$next_src" | jq -r '.user')
                local new_path=$(echo "$next_src" | jq -r '.path')
                local new_size=$(echo "$next_src" | jq -r '.size')
                
                echo "🔄 Replacing $failed_user ($reason) → NEW: $new_user"
                if queue_download "$TOKEN" "$new_user" "$new_path" "$new_size"; then
                    UNUSED_INDEX=$((UNUSED_INDEX + 1))
                    echo "$UNUSED_INDEX" > /tmp/unused_index.txt
                fi
            else
                # Use a completed user (they're idle now)
                local idle_user=$(head -1 /tmp/completed_users.txt 2>/dev/null)
                if [ -n "$idle_user" ]; then
                    local idle_src=$(echo "$ALL_SOURCES" | jq --arg u "$idle_user" '.[] | select(.user == $u)')
                    local idle_path=$(echo "$idle_src" | jq -r '.path')
                    local idle_size=$(echo "$idle_src" | jq -r '.size')
                    
                    echo "🔄 Replacing $failed_user ($reason) → IDLE: $idle_user"
                    queue_download "$TOKEN" "$idle_user" "$idle_path" "$idle_size"
                    # Rotate completed list
                    sed -i '1d' /tmp/completed_users.txt 2>/dev/null
                    echo "$idle_user" >> /tmp/completed_users.txt
                else
                    echo "⚠️  No replacement available for $failed_user"
                fi
            fi
            
            RETRY_COUNT=$((RETRY_COUNT + 1))
        done
        
        # Check completion - need MAX_SOURCES successful
        if [ "$OK" -ge "$MAX_SOURCES" ]; then
            echo ""
            echo "🎉 SUCCESS! $OK/$MAX_SOURCES downloads completed in ${ELAPSED}s"
            break
        fi
        
        # Timeout after 5 minutes
        if [ "$ELAPSED" -gt 300 ]; then
            echo ""
            echo "⏱️ TIMEOUT after 5 minutes ($OK/$MAX_SOURCES completed)"
            break
        fi
        
        # Max retries
        if [ "$RETRY_COUNT" -gt 30 ]; then
            echo ""
            echo "❌ Too many replacements ($RETRY_COUNT)"
            break
        fi
    done
    
    # Cleanup
    rm -f /tmp/stall_* /tmp/completed_users.txt /tmp/replaced_ids.txt /tmp/unused_index.txt
    
    # Final summary
    echo ""
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  FINAL RESULTS"
    echo "═══════════════════════════════════════════════════════════════════"
    curl -s "$API/api/v0/transfers/downloads" -H "Authorization: Bearer $TOKEN" | jq --argjson size "$TARGET_SIZE" '
        [.[] | .directories[].files[] | select(.size == $size)] |
        [.[] | select(.state == "Completed, Succeeded")] |
        sort_by(-.averageSpeed) |
        map({
            user: .username,
            speed_kbps: ((.averageSpeed / 1024) | floor),
            time: .elapsedTime
        })
    '
}

main "$@"
