// <copyright file="SoulseekProtocolParserAdditionalTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Bridge.Protocol;

using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using slskd.VirtualSoulfind.Bridge.Protocol;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Additional unit tests for SoulseekProtocolParser edge cases and error handling.
/// </summary>
public class SoulseekProtocolParserAdditionalTests
{
    private readonly ITestOutputHelper output;
    private readonly ILogger<SoulseekProtocolParser> logger;
    private readonly SoulseekProtocolParser parser;

    public SoulseekProtocolParserAdditionalTests(ITestOutputHelper output)
    {
        this.output = output;
        logger = new XunitLogger<SoulseekProtocolParser>(output);
        parser = new SoulseekProtocolParser(logger);
    }

    [Fact]
    public void ParseLoginRequest_Should_Handle_Empty_Strings()
    {
        // Arrange
        var payload = BuildLoginRequestPayload(string.Empty, string.Empty);

        // Act
        var result = parser.ParseLoginRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Username);
        Assert.Equal(string.Empty, result.Password);
    }

    [Fact]
    public void ParseSearchRequest_Should_Handle_Empty_Query()
    {
        // Arrange
        var payload = BuildSearchRequestPayload(string.Empty, 12345);

        // Act
        var result = parser.ParseSearchRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Query);
        Assert.Equal(12345, result.Token);
    }

    [Fact]
    public void ParseDownloadRequest_Should_Handle_Long_Filenames()
    {
        // Arrange
        var longFilename = new string('a', 1000) + ".mp3";
        var payload = BuildDownloadRequestPayload("user", longFilename, 67890);

        // Act
        var result = parser.ParseDownloadRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longFilename, result.Filename);
    }

    [Fact]
    public async Task ReadMessageAsync_Should_Handle_Invalid_Length()
    {
        // Arrange - Create stream with invalid length (too large)
        var stream = new MemoryStream();
        var invalidLength = 10 * 1024 * 1024; // 10MB (exceeds max)
        var lengthBytes = BitConverter.GetBytes(invalidLength);
        stream.Write(lengthBytes, 0, 4);
        stream.Position = 0;

        // Act & Assert
        var message = await parser.ReadMessageAsync(stream);
        Assert.Null(message); // Should return null for invalid message
    }

    [Fact]
    public async Task WriteMessageAsync_Then_ReadMessageAsync_Should_RoundTrip()
    {
        // Arrange
        var messageType = SoulseekProtocolParser.MessageType.SearchResponse;
        var files = new List<SearchFileResult>
        {
            new SearchFileResult
            {
                Username = "testuser",
                Filename = "test.mp3",
                Size = 1024,
                Code = 0,
                Extension = ".mp3"
            }
        };
        var payload = parser.BuildSearchResponse(12345, files);
        var stream = new MemoryStream();

        // Act
        await parser.WriteMessageAsync(stream, messageType, payload);
        stream.Position = 0;
        var readMessage = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(readMessage);
        Assert.Equal(messageType, readMessage.Type);
        Assert.True(readMessage.Payload.Length > 0);
    }

    [Fact]
    public void BuildSearchResponse_Should_Handle_Empty_FileList()
    {
        // Arrange
        var token = 12345;
        var files = new List<SearchFileResult>();

        // Act
        var payload = parser.BuildSearchResponse(token, files);

        // Assert
        Assert.NotNull(payload);
        Assert.True(payload.Length > 0); // Should still have token and count (0)
    }

    [Fact]
    public void BuildRoomListResponse_Should_Handle_Empty_RoomList()
    {
        // Arrange
        var rooms = new List<RoomInfo>();

        // Act
        var payload = parser.BuildRoomListResponse(rooms);

        // Assert
        Assert.NotNull(payload);
        Assert.True(payload.Length > 0); // Should still have count (0)
    }

    private byte[] BuildLoginRequestPayload(string username, string password)
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

    private byte[] BuildSearchRequestPayload(string query, int token)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var queryBytes = Encoding.UTF8.GetBytes(query);
        writer.Write(queryBytes.Length);
        writer.Write(queryBytes);
        writer.Write(token);
        return stream.ToArray();
    }

    private byte[] BuildDownloadRequestPayload(string username, string filename, int token)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        writer.Write(usernameBytes.Length);
        writer.Write(usernameBytes);
        var filenameBytes = Encoding.UTF8.GetBytes(filename);
        writer.Write(filenameBytes.Length);
        writer.Write(filenameBytes);
        writer.Write(token);
        return stream.ToArray();
    }

    /// <summary>
    /// Xunit logger implementation for unit tests.
    /// </summary>
    private class XunitLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

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
