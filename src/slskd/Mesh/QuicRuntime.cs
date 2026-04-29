// <copyright file="QuicRuntime.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh;

using System;
using System.Net.Quic;
using System.Runtime.Versioning;

public static class QuicRuntime
{
    [SupportedOSPlatformGuard("linux")]
    [SupportedOSPlatformGuard("macos")]
    [SupportedOSPlatformGuard("windows")]
    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
#pragma warning disable CA1416 // OS guard above constrains System.Net.Quic support checks to supported platforms.
            return QuicConnection.IsSupported && QuicListener.IsSupported;
#pragma warning restore CA1416
        }
        catch
        {
            return false;
        }
    }
}
