// <copyright file="SecureMessageFramer.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using slskd.DhtRendezvous.Messages;

/// <summary>
/// Secure message framing using length-prefixed protocol.
///
/// Wire format:
/// [4 bytes: message length (big-endian)] [N bytes: JSON payload]
///
/// SECURITY: This prevents unbounded reads that could exhaust memory.
/// </summary>
public sealed class SecureMessageFramer : IDisposable
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly Stream _stream;
    private readonly JsonSerializerOptions _jsonOptions;

    // Serializes writes so concurrent callers (outbound read loop sending a Pong,
    // MeshOverlaySearchService sending mesh_search_req, MeshServiceClient sending a
    // service call) cannot interleave the 4-byte length header and JSON payload on the
    // shared SslStream. SslStream.WriteAsync is not thread-safe; torn frames show up
    // on the peer as a bogus length prefix (e.g. JSON "{" bytes 0x7B2BF939 being
    // interpreted as an int32 length), which triggers an immediate disconnect.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// Length header size in bytes.
    /// </summary>
    public const int HeaderSize = 4;

    /// <summary>
    /// Maximum message size (see <see cref="OverlayProtocol.MaxMessageSize"/>).
    /// </summary>
    public const int MaxMessageSize = OverlayProtocol.MaxMessageSize;

    /// <summary>
    /// Minimum valid message size (empty JSON object: {}).
    /// </summary>
    public const int MinMessageSize = 2;

    public SecureMessageFramer(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _jsonOptions = DefaultJsonOptions;
    }

    /// <summary>
    /// Read a length-prefixed message from the stream.
    /// </summary>
    /// <typeparam name="T">The message type to deserialize.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized message.</returns>
    /// <exception cref="ProtocolViolationException">If message is invalid.</exception>
    public async Task<T> ReadMessageAsync<T>(CancellationToken cancellationToken = default)
    {
        var buffer = await ReadPayloadAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var message = JsonSerializer.Deserialize<T>(buffer, _jsonOptions);
            if (message is null)
            {
                throw new ProtocolViolationException("Deserialized message is null");
            }

            return message;
        }
        catch (JsonException ex)
        {
            throw new ProtocolViolationException("Invalid JSON", ex);
        }
    }

    /// <summary>
    /// Read raw message bytes (for when you need to determine message type first).
    /// </summary>
    public async Task<byte[]> ReadRawMessageAsync(CancellationToken cancellationToken = default)
        => await ReadPayloadAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Deserialize raw bytes to a specific message type.
    /// </summary>
    public static T DeserializeMessage<T>(byte[] data)
    {
        try
        {
            var message = JsonSerializer.Deserialize<T>(data, DefaultJsonOptions);
            if (message is null)
            {
                throw new ProtocolViolationException("Deserialized message is null");
            }

            return message;
        }
        catch (JsonException ex)
        {
            throw new ProtocolViolationException("Invalid JSON", ex);
        }
    }

    /// <summary>
    /// Extract message type from raw bytes without full deserialization.
    /// </summary>
    public static string? ExtractMessageType(byte[] data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("type", out var typeProp))
            {
                return typeProp.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Write a message with length prefix.
    /// </summary>
    public async Task WriteMessageAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);

        if (json.Length > MaxMessageSize)
        {
            throw new ProtocolViolationException($"Message too large to send: {json.Length} > {MaxMessageSize}");
        }

        var header = new byte[HeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(header, json.Length);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }

    private async Task<byte[]> ReadPayloadAsync(CancellationToken cancellationToken)
    {
        var headerBuffer = new byte[HeaderSize];
        await ReadExactlyAsync(headerBuffer, cancellationToken).ConfigureAwait(false);

        if (headerBuffer[0] == (byte)'{')
        {
            return await ReadUnframedJsonPayloadAsync(headerBuffer, cancellationToken).ConfigureAwait(false);
        }

        var length = BinaryPrimitives.ReadInt32BigEndian(headerBuffer);

        if (length < MinMessageSize)
        {
            throw new ProtocolViolationException($"Message too small: {length} < {MinMessageSize}");
        }

        if (length > MaxMessageSize)
        {
            throw new ProtocolViolationException($"Message too large: {length} > {MaxMessageSize}");
        }

        var buffer = new byte[length];
        await ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);

        return buffer;
    }

    private async Task<byte[]> ReadUnframedJsonPayloadAsync(byte[] prefix, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxMessageSize];
        Buffer.BlockCopy(prefix, 0, buffer, 0, prefix.Length);
        var length = prefix.Length;

        while (true)
        {
            if (IsCompleteJsonObject(buffer.AsSpan(0, length)))
            {
                var payload = new byte[length];
                Buffer.BlockCopy(buffer, 0, payload, 0, length);
                return payload;
            }

            if (length >= MaxMessageSize)
            {
                throw new ProtocolViolationException($"Unframed JSON message too large: {length} >= {MaxMessageSize}");
            }

            var read = await _stream.ReadAsync(buffer.AsMemory(length, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed while reading unframed JSON message");
            }

            length += read;
        }
    }

    private static bool IsCompleteJsonObject(ReadOnlySpan<byte> data)
    {
        try
        {
            var reader = new Utf8JsonReader(data, isFinalBlock: true, state: default);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read())
            {
            }

            return reader.BytesConsumed == data.Length;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Read exactly the specified number of bytes.
    /// </summary>
    private async Task ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        var remaining = buffer.Length;

        while (remaining > 0)
        {
            var read = await _stream.ReadAsync(buffer.AsMemory(offset, remaining), cancellationToken);

            if (read == 0)
            {
                throw new EndOfStreamException("Connection closed while reading message");
            }

            offset += read;
            remaining -= read;
        }
    }
}

/// <summary>
/// Exception thrown when protocol is violated.
/// </summary>
public class ProtocolViolationException : Exception
{
    public ProtocolViolationException(string message)
        : base(message)
    {
    }

    public ProtocolViolationException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
