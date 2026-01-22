#!/bin/bash
# BUILD POOL - Search for popular tracks and build /tmp/flat.json
# This creates the pool of sources for race-download.sh

API="http://localhost:54321"
POOL_FILE="/tmp/flat.json"
SEARCHES_FILE="/tmp/searches.json"

# Popular albums with high share counts (skip Pink Floyd - always times out)
SEARCHES=(
    "daft punk random access memories flac"
    "nirvana nevermind flac"
    "radiohead in rainbows flac"
    "kendrick lamar damn flac"
    "arctic monkeys am flac"
    "tame impala currents flac"
    "tyler creator igor flac"
    "kanye west graduation flac"
)

# Timeouts
SEARCH_TIMEOUT=45
CURL_TIMEOUT=10

get_token() {
    curl -s -X POST "$API/api/v0/session" \
        -H "Content-Type: application/json" \
        -d '{"username":"slskd","password":"slskd"}' | jq -r '.token'
}

search_and_wait() {
    local query="$1"
    local token="$2"
    local timeout="${SEARCH_TIMEOUT:-45}"
    
    echo "Searching: $query (${timeout}s timeout)"
    
    local search_id=$(curl -s --max-time $CURL_TIMEOUT -X POST "$API/api/v0/searches" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json" \
        -d "{\"searchText\":\"$query\",\"responseLimit\":2000,\"searchTimeout\":${timeout}000}" | jq -r '.id // empty')
    
    if [ -z "$search_id" ]; then
        echo "  FAILED to start search"
        return 1
    fi
    
    echo "  ID: $search_id"
    
    # Wait for completion with timeout
    local waited=0
    while [ $waited -lt $((timeout + 10)) ]; do
        local state=$(curl -s --max-time $CURL_TIMEOUT "$API/api/v0/searches/$search_id" \
            -H "Authorization: Bearer $token" | jq -r '.state // "Unknown"')
        
        if [[ "$state" == *"Completed"* ]]; then
            break
        fi
        sleep 2
        waited=$((waited + 2))
        printf "."
    done
    echo ""
    
    local info=$(curl -s --max-time $CURL_TIMEOUT "$API/api/v0/searches/$search_id" -H "Authorization: Bearer $token")
    local responses=$(echo "$info" | jq '.responseCount // 0')
    local files=$(echo "$info" | jq '.fileCount // 0')
    echo "  Found: $responses responses, $files files"
    
    echo "$search_id"
}

build_pool() {
    local token=$(get_token)
    
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  BUILDING POOL - Searching for popular tracks                    ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    
    # Initialize pool
    echo "[]" > "$POOL_FILE"
    echo "[]" > "$SEARCHES_FILE"
    
    for query in "${SEARCHES[@]}"; do
        echo ""
        echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        
        local search_id=$(search_and_wait "$query" "$token" 60)
        
        # Get responses
        local responses=$(curl -s "$API/api/v0/searches/$search_id/responses" \
            -H "Authorization: Bearer $token")
        
        # Flatten FLAC files with metadata
        local flat=$(echo "$responses" | jq --arg q "$query" '
            [.[] | .username as $user | .uploadSpeed as $speed | .hasFreeUploadSlot as $free |
             .files[] | select(.filename | test("\\.flac$"; "i")) | 
             {user: $user, speed: $speed, free: $free, path: .filename, size: .size, query: $q}
            ]
        ')
        
        local count=$(echo "$flat" | jq 'length')
        echo "  FLAC files: $count"
        
        # Merge into pool
        jq -s '.[0] + .[1]' "$POOL_FILE" <(echo "$flat") > /tmp/pool_merge.json
        mv /tmp/pool_merge.json "$POOL_FILE"
        
        # Save search metadata
        jq -s --arg q "$query" --arg id "$search_id" '.[0] + [{query: $q, id: $id, timestamp: now | todate}]' \
            "$SEARCHES_FILE" <(echo "[]") > /tmp/searches_merge.json
        mv /tmp/searches_merge.json "$SEARCHES_FILE"
        
        echo "  Pool total: $(jq 'length' $POOL_FILE)"
    done
    
    echo ""
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  POOL COMPLETE                                                   ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    show_pool_summary
}

show_pool_summary() {
    echo "=== POOL SUMMARY ==="
    echo "Total files: $(jq 'length' $POOL_FILE)"
    echo ""
    echo "=== TOP 20 FILES BY SOURCE COUNT ==="
    jq 'group_by(.size) | 
        map({
            size: .[0].size, 
            mb: (.[0].size/1048576|floor), 
            users: ([.[].user]|unique|length),
            query: .[0].query,
            sample: (.[0].path|split("\\\\")[-1])
        }) | 
        sort_by(-.users) | 
        .[0:20] |
        .[] | 
        "[\(.users) sources] \(.sample) (\(.mb) MB) - \(.query)"
    ' -r "$POOL_FILE"
}

add_search() {
    local query="$1"
    local token=$(get_token)
    
    echo "Adding search: $query"
    local search_id=$(search_and_wait "$query" "$token" 60)
    
    local responses=$(curl -s "$API/api/v0/searches/$search_id/responses" \
        -H "Authorization: Bearer $token")
    
    local flat=$(echo "$responses" | jq --arg q "$query" '
        [.[] | .username as $user | .uploadSpeed as $speed | .hasFreeUploadSlot as $free |
         .files[] | select(.filename | test("\\.flac$"; "i")) | 
         {user: $user, speed: $speed, free: $free, path: .filename, size: .size, query: $q}
        ]
    ')
    
    local count=$(echo "$flat" | jq 'length')
    echo "  FLAC files: $count"
    
    # Merge into pool (dedupe by user+size)
    jq -s '.[0] + .[1] | unique_by(.user + "|" + (.size|tostring))' "$POOL_FILE" <(echo "$flat") > /tmp/pool_merge.json
    mv /tmp/pool_merge.json "$POOL_FILE"
    
    echo "  Pool total: $(jq 'length' $POOL_FILE)"
    echo ""
    show_pool_summary
}

restore_pool() {
    local backup="$1"
    if [ -f "$backup" ]; then
        cp "$backup" "$POOL_FILE"
        echo "Restored pool from $backup"
        show_pool_summary
    else
        echo "Backup not found: $backup"
    fi
}

case "$1" in
    build) build_pool ;;
    add) add_search "$2" ;;
    summary) show_pool_summary ;;
    restore) restore_pool "${2:-/home/keith/Documents/Code/slskdn/test-data/pool-backup.json}" ;;
    save) 
        cp "$POOL_FILE" "/home/keith/Documents/Code/slskdn/test-data/pool-backup.json"
        echo "Saved pool to test-data/pool-backup.json"
        ;;
    *)
        echo "Usage: $0 <command> [args]"
        echo ""
        echo "Commands:"
        echo "  build    - Build fresh pool from all searches"
        echo "  add      - Add a search to existing pool"  
        echo "  summary  - Show pool summary"
        echo "  save     - Save pool to test-data/pool-backup.json"
        echo "  restore  - Restore pool from backup"
        echo ""
        echo "Example:"
        echo "  $0 build"
        echo "  $0 add 'queen greatest hits flac'"
        echo "  $0 summary"
        echo "  $0 save"
        echo "  $0 restore"
        ;;
esac

