// <copyright file="ItemRecommendationsRequest.cs" company="JP Dillingham">
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
    /// <summary>
    ///     Requests item recommendations or item similar users.
    /// </summary>
    internal sealed class ItemRecommendationsRequest : IOutgoingMessage
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ItemRecommendationsRequest"/> class.
        /// </summary>
        /// <param name="code">The message code.</param>
        /// <param name="item">The item.</param>
        public ItemRecommendationsRequest(MessageCode.Server code, string item)
        {
            if (code != MessageCode.Server.GetItemRecommendations && code != MessageCode.Server.GetItemSimilarUsers)
            {
                throw new MessageException($"Message Code mismatch creating {nameof(ItemRecommendationsRequest)} (expected: {(int)MessageCode.Server.GetItemRecommendations} or {(int)MessageCode.Server.GetItemSimilarUsers}, received: {(int)code})");
            }

            Code = code;
            Item = item;
        }

        /// <summary>
        ///     Gets the message code.
        /// </summary>
        public MessageCode.Server Code { get; }

        /// <summary>
        ///     Gets the item.
        /// </summary>
        public string Item { get; }

        /// <summary>
        ///     Constructs a <see cref="byte"/> array from this message.
        /// </summary>
        /// <returns>The constructed byte array.</returns>
        public byte[] ToByteArray()
        {
            return new MessageBuilder()
                .WriteCode(Code)
                .WriteString(Item)
                .Build();
        }
    }
}
