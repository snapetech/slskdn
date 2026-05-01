// <copyright file="SetListenPortCommand.cs" company="JP Dillingham">
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
//     Modified: Added optional type-1 obfuscated port advertisement fields.
// </copyright>

namespace Soulseek.Messaging.Messages
{
    using System;
    using System.Net;

    /// <summary>
    ///     Advises the server of the local listen port.
    /// </summary>
    internal sealed class SetListenPortCommand : IOutgoingMessage
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SetListenPortCommand"/> class.
        /// </summary>
        /// <param name="port">The port on which to listen.</param>
        /// <param name="obfuscationType">The optional obfuscation type to advertise.</param>
        /// <param name="obfuscatedPort">The optional obfuscated peer-message port to advertise.</param>
        public SetListenPortCommand(int port, int? obfuscationType = null, int? obfuscatedPort = null)
        {
            if (port < 1024 || port > IPEndPoint.MaxPort)
            {
                throw new ArgumentOutOfRangeException(nameof(port), port, $"The port must be between 1024 and {IPEndPoint.MaxPort}");
            }

            if (obfuscationType.HasValue != obfuscatedPort.HasValue)
            {
                throw new ArgumentException("Obfuscation type and port must be supplied together");
            }

            if (obfuscationType < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(obfuscationType), obfuscationType, "The obfuscation type must be greater than or equal to zero");
            }

            if (obfuscatedPort.HasValue && (obfuscatedPort < 1024 || obfuscatedPort > IPEndPoint.MaxPort))
            {
                throw new ArgumentOutOfRangeException(nameof(obfuscatedPort), obfuscatedPort, $"The obfuscated port must be between 1024 and {IPEndPoint.MaxPort}");
            }

            Port = port;
            ObfuscationType = obfuscationType;
            ObfuscatedPort = obfuscatedPort;
        }

        /// <summary>
        ///     Gets the optional obfuscated port.
        /// </summary>
        public int? ObfuscatedPort { get; }

        /// <summary>
        ///     Gets the optional obfuscation type.
        /// </summary>
        public int? ObfuscationType { get; }

        /// <summary>
        ///     Gets the port on which to listen.
        /// </summary>
        public int Port { get; }

        /// <summary>
        ///     Constructs a <see cref="byte"/> array from this message.
        /// </summary>
        /// <returns>The constructed byte array.</returns>
        public byte[] ToByteArray()
        {
            var builder = new MessageBuilder()
                .WriteCode(MessageCode.Server.SetListenPort)
                .WriteInteger(Port);

            if (ObfuscationType.HasValue)
            {
                builder
                    .WriteInteger(ObfuscationType.Value)
                    .WriteInteger(ObfuscatedPort.Value);
            }

            return builder.Build();
        }
    }
}
