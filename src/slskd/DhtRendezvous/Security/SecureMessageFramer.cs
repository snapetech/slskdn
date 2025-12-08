// <copyright file="SecureMessageFramer.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
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
public sealed class SecureMessageFramer
{
    private readonly Stream _stream;
    private readonly JsonSerializerOptions _jsonOptions;
    
    /// <summary>
    /// Length header size in bytes.
    /// </summary>
    public const int HeaderSize = 4;
    
    /// <summary>
    /// Maximum message size (4KB).
    /// </summary>
    public const int MaxMessageSize = OverlayProtocol.MaxMessageSize;
    
    /// <summary>
    /// Minimum valid message size (empty JSON object: {}).
    /// </summary>
    public const int MinMessageSize = 2;
    
    public SecureMessageFramer(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
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
        // Step 1: Read 4-byte length header
        var headerBuffer = new byte[HeaderSize];
        await ReadExactlyAsync(headerBuffer, cancellationToken);
        
        // Step 2: Parse length (big-endian)
        var length = BinaryPrimitives.ReadInt32BigEndian(headerBuffer);
        
        // Step 3: VALIDATE length BEFORE allocating buffer
        if (length < MinMessageSize)
        {
            throw new ProtocolViolationException($"Message too small: {length} < {MinMessageSize}");
        }
        
        if (length > MaxMessageSize)
        {
            throw new ProtocolViolationException($"Message too large: {length} > {MaxMessageSize}");
        }
        
        // Step 4: Now safe to allocate and read
        var buffer = new byte[length];
        await ReadExactlyAsync(buffer, cancellationToken);
        
        // Step 5: Deserialize JSON
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
            throw new ProtocolViolationException($"Invalid JSON: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Read raw message bytes (for when you need to determine message type first).
    /// </summary>
    public async Task<byte[]> ReadRawMessageAsync(CancellationToken cancellationToken = default)
    {
        var headerBuffer = new byte[HeaderSize];
        await ReadExactlyAsync(headerBuffer, cancellationToken);
        
        var length = BinaryPrimitives.ReadInt32BigEndian(headerBuffer);
        
        if (length < MinMessageSize || length > MaxMessageSize)
        {
            throw new ProtocolViolationException($"Invalid message length: {length}");
        }
        
        var buffer = new byte[length];
        await ReadExactlyAsync(buffer, cancellationToken);
        
        return buffer;
    }
    
    /// <summary>
    /// Deserialize raw bytes to a specific message type.
    /// </summary>
    public T DeserializeMessage<T>(byte[] data)
    {
        try
        {
            var message = JsonSerializer.Deserialize<T>(data, _jsonOptions);
            if (message is null)
            {
                throw new ProtocolViolationException("Deserialized message is null");
            }
            
            return message;
        }
        catch (JsonException ex)
        {
            throw new ProtocolViolationException($"Invalid JSON: {ex.Message}", ex);
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
        // Serialize to JSON
        var json = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);
        
        // Check size BEFORE sending
        if (json.Length > MaxMessageSize)
        {
            throw new ProtocolViolationException($"Message too large to send: {json.Length} > {MaxMessageSize}");
        }
        
        // Write length header (big-endian)
        var header = new byte[HeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(header, json.Length);
        
        await _stream.WriteAsync(header, cancellationToken);
        await _stream.WriteAsync(json, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
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
    public ProtocolViolationException(string message) : base(message) { }
    public ProtocolViolationException(string message, Exception inner) : base(message, inner) { }
}

