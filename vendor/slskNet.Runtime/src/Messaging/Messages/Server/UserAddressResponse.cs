// <copyright file="UserAddressResponse.cs" company="JP Dillingham">
//     Copyright (c) JP Dillingham.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, version 3.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
//
//     This program is distributed with Additional Terms pursuant to Section 7
//     of the GPLv3.  See the LICENSE file in the root directory of this
//     project for the complete terms and conditions.
//
//     SPDX-FileCopyrightText: JP Dillingham
//     SPDX-License-Identifier: GPL-3.0-only
//
//     Modified by slskdN Team.
//     Modified: Parse optional type-1 obfuscated port metadata.
// </copyright>

namespace Soulseek.Messaging.Messages
{
    using System;
    using System.Net;

    /// <summary>
    ///     The response to a request for a peer's address.
    /// </summary>
    internal sealed class UserAddressResponse : IIncomingMessage
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserAddressResponse"/> class.
        /// </summary>
        /// <param name="username">The requested peer username.</param>
        /// <param name="ipAddress">The IP address of the peer.</param>
        /// <param name="port">The port on which the peer is listening.</param>
        /// <param name="obfuscationType">The peer-message obfuscation type, if advertised.</param>
        /// <param name="obfuscatedPort">The obfuscated peer-message port, if advertised.</param>
        public UserAddressResponse(string username, IPAddress ipAddress, int port, int obfuscationType = 0, int obfuscatedPort = 0)
            : this(username, new IPEndPoint(ipAddress, port), obfuscationType, obfuscatedPort)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="UserAddressResponse"/> class.
        /// </summary>
        /// <param name="username">The requested peer username.</param>
        /// <param name="endpoint">The IP endpoint of the peer.</param>
        /// <param name="obfuscationType">The peer-message obfuscation type, if advertised.</param>
        /// <param name="obfuscatedPort">The obfuscated peer-message port, if advertised.</param>
        public UserAddressResponse(string username, IPEndPoint endpoint, int obfuscationType = 0, int obfuscatedPort = 0)
        {
            Username = username;
            IPEndPoint = endpoint;
            ObfuscationType = obfuscationType;
            ObfuscatedPort = obfuscatedPort;

            IPAddress = IPEndPoint.Address;
            Port = IPEndPoint.Port;
        }

        /// <summary>
        ///     Gets the obfuscated peer-message endpoint, if advertised.
        /// </summary>
        public IPEndPoint ObfuscatedIPEndPoint => HasObfuscatedEndpoint ? new IPEndPoint(IPAddress, ObfuscatedPort) : null;

        /// <summary>
        ///     Gets the obfuscated peer-message port, if advertised.
        /// </summary>
        public int ObfuscatedPort { get; }

        /// <summary>
        ///     Gets the peer-message obfuscation type, if advertised.
        /// </summary>
        public int ObfuscationType { get; }

        /// <summary>
        ///     Gets a value indicating whether compatible obfuscated peer-message metadata was advertised.
        /// </summary>
        public bool HasObfuscatedEndpoint => ObfuscationType == 1 && ObfuscatedPort > 0 && ObfuscatedPort <= IPEndPoint.MaxPort;

        /// <summary>
        ///     Gets the IP address of the peer.
        /// </summary>
        public IPAddress IPAddress { get; }

        /// <summary>
        ///     Gets the IP endpoint of the peer.
        /// </summary>
        public IPEndPoint IPEndPoint { get; }

        /// <summary>
        ///     Gets the port on which the peer is listening.
        /// </summary>
        public int Port { get; }

        /// <summary>
        ///     Gets the requested peer username.
        /// </summary>
        public string Username { get; }

        /// <summary>
        ///     Creates a new instance of <see cref="UserAddressResponse"/> from the specified <paramref name="bytes"/>.
        /// </summary>
        /// <param name="bytes">The byte array from which to parse.</param>
        /// <returns>The created instance.</returns>
        public static UserAddressResponse FromByteArray(byte[] bytes)
        {
            var reader = new MessageReader<MessageCode.Server>(bytes);
            var code = reader.ReadCode();

            if (code != MessageCode.Server.GetPeerAddress)
            {
                throw new MessageException($"Message Code mismatch creating {nameof(UserAddressResponse)} (expected: {(int)MessageCode.Server.GetPeerAddress}, received: {(int)code})");
            }

            var username = reader.ReadString();

            var ipBytes = reader.ReadBytes(4);
            Array.Reverse(ipBytes);
            var ipAddress = new IPAddress(ipBytes);

            var port = reader.ReadInteger();
            var obfuscationType = 0;
            var obfuscatedPort = 0;

            if (reader.HasMoreData)
            {
                obfuscationType = reader.ReadInteger();
                obfuscatedPort = BitConverter.ToUInt16(reader.ReadBytes(2), 0);
            }

            return new UserAddressResponse(username, ipAddress, port, obfuscationType, obfuscatedPort);
        }
    }
}
