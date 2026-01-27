// <copyright file="BridgePerformanceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.VirtualSoulfind.Bridge;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.VirtualSoulfind.Bridge.Protocol;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Performance and load tests for bridge proxy server.
/// Tests concurrent connections, message throughput, memory usage, and latency.
/// </summary>
[Trait("Category", "L2-Bridge-Performance")]
public class BridgePerformanceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper output;
    private readonly ILoggerFactory loggerFactory;
    private SoulseekProtocolParser parser;

    public BridgePerformanceTests(ITestOutputHelper output)
    {
        this.output = output;
        loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        parser = new SoulseekProtocolParser(loggerFactory.CreateLogger<SoulseekProtocolParser>());
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ProtocolParser_Should_Handle_Concurrent_Reads()
    {
        // Arrange
        const int concurrentStreams = 10;
        const int messagesPerStream = 100;
        var tasks = new List<Task>();

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < concurrentStreams; i++)
        {
            var streamIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                var stream = CreateTestMessageStream(messagesPerStream);
                for (int j = 0; j < messagesPerStream; j++)
                {
                    var message = await parser.ReadMessageAsync(stream);
                    Assert.NotNull(message);
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var totalMessages = concurrentStreams * messagesPerStream;
        var messagesPerSecond = totalMessages / sw.Elapsed.TotalSeconds;
        output.WriteLine($"Processed {totalMessages} messages in {sw.Elapsed.TotalSeconds:F2}s ({messagesPerSecond:F0} msg/s)");
        Assert.True(messagesPerSecond > 1000, $"Expected >1000 msg/s, got {messagesPerSecond:F0}");
    }

    [Fact]
    public async Task ProtocolParser_Should_Handle_Concurrent_Writes()
    {
        // Arrange
        const int concurrentWriters = 10;
        const int messagesPerWriter = 100;
        var tasks = new List<Task>();

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < concurrentWriters; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stream = new MemoryStream();
                for (int j = 0; j < messagesPerWriter; j++)
                {
                    var payload = BuildLoginPayload($"user{j}", "password");
                    await parser.WriteMessageAsync(stream, SoulseekProtocolParser.MessageType.Login, payload);
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var totalMessages = concurrentWriters * messagesPerWriter;
        var messagesPerSecond = totalMessages / sw.Elapsed.TotalSeconds;
        output.WriteLine($"Wrote {totalMessages} messages in {sw.Elapsed.TotalSeconds:F2}s ({messagesPerSecond:F0} msg/s)");
        Assert.True(messagesPerSecond > 1000, $"Expected >1000 msg/s, got {messagesPerSecond:F0}");
    }

    [Fact]
    public async Task ProtocolParser_Should_Have_Low_Latency()
    {
        // Arrange
        const int iterations = 1000;
        var latencies = new List<TimeSpan>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var stream = new MemoryStream();
            var payload = BuildLoginPayload("testuser", "testpass");
            
            var sw = Stopwatch.StartNew();
            await parser.WriteMessageAsync(stream, SoulseekProtocolParser.MessageType.Login, payload);
            stream.Position = 0;
            var message = await parser.ReadMessageAsync(stream);
            sw.Stop();

            Assert.NotNull(message);
            latencies.Add(sw.Elapsed);
        }

        // Assert
        var avgLatency = TimeSpan.FromMilliseconds(latencies.Average(l => l.TotalMilliseconds));
        var p95Latency = latencies.OrderBy(l => l).Skip((int)(iterations * 0.95)).First();
        var p99Latency = latencies.OrderBy(l => l).Skip((int)(iterations * 0.99)).First();

        output.WriteLine($"Average latency: {avgLatency.TotalMicroseconds:F2}μs");
        output.WriteLine($"P95 latency: {p95Latency.TotalMicroseconds:F2}μs");
        output.WriteLine($"P99 latency: {p99Latency.TotalMicroseconds:F2}μs");

        Assert.True(avgLatency.TotalMilliseconds < 10, $"Expected avg latency <10ms, got {avgLatency.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task ProtocolParser_Should_Handle_Large_Messages()
    {
        // Arrange
        var largeQuery = new string('a', 10000); // 10KB query
        var payload = BuildSearchPayload(largeQuery, 12345);
        var stream = new MemoryStream();

        // Act
        await parser.WriteMessageAsync(stream, SoulseekProtocolParser.MessageType.SearchRequest, payload);
        stream.Position = 0;
        var message = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(message);
        var parsed = parser.ParseSearchRequest(message.Payload);
        Assert.NotNull(parsed);
        Assert.Equal(largeQuery, parsed.Query);
    }

    [Fact]
    public async Task ProtocolParser_Should_Handle_Many_Small_Messages()
    {
        // Arrange
        const int messageCount = 10000;
        var stream = new MemoryStream();

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < messageCount; i++)
        {
            var payload = BuildLoginPayload($"user{i}", "pass");
            await parser.WriteMessageAsync(stream, SoulseekProtocolParser.MessageType.Login, payload);
        }
        sw.Stop();

        // Assert
        var messagesPerSecond = messageCount / sw.Elapsed.TotalSeconds;
        output.WriteLine($"Wrote {messageCount} messages in {sw.Elapsed.TotalSeconds:F2}s ({messagesPerSecond:F0} msg/s)");
        Assert.True(messagesPerSecond > 5000, $"Expected >5000 msg/s, got {messagesPerSecond:F0}");
    }

    [Fact]
    public void ProtocolParser_Should_Use_Reasonable_Memory()
    {
        // Arrange
        const int messageCount = 1000;
        var streams = new List<MemoryStream>();

        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var memoryBefore = GC.GetTotalMemory(false);
        
        for (int i = 0; i < messageCount; i++)
        {
            var stream = new MemoryStream();
            var payload = BuildLoginPayload($"user{i}", "password");
            parser.WriteMessageAsync(stream, SoulseekProtocolParser.MessageType.Login, payload).Wait();
            streams.Add(stream);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;

        // Cleanup
        foreach (var stream in streams)
        {
            stream.Dispose();
        }
        streams.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Assert
        var memoryPerMessage = memoryUsed / (double)messageCount;
        output.WriteLine($"Memory used: {memoryUsed / 1024.0:F2} KB ({memoryPerMessage:F2} bytes/message)");
        // MemoryStream overhead is significant (each stream has internal buffers)
        // Allow up to 5KB per message to account for MemoryStream overhead in test scenario
        Assert.True(memoryPerMessage < 5000, $"Expected <5000 bytes/message, got {memoryPerMessage:F2}");
        
        // More important: verify memory is released after cleanup
        var memoryAfterCleanup = GC.GetTotalMemory(false);
        var memoryReleased = memoryAfter - memoryAfterCleanup;
        output.WriteLine($"Memory released after cleanup: {memoryReleased / 1024.0:F2} KB");
        Assert.True(memoryReleased > memoryUsed * 0.5, "At least 50% of memory should be released after cleanup");
    }

    [Fact]
    public async Task ProtocolParser_Should_Handle_Rapid_Connect_Disconnect()
    {
        // Arrange
        const int iterations = 100;
        var tasks = new List<Task>();

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var stream = new MemoryStream();
                var payload = BuildLoginPayload("user", "pass");
                await parser.WriteMessageAsync(stream, SoulseekProtocolParser.MessageType.Login, payload);
                stream.Position = 0;
                var message = await parser.ReadMessageAsync(stream);
                Assert.NotNull(message);
                stream.Dispose();
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var opsPerSecond = iterations / sw.Elapsed.TotalSeconds;
        output.WriteLine($"Completed {iterations} connect/disconnect cycles in {sw.Elapsed.TotalSeconds:F2}s ({opsPerSecond:F0} ops/s)");
        Assert.True(opsPerSecond > 50, $"Expected >50 ops/s, got {opsPerSecond:F0}");
    }

    private MemoryStream CreateTestMessageStream(int messageCount)
    {
        var stream = new MemoryStream();
        for (int i = 0; i < messageCount; i++)
        {
            var payload = BuildLoginPayload($"user{i}", "pass");
            parser.WriteMessageAsync(stream, SoulseekProtocolParser.MessageType.Login, payload).Wait();
        }
        stream.Position = 0;
        return stream;
    }

    private byte[] BuildLoginPayload(string username, string password)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        writer.Write(usernameBytes.Length);
        writer.Write(usernameBytes);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        writer.Write(passwordBytes.Length);
        writer.Write(passwordBytes);
        return stream.ToArray();
    }

    private byte[] BuildSearchPayload(string query, int token)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var queryBytes = Encoding.UTF8.GetBytes(query);
        writer.Write(queryBytes.Length);
        writer.Write(queryBytes);
        writer.Write(token);
        return stream.ToArray();
    }
}
