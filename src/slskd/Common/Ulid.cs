// <copyright file="Ulid.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common;

using System;

/// <summary>
/// Minimal Ulid stand-in for compatibility. Uses Guid internally.
/// </summary>
public static class Ulid
{
    public static string NewUlid() => Guid.NewGuid().ToString("N");
}
