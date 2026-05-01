// <copyright file="PeerObfuscationOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team.
//
//     This file is part of slskNet.Runtime, a modified version of Soulseek.NET.
//     Modified: Added type-1 peer-message obfuscation runtime options.
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
//     SPDX-FileCopyrightText: slskdN Team
//     SPDX-License-Identifier: GPL-3.0-only
// </copyright>

namespace Soulseek
{
    using System;
    using System.Net;

    /// <summary>
    ///     Options for Soulseek type-1 peer-message obfuscation.
    /// </summary>
    public sealed class PeerObfuscationOptions
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="PeerObfuscationOptions"/> class.
        /// </summary>
        /// <param name="enabled">A value indicating whether to advertise and accept obfuscated peer-message connections.</param>
        /// <param name="listenPort">The dedicated obfuscated peer-message listener port.</param>
        /// <param name="type">The obfuscation type to advertise. Type 1 is the rotated Soulseek peer-message transform.</param>
        /// <param name="advertiseRegularPort">A value indicating whether to advertise the regular peer-message port.</param>
        /// <param name="preferOutbound">A value indicating whether outbound peer-message dials should prefer compatible obfuscated endpoints.</param>
        public PeerObfuscationOptions(
            bool enabled = false,
            int listenPort = 0,
            int type = 1,
            bool advertiseRegularPort = true,
            bool preferOutbound = false)
        {
            if (enabled && type != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(type), "Only type 1 peer-message obfuscation is supported");
            }

            if (enabled && listenPort == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(listenPort), "Must be specified when peer obfuscation is enabled");
            }

            if (listenPort != 0 && (listenPort < 1024 || listenPort > IPEndPoint.MaxPort))
            {
                throw new ArgumentOutOfRangeException(nameof(listenPort), $"Must be zero or between 1024 and {IPEndPoint.MaxPort}");
            }

            if (enabled && !advertiseRegularPort)
            {
                throw new ArgumentException("The regular peer port must be advertised when peer obfuscation is enabled", nameof(advertiseRegularPort));
            }

            Enabled = enabled;
            ListenPort = listenPort;
            Type = type;
            AdvertiseRegularPort = advertiseRegularPort;
            PreferOutbound = preferOutbound;
        }

        /// <summary>
        ///     Gets a value indicating whether obfuscated peer-message support is enabled.
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        ///     Gets the dedicated obfuscated peer-message listener port.
        /// </summary>
        public int ListenPort { get; }

        /// <summary>
        ///     Gets the obfuscation type.
        /// </summary>
        public int Type { get; }

        /// <summary>
        ///     Gets a value indicating whether to advertise the regular peer-message port.
        /// </summary>
        public bool AdvertiseRegularPort { get; }

        /// <summary>
        ///     Gets a value indicating whether outbound peer-message dials prefer obfuscated endpoints.
        /// </summary>
        public bool PreferOutbound { get; }
    }
}
