// <copyright file="ReleaseOnDisposeStream.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Streaming;

using System;
using System.IO;

/// <summary>Wraps a stream and invokes an action when disposed. Used to release IStreamSessionLimiter when the response completes.</summary>
public sealed class ReleaseOnDisposeStream : Stream
{
    private readonly Stream _inner;
    private readonly Action _onDispose;
    private bool _disposed;

    public ReleaseOnDisposeStream(Stream inner, Action onDispose)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        try { _onDispose(); } catch { /* best-effort */ }
        _inner.Dispose();
        base.Dispose(disposing);
    }
}
