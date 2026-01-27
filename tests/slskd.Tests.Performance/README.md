# Performance Benchmarking Suite

Comprehensive performance benchmarks for slskdN components.

## Overview

This test project contains BenchmarkDotNet-based performance benchmarks for:

- **HashDb**: Database query performance, caching effectiveness
- **Swarm Downloads**: Chunk optimization, peer selection, scheduling
- **API Endpoints**: Response times, throughput, concurrency
- **Transport Layer**: Network transport performance (Security)

## Running Benchmarks

### All Benchmarks

```bash
dotnet run --project tests/slskd.Tests.Performance --configuration Release
```

### Specific Benchmark Class

```bash
# HashDb benchmarks
dotnet run --project tests/slskd.Tests.Performance --configuration Release --filter "*HashDbPerformanceBenchmarks*"

# Swarm benchmarks
dotnet run --project tests/slskd.Tests.Performance --configuration Release --filter "*SwarmPerformanceBenchmarks*"

# API benchmarks (requires running slskdn instance)
export SLSKDN_TEST_URL=http://localhost:5000
dotnet run --project tests/slskd.Tests.Performance --configuration Release --filter "*ApiPerformanceBenchmarks*"
```

### Using BenchmarkDotNet Runner

```bash
# Run specific benchmark class
dotnet run --project tests/slskd.Tests.Performance --configuration Release -- \
  --filter "slskd.Tests.Performance.HashDb.HashDbPerformanceBenchmarks"

# Run with specific job configuration
dotnet run --project tests/slskd.Tests.Performance --configuration Release -- \
  --job short
```

## Benchmark Categories

### HashDb Benchmarks

- **Lookup**: Single hash lookups (with/without cache, cache hits)
- **Query**: Size-based queries, sequential/parallel lookups
- **Write**: Single and batch hash storage
- **Stats**: Statistics retrieval

### Swarm Benchmarks

- **Optimization**: Chunk size optimization for various file sizes and peer counts
- **Scheduling**: Chunk assignment (sequential and parallel)
- **Selection**: Peer selection based on metrics

### API Benchmarks

- **Read**: GET endpoint performance (session, application state, HashDb stats, jobs)
- **Write**: POST endpoint performance (create search)
- **Concurrency**: Concurrent request handling

### Transport Benchmarks

- **Latency**: Connection latency for different transport types
- **Throughput**: Transport selection and processing throughput
- **Memory**: Memory usage for transport creation
- **Concurrency**: Concurrent transport operations

## Benchmark Results

Results are displayed in the console and can be exported to:

- **Markdown**: `BenchmarkDotNet.Artifacts/results/*.md`
- **CSV**: `BenchmarkDotNet.Artifacts/results/*.csv`
- **HTML**: `BenchmarkDotNet.Artifacts/results/*.html`

## Performance Targets

### HashDb

- **Lookup (no cache)**: < 10ms for single lookup
- **Lookup (cache hit)**: < 1ms
- **Size query**: < 50ms for 1000 entries
- **Batch write**: < 100ms for 100 entries

### Swarm

- **Chunk optimization**: < 5ms for recommendation
- **Peer selection**: < 10ms for 100 candidates
- **Chunk assignment**: < 50ms for 100 chunks

### API

- **GET endpoints**: < 50ms response time
- **POST endpoints**: < 100ms response time
- **Concurrent requests**: Handle 100+ concurrent requests

## Continuous Integration

Benchmarks can be run in CI to detect performance regressions:

```bash
# Run benchmarks and compare to baseline
dotnet run --project tests/slskd.Tests.Performance --configuration Release -- \
  --filter "*" \
  --exporters json \
  --baseline
```

## Notes

- **API Benchmarks**: Require a running slskdn instance. Set `SLSKDN_TEST_URL` environment variable or use default `http://localhost:5000`.
- **HashDb Benchmarks**: Create temporary databases in system temp directory.
- **Swarm Benchmarks**: Use mock data and don't require network connectivity.
- **Transport Benchmarks**: Use mock transports and measure protocol overhead.

## Best Practices

1. **Run in Release mode**: Always use `--configuration Release` for accurate results
2. **Warmup**: Benchmarks include warmup iterations automatically
3. **Multiple runs**: Run benchmarks multiple times to account for variance
4. **Baseline comparison**: Use `--baseline` to compare against previous runs
5. **CI integration**: Run benchmarks on performance-critical changes

---

**See Also:**
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Performance Testing Guide](../docs/dev/e2e-testing-guide.md)
