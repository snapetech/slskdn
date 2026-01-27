// <copyright file="BridgeProtocolValidationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.VirtualSoulfind.Bridge;

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using slskd.VirtualSoulfind.Bridge.Protocol;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Protocol format validation tests for SoulseekProtocolParser.
/// Tests compatibility with actual Soulseek client message formats.
/// </summary>
[Trait("Category", "L2-Bridge")]
public class BridgeProtocolValidationTests
{
    private readonly ITestOutputHelper output;
    private readonly SoulseekProtocolParser parser;

    public BridgeProtocolValidationTests(ITestOutputHelper output)
    {
        this.output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        parser = new SoulseekProtocolParser(loggerFactory.CreateLogger<SoulseekProtocolParser>());
    }

    [Fact]
    public void ParseLoginRequest_Should_Handle_Empty_Username()
    {
        // Arrange
        var payload = BuildLoginPayload(string.Empty, "password");

        // Act
        var result = parser.ParseLoginRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Username);
        Assert.Equal("password", result.Password);
    }

    [Fact]
    public void ParseLoginRequest_Should_Handle_Empty_Password()
    {
        // Arrange
        var payload = BuildLoginPayload("username", string.Empty);

        // Act
        var result = parser.ParseLoginRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("username", result.Username);
        Assert.Equal(string.Empty, result.Password);
    }

    [Fact]
    public void ParseLoginRequest_Should_Handle_Unicode_Characters()
    {
        // Arrange
        var username = "test用户";
        var password = "пароль123";
        var payload = BuildLoginPayload(username, password);

        // Act
        var result = parser.ParseLoginRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
        Assert.Equal(password, result.Password);
    }

    [Fact]
    public void ParseSearchRequest_Should_Handle_Empty_Query()
    {
        // Arrange
        var payload = BuildSearchPayload(string.Empty, 12345);

        // Act
        var result = parser.ParseSearchRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Query);
        Assert.Equal(12345, result.Token);
    }

    [Fact]
    public void ParseSearchRequest_Should_Handle_Long_Query()
    {
        // Arrange
        var longQuery = new string('a', 1000); // 1000 character query
        var payload = BuildSearchPayload(longQuery, 12345);

        // Act
        var result = parser.ParseSearchRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(longQuery, result.Query);
        Assert.Equal(12345, result.Token);
    }

    [Fact]
    public void ParseSearchRequest_Should_Handle_Special_Characters()
    {
        // Arrange
        var query = "test & query (with) [special] {chars}";
        var payload = BuildSearchPayload(query, 12345);

        // Act
        var result = parser.ParseSearchRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
    }

    [Fact]
    public async Task ReadMessageAsync_Should_Handle_Invalid_Length()
    {
        // Arrange
        var stream = new MemoryStream();
        // Write invalid length (negative or too large)
        var invalidLength = BitConverter.GetBytes(-1);
        await stream.WriteAsync(invalidLength, 0, 4);

        // Act
        var result = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.Null(result); // Should return null for invalid message
    }

    [Fact]
    public async Task ReadMessageAsync_Should_Handle_Truncated_Message()
    {
        // Arrange
        var stream = new MemoryStream();
        // Write valid length but incomplete message
        var length = BitConverter.GetBytes(100);
        await stream.WriteAsync(length, 0, 4);
        // Write only type, no payload
        var type = BitConverter.GetBytes((int)SoulseekProtocolParser.MessageType.Login);
        await stream.WriteAsync(type, 0, 4);
        stream.Position = 0;

        // Act
        var result = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.Null(result); // Should return null for incomplete message
    }

    [Fact]
    public async Task WriteMessageAsync_ReadMessageAsync_Should_Roundtrip()
    {
        // Arrange
        var stream = new MemoryStream();
        var originalType = SoulseekProtocolParser.MessageType.SearchRequest;
        var originalPayload = BuildSearchPayload("test query", 12345);

        // Act - Write message
        await parser.WriteMessageAsync(stream, originalType, originalPayload);
        stream.Position = 0;

        // Act - Read message back
        var result = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalType, result.Type);
        Assert.Equal(originalPayload, result.Payload);
    }

    [Fact]
    public async Task WriteMessageAsync_ReadMessageAsync_Should_Roundtrip_Login()
    {
        // Arrange
        var stream = new MemoryStream();
        var originalType = SoulseekProtocolParser.MessageType.Login;
        var originalPayload = BuildLoginPayload("testuser", "testpass");

        // Act - Write message
        await parser.WriteMessageAsync(stream, originalType, originalPayload);
        stream.Position = 0;

        // Act - Read message back
        var result = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalType, result.Type);
        Assert.Equal(originalPayload, result.Payload);

        // Verify parsed content
        var login = parser.ParseLoginRequest(result.Payload);
        Assert.Equal("testuser", login.Username);
        Assert.Equal("testpass", login.Password);
    }

    [Fact]
    public async Task WriteMessageAsync_ReadMessageAsync_Should_Roundtrip_Empty_Payload()
    {
        // Arrange
        var stream = new MemoryStream();
        var originalType = SoulseekProtocolParser.MessageType.RoomListRequest;
        var originalPayload = Array.Empty<byte>();

        // Act - Write message
        await parser.WriteMessageAsync(stream, originalType, originalPayload);
        stream.Position = 0;

        // Act - Read message back
        var result = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalType, result.Type);
        Assert.Equal(originalPayload, result.Payload);
    }

    [Fact]
    public void BuildLoginResponse_Should_Format_Correctly()
    {
        // Arrange
        var success = true;
        var message = "Login successful";

        // Act
        var response = parser.BuildLoginResponse(success, message);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Length > 0);

        // Verify format: [bool: success] [int: messageLength] [bytes: message]
        using var reader = new BinaryReader(new MemoryStream(response));
        var resultSuccess = reader.ReadBoolean();
        var messageLength = reader.ReadInt32();
        var resultMessage = Encoding.UTF8.GetString(reader.ReadBytes(messageLength));

        Assert.Equal(success, resultSuccess);
        Assert.Equal(message, resultMessage);
    }

    [Fact]
    public void BuildSearchResponse_Should_Format_Correctly()
    {
        // Arrange
        var token = 12345;
        var files = new List<slskd.VirtualSoulfind.Bridge.Protocol.SearchFileResult>
        {
            new slskd.VirtualSoulfind.Bridge.Protocol.SearchFileResult
            {
                Username = "user1",
                Filename = "test.mp3",
                Size = 1024,
                Code = 0,
                Extension = ".mp3"
            }
        };

        // Act
        var response = parser.BuildSearchResponse(token, files);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Length > 0);
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

    [Fact]
    public async Task ReadMessageAsync_Should_Handle_All_Message_Types()
    {
        // Test that parser can read all defined message types
        foreach (SoulseekProtocolParser.MessageType messageType in Enum.GetValues(typeof(SoulseekProtocolParser.MessageType)))
        {
            var stream = new MemoryStream();
            var payload = Array.Empty<byte>();
            await parser.WriteMessageAsync(stream, messageType, payload);
            stream.Position = 0;

            var result = await parser.ReadMessageAsync(stream);

            Assert.NotNull(result);
            Assert.Equal(messageType, result.Type);
        }
    }

    [Fact]
    public async Task ReadMessageAsync_Should_Reject_Message_Length_Exceeding_Max()
    {
        // Arrange
        var stream = new MemoryStream();
        var maxLength = 1024 * 1024 + 1; // Exceeds 1MB limit
        var lengthBytes = BitConverter.GetBytes(maxLength);
        await stream.WriteAsync(lengthBytes, 0, 4);

        // Act
        var result = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.Null(result); // Should reject oversized messages
    }

    [Fact]
    public void ParseDownloadRequest_Should_Handle_Unicode_Filenames()
    {
        // Arrange
        var username = "testuser";
        var filename = "test文件.mp3"; // Unicode filename
        var token = 12345;
        var payload = BuildDownloadPayload(username, filename, token);

        // Act
        var result = parser.ParseDownloadRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
        Assert.Equal(filename, result.Filename);
        Assert.Equal(token, result.Token);
    }

    [Fact]
    public void ParseDownloadRequest_Should_Handle_Path_With_Special_Characters()
    {
        // Arrange
        var username = "testuser";
        var filename = "Music/Artist - Album (2020)/01 - Track Name.mp3";
        var token = 12345;
        var payload = BuildDownloadPayload(username, filename, token);

        // Act
        var result = parser.ParseDownloadRequest(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(filename, result.Filename);
    }

    [Fact]
    public async Task WriteMessageAsync_ReadMessageAsync_Should_Handle_Large_Payloads()
    {
        // Arrange
        var stream = new MemoryStream();
        var largePayload = new byte[100 * 1024]; // 100KB payload
        new Random().NextBytes(largePayload);
        var originalType = SoulseekProtocolParser.MessageType.FileTransfer;

        // Act - Write message
        await parser.WriteMessageAsync(stream, originalType, largePayload);
        stream.Position = 0;

        // Act - Read message back
        var result = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalType, result.Type);
        Assert.Equal(largePayload, result.Payload);
    }

    [Fact]
    public void BuildSearchResponse_Should_Handle_Empty_File_List()
    {
        // Arrange
        var token = 12345;
        var files = new List<slskd.VirtualSoulfind.Bridge.Protocol.SearchFileResult>();

        // Act
        var response = parser.BuildSearchResponse(token, files);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Length > 0);

        // Verify format: [token: int32] [file count: int32 (0)]
        using var reader = new BinaryReader(new MemoryStream(response));
        var resultToken = reader.ReadInt32();
        var fileCount = reader.ReadInt32();

        Assert.Equal(token, resultToken);
        Assert.Equal(0, fileCount);
    }

    [Fact]
    public void BuildRoomListResponse_Should_Format_Correctly()
    {
        // Arrange
        var rooms = new List<slskd.VirtualSoulfind.Bridge.Protocol.RoomInfo>
        {
            new slskd.VirtualSoulfind.Bridge.Protocol.RoomInfo { Name = "Room1", UserCount = 10 },
            new slskd.VirtualSoulfind.Bridge.Protocol.RoomInfo { Name = "Room2", UserCount = 5 }
        };

        // Act
        var response = parser.BuildRoomListResponse(rooms);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Length > 0);
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

    private byte[] BuildDownloadPayload(string username, string filename, int token)
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
}
