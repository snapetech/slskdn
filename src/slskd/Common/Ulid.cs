namespace slskd.Common;

using System;

/// <summary>
/// Minimal Ulid stand-in for compatibility. Uses Guid internally.
/// </summary>
public static class Ulid
{
    public static string NewUlid() => Guid.NewGuid().ToString("N");
}

