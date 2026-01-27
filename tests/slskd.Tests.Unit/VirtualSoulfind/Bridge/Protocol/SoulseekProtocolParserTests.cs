// <copyright file="SoulseekProtocolParserTests.cs" company="slskdN Team">
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
/// Unit tests for SoulseekProtocolParser.
/// </summary>
public class SoulseekProtocolParserTests
{
    private readonly ITestOutputHelper output;
    private readonly ILogger<SoulseekProtocolParser> logger;
    private readonly SoulseekProtocolParser parser;

    public SoulseekProtocolParserTests(ITestOutputHelper output)
    {
        this.output = output;
        logger = new XunitLogger<SoulseekProtocolParser>(output);
        parser = new SoulseekProtocolParser(logger);
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

    [Fact]
    public void ParseLoginRequest_Should_Extract_Username_And_Password()
    {
        // Arrange
        var username = "testuser";
        var password = "testpass";
        var payload = BuildLoginRequestPayload(username, password);

        // Act
        var result = parser.ParseLoginRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
        Assert.Equal(password, result.Password);
    }

    [Fact]
    public void ParseSearchRequest_Should_Extract_Query_And_Token()
    {
        // Arrange
        var query = "test query";
        var token = 12345;
        var payload = BuildSearchRequestPayload(query, token);

        // Act
        var result = parser.ParseSearchRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
        Assert.Equal(token, result.Token);
    }

    [Fact]
    public void ParseDownloadRequest_Should_Extract_Username_Filename_And_Token()
    {
        // Arrange
        var username = "testuser";
        var filename = "testfile.mp3";
        var token = 67890;
        var payload = BuildDownloadRequestPayload(username, filename, token);

        // Act
        var result = parser.ParseDownloadRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
        Assert.Equal(filename, result.Filename);
        Assert.Equal(token, result.Token);
    }

    [Fact]
    public void BuildLoginResponse_Should_Create_Valid_Payload()
    {
        // Arrange
        var success = true;
        var message = "Login successful";

        // Act
        var payload = parser.BuildLoginResponse(success, message);

        // Assert
        Assert.NotNull(payload);
        Assert.True(payload.Length > 0);
    }

    [Fact]
    public void BuildSearchResponse_Should_Create_Valid_Payload()
    {
        // Arrange
        var token = 12345;
        var files = new List<SearchFileResult>
        {
            new SearchFileResult
            {
                Username = "user1",
                Filename = "file1.mp3",
                Size = 1024,
                Code = 0,
                Extension = ".mp3"
            },
            new SearchFileResult
            {
                Username = "user2",
                Filename = "file2.flac",
                Size = 2048,
                Code = 0,
                Extension = ".flac"
            }
        };

        // Act
        var payload = parser.BuildSearchResponse(token, files);

        // Assert
        Assert.NotNull(payload);
        Assert.True(payload.Length > 0);
    }

    [Fact]
    public void BuildRoomListResponse_Should_Create_Valid_Payload()
    {
        // Arrange
        var rooms = new List<RoomInfo>
        {
            new RoomInfo { Name = "Room1", UserCount = 10 },
            new RoomInfo { Name = "Room2", UserCount = 20 }
        };

        // Act
        var payload = parser.BuildRoomListResponse(rooms);

        // Assert
        Assert.NotNull(payload);
        Assert.True(payload.Length > 0);
    }

    [Fact]
    public async Task ReadMessageAsync_Should_Parse_Message_Correctly()
    {
        // Arrange
        var messageType = SoulseekProtocolParser.MessageType.Login;
        var payload = BuildLoginRequestPayload("user", "pass");
        var stream = CreateMessageStream(messageType, payload);

        // Act
        var message = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(messageType, message.Type);
        Assert.Equal(payload.Length, message.Payload.Length);
    }

    [Fact]
    public async Task WriteMessageAsync_Should_Write_Message_Correctly()
    {
        // Arrange
        var messageType = SoulseekProtocolParser.MessageType.LoginResponse;
        var payload = parser.BuildLoginResponse(true, "Success");
        var stream = new MemoryStream();

        // Act
        await parser.WriteMessageAsync(stream, messageType, payload);
        stream.Position = 0;

        // Assert
        var message = await parser.ReadMessageAsync(stream);
        Assert.NotNull(message);
        Assert.Equal(messageType, message.Type);
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

    private Stream CreateMessageStream(SoulseekProtocolParser.MessageType type, byte[] payload)
    {
        var stream = new MemoryStream();
        var messageLength = 4 + payload.Length; // Type (4) + payload
        var lengthBytes = BitConverter.GetBytes(messageLength);
        stream.Write(lengthBytes, 0, 4);
        var typeBytes = BitConverter.GetBytes((int)type);
        stream.Write(typeBytes, 0, 4);
        stream.Write(payload, 0, payload.Length);
        stream.Position = 0;
        return stream;
    }
}
