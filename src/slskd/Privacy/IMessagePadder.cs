// <copyright file="IMessagePadder.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

namespace slskd.Privacy;

using System;

/// <summary>
///     Pads outbound messages to a configured bucket size for traffic analysis resistance.
/// </summary>
public interface IMessagePadder
{
    /// <summary>
    ///     Pads the payload to the next configured bucket size using random fill bytes.
    /// </summary>
    /// <param name="payload">The payload to pad.</param>
    /// <returns>The padded payload; original payload if padding is disabled or no bucket matches.</returns>
    byte[] Pad(ReadOnlyMemory<byte> payload);
}
