// <copyright file="TransportPerformanceBenchmarks.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Runtimes;
using Microsoft.Extensions.Logging;
using slskd.Common.Security;
using Xunit;
using Xunit.Abstractions;

namespace slskd.Tests.Performance.Security;

/// <summary>
/// Performance benchmarks for slskdN transport implementations.
/// Measures latency, throughput, and resource usage.
/// </summary>
[Config(typeof(TransportBenchmarkConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class TransportPerformanceBenchmarks
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;

    // Benchmark payloads
    private readonly byte[] _smallPayload = Encoding.UTF8.GetBytes("Hello World!");
    private readonly byte[] _mediumPayload = Encoding.UTF8.GetBytes(new string('x', 1024)); // 1KB
    private readonly byte[] _largePayload = Encoding.UTF8.GetBytes(new string('x', 65536)); // 64KB

    // Mock transports for testing
    private DirectTransport? _directTransport;
    private WebSocketTransport? _webSocketTransport;
    private HttpTunnelTransport? _httpTunnelTransport;

    public TransportPerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        _logger = new XunitLogger(_output);
    }

    [GlobalSetup]
    public void Setup()
    {
        // Initialize mock transports
        _directTransport = new DirectTransport((ILogger<DirectTransport>)_logger);

        var webSocketOptions = new WebSocketOptions
        {
            ServerUrl = "ws://127.0.0.1:12345/mock", // Will fail but for benchmark structure
            SubProtocol = "benchmark"
        };
        _webSocketTransport = new WebSocketTransport(webSocketOptions, (ILogger<WebSocketTransport>)_logger);

        var httpTunnelOptions = new HttpTunnelOptions
        {
            ProxyUrl = "http://127.0.0.1:12346/mock" // Will fail but for benchmark structure
        };
        _httpTunnelTransport = new HttpTunnelTransport(httpTunnelOptions, (ILogger<HttpTunnelTransport>)_logger);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Latency")]
    public async Task DirectTransport_ConnectionLatency()
    {
        if (_directTransport == null) return;

        var sw = Stopwatch.StartNew();
        try
        {
            await _directTransport.ConnectAsync("127.0.0.1", 12345);
        }
        catch
        {
            // Expected to fail - we're measuring connection attempt latency
        }
        sw.Stop();

        _output.WriteLine($"Direct connection attempt: {sw.Elapsed.TotalMilliseconds}ms");
    }

    [Benchmark]
    [BenchmarkCategory("Latency")]
    public async Task WebSocketTransport_ConnectionLatency()
    {
        if (_webSocketTransport == null) return;

        var sw = Stopwatch.StartNew();
        try
        {
            await _webSocketTransport.ConnectAsync("127.0.0.1", 12345);
        }
        catch
        {
            // Expected to fail - we're measuring connection attempt latency
        }
        sw.Stop();

        _output.WriteLine($"WebSocket connection attempt: {sw.Elapsed.TotalMilliseconds}ms");
    }

    [Benchmark]
    [BenchmarkCategory("Latency")]
    public async Task HttpTunnelTransport_ConnectionLatency()
    {
        if (_httpTunnelTransport == null) return;

        var sw = Stopwatch.StartNew();
        try
        {
            await _httpTunnelTransport.ConnectAsync("127.0.0.1", 12345);
        }
        catch
        {
            // Expected to fail - we're measuring connection attempt latency
        }
        sw.Stop();

        _output.WriteLine($"HTTP Tunnel connection attempt: {sw.Elapsed.TotalMilliseconds}ms");
    }

    [Benchmark]
    [BenchmarkCategory("Throughput")]
    [Arguments(100)] // 100 iterations
    public async Task TransportSelector_SelectionThroughput(int iterations)
    {
        var adversarialOptions = new AdversarialOptions
        {
            AnonymityLayer = new AnonymityLayerOptions { Enabled = false }
        };

        var selector = new AnonymityTransportSelector(
            adversarialOptions,
            new Mesh.Transport.TransportPolicyManager(),
            (ILogger<AnonymityTransportSelector>)_logger);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                await selector.SelectAndConnectAsync($"peer{i}", null, "127.0.0.1", 12345);
            }
            catch
            {
                // Expected failures - measuring selection throughput
            }
        }
        sw.Stop();

        var throughput = iterations / sw.Elapsed.TotalSeconds;
        _output.WriteLine($"Transport selection throughput: {throughput:F2} selections/sec");
    }

    [Benchmark]
    [BenchmarkCategory("Memory")]
    public void TransportCreation_MemoryUsage()
    {
        var before = GC.GetTotalMemory(true);

        // Create multiple transport instances
        var transports = new List<IAnonymityTransport>();
        for (int i = 0; i < 100; i++)
        {
            var options = new WebSocketOptions { ServerUrl = $"ws://test{i}.example.com" };
            transports.Add(new WebSocketTransport(options, (ILogger<WebSocketTransport>)_logger));
        }

        var after = GC.GetTotalMemory(false);
        var memoryUsed = after - before;

        _output.WriteLine($"Memory usage for 100 WebSocket transports: {memoryUsed / 1024.0:F2} KB");

        // Cleanup
        foreach (var transport in transports)
        {
            if (transport is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Concurrency")]
    [Arguments(10)] // 10 concurrent operations
    public async Task ConcurrentTransportOperations(int concurrencyLevel)
    {
        var adversarialOptions = new AdversarialOptions
        {
            AnonymityLayer = new AnonymityLayerOptions { Enabled = false }
        };

        var selector = new AnonymityTransportSelector(
            adversarialOptions,
            new Mesh.Transport.TransportPolicyManager(),
            (ILogger<AnonymityTransportSelector>)_logger);

        var tasks = new List<Task>();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < concurrencyLevel; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await selector.SelectAndConnectAsync($"peer{i}", null, "127.0.0.1", 12345);
                }
                catch
                {
                    // Expected failures
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        _output.WriteLine($"Concurrent operations ({concurrencyLevel}): {sw.Elapsed.TotalMilliseconds}ms total");
    }

    [Benchmark]
    [BenchmarkCategory("Serialization")]
    public void PayloadProcessing_Overhead()
    {
        var privacyLayer = new PrivacyLayer(
            new AdversarialOptions
            {
                Privacy = new PrivacyLayerOptions
                {
                    Enabled = true,
                    Padding = new MessagePaddingOptions { Enabled = true, BucketSizes = new List<int> { 512, 1024 } },
                    Timing = new TimingObfuscationOptions { Enabled = false }, // Disable timing for pure processing test
                    Batching = new MessageBatchingOptions { Enabled = false },
                    CoverTraffic = new CoverTrafficOptions { Enabled = false }
                }
            },
            new BucketPadder(new MessagePaddingOptions { BucketSizes = new List<int> { 512, 1024 } }, (ILogger<BucketPadder>)_logger),
            new RandomJitterObfuscator(new TimingObfuscationOptions { Enabled = false }, (ILogger<RandomJitterObfuscator>)_logger),
            new TimedBatcher(new MessageBatchingOptions { Enabled = false }, (ILogger<TimedBatcher>)_logger),
            new CoverTrafficGenerator(new CoverTrafficOptions { Enabled = false }, null, (ILogger<CoverTrafficGenerator>)_logger),
            (ILogger<PrivacyLayer>)_logger);

        var iterations = 1000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var result = privacyLayer.ApplyOutboundTransformsAsync(_mediumPayload).Result;
        }

        sw.Stop();

        var throughput = iterations / sw.Elapsed.TotalSeconds;
        _output.WriteLine($"Privacy layer processing: {throughput:F2} payloads/sec");
    }

    [Benchmark]
    [BenchmarkCategory("ErrorHandling")]
    [Arguments(100)]
    public async Task TransportErrorRecovery_Time(int errorCount)
    {
        var adversarialOptions = new AdversarialOptions
        {
            AnonymityLayer = new AnonymityLayerOptions { Enabled = false }
        };

        var selector = new AnonymityTransportSelector(
            adversarialOptions,
            new Mesh.Transport.TransportPolicyManager(),
            (ILogger<AnonymityTransportSelector>)_logger);

        var sw = Stopwatch.StartNew();
        var errorsEncountered = 0;

        for (int i = 0; i < errorCount; i++)
        {
            try
            {
                await selector.SelectAndConnectAsync($"peer{i}", null, "127.0.0.1", 12345);
            }
            catch
            {
                errorsEncountered++;
            }
        }

        sw.Stop();

        _output.WriteLine($"Error recovery: {errorsEncountered} errors in {sw.Elapsed.TotalMilliseconds}ms");
    }

    /// <summary>
    /// Benchmark configuration for transport performance testing.
    /// </summary>
    public class TransportBenchmarkConfig : ManualConfig
    {
        public TransportBenchmarkConfig()
        {
            AddJob(Job.Default
                .WithRuntime(CoreRuntime.Core80)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(10));

            AddDiagnoser(MemoryDiagnoser.Default);
            AddDiagnoser(ThreadingDiagnoser.Default);

            // Categories for different types of benchmarks
            AddColumnProvider(DefaultColumnProviders.Metrics);
            AddColumnProvider(DefaultColumnProviders.Statistics);
        }
    }

    /// <summary>
    /// Mock direct transport for benchmarking baseline.
    /// </summary>
    private class DirectTransport : IAnonymityTransport
    {
        private readonly ILogger<DirectTransport> _logger;

        public DirectTransport(ILogger<DirectTransport> logger)
        {
            _logger = logger;
        }

        public AnonymityTransportType TransportType => AnonymityTransportType.Direct;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            return await ConnectAsync(host, port, null, cancellationToken);
        }

        public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
        {
            // Simulate connection attempt
            await Task.Delay(1, cancellationToken); // Minimal delay for benchmark
            throw new Exception("Mock connection failure for benchmarking");
        }

        public AnonymityTransportStatus GetStatus()
        {
            return new AnonymityTransportStatus
            {
                IsAvailable = true,
                ActiveConnections = 0,
                TotalConnectionsAttempted = 0,
                TotalConnectionsSuccessful = 0
            };
        }
    }

    /// <summary>
    /// Xunit logger implementation for benchmarks.
    /// </summary>
    private class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}

// Additional benchmark classes for specific transport comparisons

/// <summary>
/// Benchmarks comparing transport initialization performance.
/// </summary>
public class TransportInitializationBenchmarks
{
    [Benchmark]
    public void WebSocketTransport_Initialization()
    {
        var options = new WebSocketOptions { ServerUrl = "ws://test.example.com" };
        using var transport = new WebSocketTransport(options, NullLogger<WebSocketTransport>.Instance);
    }

    [Benchmark]
    public void HttpTunnelTransport_Initialization()
    {
        var options = new HttpTunnelOptions { ProxyUrl = "http://test.example.com" };
        using var transport = new HttpTunnelTransport(options, NullLogger<HttpTunnelTransport>.Instance);
    }

    [Benchmark]
    public void Obfs4Transport_Initialization()
    {
        var options = new Obfs4Options { Obfs4ProxyPath = "/usr/bin/obfs4proxy" };
        using var transport = new Obfs4Transport(options, NullLogger<Obfs4Transport>.Instance);
    }

    [Benchmark]
    public void MeekTransport_Initialization()
    {
        var options = new MeekOptions { BridgeUrl = "https://test.example.com" };
        using var transport = new MeekTransport(options, NullLogger<MeekTransport>.Instance);
    }
}

/// <summary>
/// Benchmarks measuring status checking performance.
/// </summary>
public class TransportStatusBenchmarks
{
    private WebSocketTransport? _webSocketTransport;
    private HttpTunnelTransport? _httpTunnelTransport;
    private Obfs4Transport? _obfs4Transport;
    private MeekTransport? _meekTransport;

    [GlobalSetup]
    public void Setup()
    {
        _webSocketTransport = new WebSocketTransport(
            new WebSocketOptions { ServerUrl = "ws://127.0.0.1:12345" },
            NullLogger<WebSocketTransport>.Instance);

        _httpTunnelTransport = new HttpTunnelTransport(
            new HttpTunnelOptions { ProxyUrl = "http://127.0.0.1:12346" },
            NullLogger<HttpTunnelTransport>.Instance);

        _obfs4Transport = new Obfs4Transport(
            new Obfs4Options { Obfs4ProxyPath = "/usr/bin/obfs4proxy" },
            NullLogger<Obfs4Transport>.Instance);

        _meekTransport = new MeekTransport(
            new MeekOptions { BridgeUrl = "http://127.0.0.1:12347" },
            NullLogger<MeekTransport>.Instance);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _webSocketTransport?.Dispose();
        _httpTunnelTransport?.Dispose();
        _obfs4Transport?.Dispose();
        _meekTransport?.Dispose();
    }

    [Benchmark]
    public void WebSocketTransport_StatusCheck()
    {
        _webSocketTransport?.GetStatus();
    }

    [Benchmark]
    public void HttpTunnelTransport_StatusCheck()
    {
        _httpTunnelTransport?.GetStatus();
    }

    [Benchmark]
    public void Obfs4Transport_StatusCheck()
    {
        _obfs4Transport?.GetStatus();
    }

    [Benchmark]
    public void MeekTransport_StatusCheck()
    {
        _meekTransport?.GetStatus();
    }
}

