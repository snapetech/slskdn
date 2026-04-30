// <copyright file="OptionsOverlay.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

// <copyright file="OptionsOverlay.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using slskd.Validation;

    /// <summary>
    ///     Volatile run-time overlay for application <see cref="Options"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The values specified in this overlay are applied at run-time only, are volatile (lost when the application restarts),
    ///         and take precedence over all other options, regardless of which method was used to define them.
    ///     </para>
    ///     <para>
    ///         Only options that can be applied while the application is running can be overlaid, given the nature of how
    ///         an overlay is applied.
    ///     </para>
    ///     <para>
    ///         Every property in this class must be nullable, and must have a null default value; the application
    ///         selectively applies the patch overlay only to information that is explicitly supplied.
    ///     </para>
    /// </remarks>
    public record OptionsOverlay
    {
        /// <summary>
        ///     Gets options for the Soulseek client.
        /// </summary>
        [Validate]
        public SoulseekOptionsPatch? Soulseek { get; init; }

        /// <summary>
        ///     Gets options for external integrations.
        /// </summary>
        [Validate]
        public IntegrationOptionsPatch? Integration { get; init; }

        /// <summary>
        ///     Soulseek client options.
        /// </summary>
        public record SoulseekOptionsPatch
        {
            /// <summary>
            ///     Gets the local IP address on which to listen for incoming connections.
            /// </summary>
            [IPAddress]
            public string? ListenIpAddress { get; init; }

            /// <summary>
            ///     Gets the port on which to listen for incoming connections.
            /// </summary>
            [Range(1024, 65535)]
            public int? ListenPort { get; init; } = null;
        }

        /// <summary>
        ///     Integration options.
        /// </summary>
        public record IntegrationOptionsPatch
        {
            /// <summary>
            ///     Gets Spotify source-feed import options.
            /// </summary>
            [Validate]
            public SpotifyOptionsPatch? Spotify { get; init; }

            /// <summary>
            ///     Gets YouTube source-feed import options.
            /// </summary>
            [Validate]
            public YouTubeOptionsPatch? YouTube { get; init; }

            /// <summary>
            ///     Gets Last.fm source-feed import options.
            /// </summary>
            [Validate]
            public LastFmOptionsPatch? LastFm { get; init; }

            /// <summary>
            ///     Gets Pushbullet notification options.
            /// </summary>
            [Validate]
            public PushbulletOptionsPatch? Pushbullet { get; init; }

            /// <summary>
            ///     Gets Ntfy notification options.
            /// </summary>
            [Validate]
            public NtfyOptionsPatch? Ntfy { get; init; }

            /// <summary>
            ///     Gets Pushover notification options.
            /// </summary>
            [Validate]
            public PushoverOptionsPatch? Pushover { get; init; }

            /// <summary>
            ///     Gets FTP integration options.
            /// </summary>
            [Validate]
            public FtpOptionsPatch? Ftp { get; init; }

            /// <summary>
            ///     Spotify source-feed import options.
            /// </summary>
            public record SpotifyOptionsPatch
            {
                public bool? Enabled { get; init; }

                public string? ClientId { get; init; }

                public string? ClientSecret { get; init; }

                public string? RedirectUri { get; init; }

                [Range(1, 120)]
                public int? TimeoutSeconds { get; init; }

                [Range(1, 5000)]
                public int? MaxItemsPerImport { get; init; }

                public string? Market { get; init; }
            }

            /// <summary>
            ///     YouTube source-feed import options.
            /// </summary>
            public record YouTubeOptionsPatch
            {
                public bool? Enabled { get; init; }

                public string? ApiKey { get; init; }
            }

            /// <summary>
            ///     Last.fm source-feed import options.
            /// </summary>
            public record LastFmOptionsPatch
            {
                public bool? Enabled { get; init; }

                public string? ApiKey { get; init; }
            }

            /// <summary>
            ///     Pushbullet notification options.
            /// </summary>
            public record PushbulletOptionsPatch
            {
                public bool? Enabled { get; init; }

                [Secret]
                public string? AccessToken { get; init; }

                public string? NotificationPrefix { get; init; }

                public bool? NotifyOnPrivateMessage { get; init; }

                public bool? NotifyOnRoomMention { get; init; }

                [Range(0, 5)]
                public int? RetryAttempts { get; init; }

                [Range(0, int.MaxValue)]
                public int? CooldownTime { get; init; }
            }

            /// <summary>
            ///     Ntfy notification options.
            /// </summary>
            public record NtfyOptionsPatch
            {
                public bool? Enabled { get; init; }

                public string? Url { get; init; }

                [Secret]
                public string? AccessToken { get; init; }

                public string? NotificationPrefix { get; init; }

                public bool? NotifyOnPrivateMessage { get; init; }

                public bool? NotifyOnRoomMention { get; init; }
            }

            /// <summary>
            ///     Pushover notification options.
            /// </summary>
            public record PushoverOptionsPatch
            {
                public bool? Enabled { get; init; }

                [Secret]
                public string? UserKey { get; init; }

                [Secret]
                public string? Token { get; init; }

                public string? NotificationPrefix { get; init; }

                public bool? NotifyOnPrivateMessage { get; init; }

                public bool? NotifyOnRoomMention { get; init; }
            }

            /// <summary>
            ///     FTP integration options.
            /// </summary>
            public record FtpOptionsPatch
            {
                public bool? Enabled { get; init; }

                public string? Address { get; init; }

                [Range(1, 65535)]
                public int? Port { get; init; }

                public string? EncryptionMode { get; init; }

                public bool? IgnoreCertificateErrors { get; init; }

                public string? Username { get; init; }

                [Secret]
                public string? Password { get; init; }

                public string? RemotePath { get; init; }

                public bool? OverwriteExisting { get; init; }

                [Range(0, int.MaxValue)]
                public int? ConnectionTimeout { get; init; }

                [Range(0, 5)]
                public int? RetryAttempts { get; init; }
            }
        }
    }
}
