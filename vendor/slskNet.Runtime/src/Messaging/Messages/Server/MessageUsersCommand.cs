// <copyright file="MessageUsersCommand.cs" company="JP Dillingham">
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
// </copyright>

namespace Soulseek.Messaging.Messages
{
    using System.Collections.Generic;

    /// <summary>
    ///     Sends a private message to multiple users.
    /// </summary>
    internal sealed class MessageUsersCommand : IOutgoingMessage
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MessageUsersCommand"/> class.
        /// </summary>
        /// <param name="usernames">The users to which the message is to be sent.</param>
        /// <param name="message">The message to send.</param>
        public MessageUsersCommand(IEnumerable<string> usernames, string message)
        {
            Usernames = usernames;
            Message = message;
        }

        /// <summary>
        ///     Gets the message to send.
        /// </summary>
        public string Message { get; }

        /// <summary>
        ///     Gets the users to which the message is to be sent.
        /// </summary>
        public IEnumerable<string> Usernames { get; }

        /// <summary>
        ///     Constructs a <see cref="byte"/> array from this message.
        /// </summary>
        /// <returns>The constructed byte array.</returns>
        public byte[] ToByteArray()
        {
            var usernames = new List<string>(Usernames);
            var builder = new MessageBuilder()
                .WriteCode(MessageCode.Server.MessageUsers)
                .WriteInteger(usernames.Count);

            foreach (var username in usernames)
            {
                builder.WriteString(username);
            }

            return builder
                .WriteString(Message)
                .Build();
        }
    }
}
