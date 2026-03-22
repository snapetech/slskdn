// <copyright file="SoulseekProtocolParser.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Bridge.Protocol;

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Parser for Soulseek protocol messages (binary format).
/// Note: Soulseek protocol is proprietary and not fully documented.
/// This is a minimal implementation for bridge proxy functionality.
/// </summary>
public class SoulseekProtocolParser
{
    private readonly ILogger<SoulseekProtocolParser> logger;

    public SoulseekProtocolParser(ILogger<SoulseekProtocolParser> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Message type codes (from reverse engineering / Soulfind reference).
    /// These may need adjustment based on actual protocol.
    /// </summary>
    public enum MessageType : int
    {
        Login = 1,
        LoginResponse = 2,
        SearchRequest = 3,
        SearchResponse = 4,
        DownloadRequest = 5,
        DownloadResponse = 6,
        RoomListRequest = 7,
        RoomListResponse = 8,
        RoomJoinRequest = 9,
        RoomJoinResponse = 10,
        RoomLeaveRequest = 11,
        RoomMessage = 12,
        UserStatus = 13,
        PeerInfo = 14,
        FileTransfer = 15,
    }

    /// <summary>
    /// Read a Soulseek protocol message from stream.
    /// Format: [4 bytes: message length (little-endian)] [4 bytes: message type] [N bytes: payload]
    /// </summary>
    public async Task<SoulseekMessage?> ReadMessageAsync(Stream stream, CancellationToken ct = default)
    {
        try
        {
            // Read message length (4 bytes, little-endian)
            var lengthBuffer = new byte[4];
            if (!await ReadExactlyAsync(stream, lengthBuffer, 0, lengthBuffer.Length, ct))
            {
                return null; // Connection closed
            }

            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Max 1MB message.
            if (messageLength < 4 || messageLength > 1024 * 1024)
            {
                logger.LogWarning("[SOULSEEK-PROTO] Invalid message length: {Length}", messageLength);
                return null;
            }

            // Read message type (4 bytes, little-endian)
            var typeBuffer = new byte[4];
            if (!await ReadExactlyAsync(stream, typeBuffer, 0, typeBuffer.Length, ct))
            {
                return null;
            }

            var messageType = (MessageType)BitConverter.ToInt32(typeBuffer, 0);

            // Read payload (remaining bytes)
            var payloadLength = messageLength - 4; // Subtract type bytes
            if (payloadLength < 0)
            {
                logger.LogWarning("[SOULSEEK-PROTO] Invalid payload length: {Length}", payloadLength);
                return null;
            }

            var payload = new byte[payloadLength];
            if (payloadLength > 0)
            {
                if (!await ReadExactlyAsync(stream, payload, 0, payload.Length, ct))
                {
                    logger.LogWarning("[SOULSEEK-PROTO] Incomplete payload: expected {Expected} bytes",
                        payloadLength);
                    return null;
                }
            }

            return new SoulseekMessage
            {
                Type = messageType,
                Payload = payload
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SOULSEEK-PROTO] Error reading message");
            return null;
        }
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var bytesRead = await stream.ReadAsync(
                buffer,
                offset + totalRead,
                count - totalRead,
                ct);

            if (bytesRead == 0)
            {
                return false;
            }

            totalRead += bytesRead;
        }

        return true;
    }

    /// <summary>
    /// Write a Soulseek protocol message to stream.
    /// </summary>
    public async Task WriteMessageAsync(Stream stream, MessageType type, byte[] payload, CancellationToken ct = default)
    {
        try
        {
            var messageLength = 4 + (payload?.Length ?? 0); // Type (4) + payload

            // Write message length (4 bytes, little-endian)
            var lengthBytes = BitConverter.GetBytes(messageLength);
            await stream.WriteAsync(lengthBytes, 0, 4, ct);

            // Write message type (4 bytes, little-endian)
            var typeBytes = BitConverter.GetBytes((int)type);
            await stream.WriteAsync(typeBytes, 0, 4, ct);

            // Write payload
            if (payload != null && payload.Length > 0)
            {
                await stream.WriteAsync(payload, 0, payload.Length, ct);
            }

            await stream.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SOULSEEK-PROTO] Error writing message");
            throw;
        }
    }

    /// <summary>
    /// Parse login request from payload.
    /// Format: [username: string] [password: string]
    /// </summary>
    public LoginRequest? ParseLoginRequest(byte[] payload)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            var username = ReadString(reader);
            var password = ReadString(reader);
            return new LoginRequest { Username = username, Password = password };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SOULSEEK-PROTO] Failed to parse login request");
            return null;
        }
    }

    /// <summary>
    /// Parse search request from payload.
    /// Format: [search text: string] [token: int32]
    /// </summary>
    public SearchRequest? ParseSearchRequest(byte[] payload)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            var query = ReadString(reader);
            var token = reader.ReadInt32();
            return new SearchRequest { Query = query, Token = token };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SOULSEEK-PROTO] Failed to parse search request");
            return null;
        }
    }

    /// <summary>
    /// Parse download request from payload.
    /// Format: [username: string] [filename: string] [token: int32]
    /// </summary>
    public DownloadRequest? ParseDownloadRequest(byte[] payload)
    {
        try
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            var username = ReadString(reader);
            var filename = ReadString(reader);
            var token = reader.ReadInt32();
            return new DownloadRequest { Username = username, Filename = filename, Token = token };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[SOULSEEK-PROTO] Failed to parse download request");
            return null;
        }
    }

    /// <summary>
    /// Build login response payload.
    /// Format: [success: bool] [message: string]
    /// </summary>
    public byte[] BuildLoginResponse(bool success, string message)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(success);
        WriteString(writer, message);
        return stream.ToArray();
    }

    /// <summary>
    /// Build search response payload.
    /// Format: [token: int32] [file count: int32] [files: array of (username, filename, size, code, extension)]
    /// </summary>
    public byte[] BuildSearchResponse(int token, List<SearchFileResult> files)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(token);
        writer.Write(files.Count);
        foreach (var file in files)
        {
            WriteString(writer, file.Username);
            WriteString(writer, file.Filename);
            writer.Write(file.Size);
            writer.Write(file.Code);
            WriteString(writer, file.Extension ?? string.Empty);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Build room list response payload.
    /// Format: [room count: int32] [rooms: array of (name, user count)]
    /// </summary>
    public byte[] BuildRoomListResponse(List<RoomInfo> rooms)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(rooms.Count);
        foreach (var room in rooms)
        {
            WriteString(writer, room.Name);
            writer.Write(room.UserCount);
        }

        return stream.ToArray();
    }

    private string ReadString(BinaryReader reader)
    {
        var length = reader.ReadInt32();

        // Max 1MB string.
        if (length < 0 || length > 1024 * 1024)
        {
            throw new InvalidOperationException($"Invalid string length: {length}");
        }

        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}

/// <summary>
/// Raw Soulseek protocol message.
/// </summary>
public class SoulseekMessage
{
    public SoulseekProtocolParser.MessageType Type { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Login request.
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Search request.
/// </summary>
public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Token { get; set; }
}

/// <summary>
/// Download request.
/// </summary>
public class DownloadRequest
{
    public string Username { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public int Token { get; set; }
}

/// <summary>
/// Search file result.
/// </summary>
public class SearchFileResult
{
    public string Username { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public long Size { get; set; }
    public int Code { get; set; }
    public string? Extension { get; set; }
}

/// <summary>
/// Room information.
/// </summary>
public class RoomInfo
{
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
}
