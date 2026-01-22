#!/bin/bash
# Swarm Download Test - Work Queue Style
# All peers grab from a shared pool of tiny chunks

API="http://localhost:54321"
CHUNK_SIZE=$((256 * 1024))  # 256 KB chunks

get_token() {
    curl -s -X POST "$API/api/v0/session" \
        -H "Content-Type: application/json" \
        -d '{"username":"slskd","password":"slskd"}' | jq -r '.token'
}

main() {
    local TOKEN=$(get_token)
    local TARGET_SIZE=${1:-21721524}  # Default: Within (20 MB)
    
    # Calculate chunks
    local NUM_CHUNKS=$(( (TARGET_SIZE + CHUNK_SIZE - 1) / CHUNK_SIZE ))
    local CHUNK_SIZE_KB=$((CHUNK_SIZE / 1024))
    
    echo "╔═══════════════════════════════════════════════════════════════════╗"
    echo "║  SWARM DOWNLOAD - WORK QUEUE STYLE                               ║"
    echo "╚═══════════════════════════════════════════════════════════════════╝"
    echo ""
    echo "File size: $((TARGET_SIZE / 1024)) KB"
    echo "Chunk size: ${CHUNK_SIZE_KB} KB"
    echo "Total chunks: $NUM_CHUNKS"
    echo ""
    
    # Get available sources
    local SOURCES=$(jq --argjson size "$TARGET_SIZE" \
        '[.[] | select(.size == $size)] | unique_by(.user) | sort_by(-.speed) | .[0:10]' \
        /tmp/flat.json)
    local NUM_SOURCES=$(echo "$SOURCES" | jq 'length')
    
    echo "Available peers: $NUM_SOURCES"
    echo "$SOURCES" | jq -r '.[0:5] | .[] | "  \(.user) @ \((.speed / 1024) | floor) KB/s"'
    echo "  ..."
    echo ""
    
    # Create chunk queue
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  CHUNK QUEUE"
    echo "═══════════════════════════════════════════════════════════════════"
    
    # Initialize chunk status array
    declare -A CHUNK_STATUS  # pending, assigned:user, complete:user, failed
    declare -A PEER_CHUNKS   # user -> count of chunks completed
    
    for i in $(seq 0 $((NUM_CHUNKS - 1))); do
        local START=$((i * CHUNK_SIZE))
        local END=$((START + CHUNK_SIZE - 1))
        [ $END -ge $TARGET_SIZE ] && END=$((TARGET_SIZE - 1))
        echo "  Chunk $i: bytes $START-$END ($((END - START + 1)) bytes)"
        CHUNK_STATUS[$i]="pending"
    done | head -10
    echo "  ... (showing first 10 of $NUM_CHUNKS)"
    
    echo ""
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  SIMULATION: How work would be distributed"
    echo "═══════════════════════════════════════════════════════════════════"
    echo ""
    echo "With $NUM_CHUNKS chunks and $NUM_SOURCES peers:"
    echo ""
    
    # Simulate based on reported speeds
    local TOTAL_SPEED=0
    while IFS= read -r src; do
        local speed=$(echo "$src" | jq -r '.speed')
        TOTAL_SPEED=$((TOTAL_SPEED + speed))
    done < <(echo "$SOURCES" | jq -c '.[]')
    
    echo "Estimated distribution (by reported speed):"
    while IFS= read -r src; do
        local user=$(echo "$src" | jq -r '.user')
        local speed=$(echo "$src" | jq -r '.speed')
        local share=$((speed * 100 / TOTAL_SPEED))
        local chunks=$((NUM_CHUNKS * speed / TOTAL_SPEED))
        local bars=$(printf '█%.0s' $(seq 1 $((share / 5 + 1))))
        printf "  %-15s %3d%% %s (~%d chunks)\n" "$user" "$share" "$bars" "$chunks"
    done < <(echo "$SOURCES" | jq -c '.[]')
    
    echo ""
    echo "Total time estimate: $((TARGET_SIZE / TOTAL_SPEED))s (parallel)"
    echo "vs single source:    $((TARGET_SIZE / $(echo "$SOURCES" | jq '.[0].speed')))s"
    echo ""
    
    # Show what a real implementation would look like
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  IMPLEMENTATION APPROACH"
    echo "═══════════════════════════════════════════════════════════════════"
    echo ""
    cat << 'EOF'
    1. Create chunk queue (ConcurrentQueue<ChunkInfo>)
    2. For each peer, spawn a worker task:
       while (queue.TryDequeue(out chunk)) {
           try {
               await DownloadChunk(peer, chunk);
               chunk.Status = Complete;
           } catch {
               queue.Enqueue(chunk);  // Re-queue for another peer
           }
       }
    3. Wait for all workers to finish
    4. Reassemble chunks into final file
    
    Benefits:
    - Fast peers naturally do more work
    - Failed chunks automatically retry on other peers
    - No coordination needed - just a shared queue
    - Scales with number of peers
EOF
    
    echo ""
    echo "═══════════════════════════════════════════════════════════════════"
    echo "  LIMITATIONS"
    echo "═══════════════════════════════════════════════════════════════════"
    echo ""
    echo "  1. Soulseek doesn't support true partial downloads"
    echo "     → We use LimitedWriteStream to cancel after N bytes"
    echo "     → Remote peer sends full file, we just don't save it all"
    echo "     → WASTES BANDWIDTH on remote side!"
    echo ""
    echo "  2. One download per file per user at a time"
    echo "     → Can't have same user doing chunk 0 and chunk 1 in parallel"
    echo "     → Must wait for user's current chunk before assigning next"
    echo ""
    echo "  3. Connection overhead"
    echo "     → Each chunk = new connection handshake"
    echo "     → Chunks too small = overhead dominates"
    echo "     → Sweet spot: 256KB-1MB chunks"
}

main "$@"

