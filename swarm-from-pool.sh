#!/bin/bash
# SWARM FROM POOL - Use flat.json directly, no searching!
# Calls the MultiSource.DownloadAsync with pre-built sources

API="http://localhost:54321"
POOL="/tmp/flat.json"

get_token() {
    curl -s -X POST "$API/api/v0/session" \
        -H "Content-Type: application/json" \
        -d '{"username":"slskd","password":"slskd"}' | jq -r '.token'
}

# Get sources from pool for a specific size
get_sources_json() {
    local size=$1
    local limit=${2:-50}
    
    jq --argjson size "$size" --argjson limit "$limit" '
        [.[] | select(.size == $size)] | 
        unique_by(.user) | 
        sort_by(-.speed) | 
        .[:$limit] |
        map({username: .user, fullPath: .path})
    ' "$POOL"
}

run_swarm() {
    local size=$1
    local chunk_size=${2:-262144}  # 256KB default
    local token=$(get_token)
    
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  SWARM FROM POOL - No searching, direct from flat.json           ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    
    # Get sources from pool
    local sources=$(get_sources_json "$size" 40)
    local num_sources=$(echo "$sources" | jq 'length')
    local num_chunks=$(( (size + chunk_size - 1) / chunk_size ))
    
    echo "File size: $size bytes ($((size / 1024 / 1024)) MB)"
    echo "Chunk size: $chunk_size bytes ($((chunk_size / 1024)) KB)"
    echo "Total chunks: $num_chunks"
    echo "Sources from pool: $num_sources"
    echo ""
    
    echo "Top 10 sources:"
    echo "$sources" | jq -r '.[0:10] | .[] | "  \(.username)"'
    echo ""
    
    # Build request - call the download endpoint directly
    local first_path=$(echo "$sources" | jq -r '.[0].fullPath')
    local output_path="/tmp/slskdn-swarm/$(date +%Y%m%d_%H%M%S)_swarm_test.flac"
    
    echo "Starting swarm download..."
    echo "Output: $output_path"
    echo ""
    
    # Call the multi-source download API
    local request=$(jq -n \
        --arg filename "$first_path" \
        --argjson fileSize "$size" \
        --arg outputPath "$output_path" \
        --argjson chunkSize "$chunk_size" \
        --argjson sources "$sources" \
        '{
            filename: $filename,
            fileSize: $fileSize,
            outputPath: $outputPath,
            chunkSize: $chunkSize,
            sources: $sources
        }')
    
    echo "Request:"
    echo "$request" | jq '{fileSize, chunkSize, sourcesCount: (.sources | length)}'
    echo ""
    
    # Call the download endpoint
    echo "Calling /api/v0/multisource/download..."
    local result=$(curl -s --max-time 300 -X POST "$API/api/v0/multisource/download" \
        -H "Authorization: Bearer $token" \
        -H "Content-Type: application/json" \
        -d "$request")
    
    echo ""
    echo "=== RESULT ==="
    echo "$result" | jq '.'
}

show_targets() {
    echo "=== AVAILABLE TARGETS IN POOL ==="
    jq 'group_by(.size) | 
        map({size: .[0].size, mb: (.[0].size/1048576|floor), users: ([.[].user]|unique|length), sample: (.[0].path|split("\\\\")[-1])}) | 
        sort_by(-.users) | 
        .[0:20] |
        .[] | "[\(.users) sources] \(.sample) (\(.mb) MB) - size: \(.size)"
    ' -r "$POOL"
}

case "$1" in
    swarm) run_swarm "$2" "${3:-262144}" ;;
    targets) show_targets ;;
    *)
        echo "Usage: $0 <command> [args]"
        echo ""
        echo "Commands:"
        echo "  targets              - Show available files in pool"
        echo "  swarm SIZE [CHUNK]   - Run swarm download for SIZE with CHUNK size (default 256KB)"
        echo ""
        echo "Example:"
        echo "  $0 targets"
        echo "  $0 swarm 21721524           # 256KB chunks"
        echo "  $0 swarm 21721524 131072    # 128KB chunks"
        ;;
esac

