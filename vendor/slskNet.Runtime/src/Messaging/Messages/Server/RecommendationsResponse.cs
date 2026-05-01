// <copyright file="RecommendationsResponse.cs" company="JP Dillingham">
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
    ///     A response containing recommendations and unrecommendations.
    /// </summary>
    internal sealed class RecommendationsResponse : IIncomingMessage
    {
        /// <summary>
        ///     Creates a new instance of <see cref="RecommendationList"/> from the specified <paramref name="bytes"/>.
        /// </summary>
        /// <param name="bytes">The byte array from which to parse.</param>
        /// <returns>The created instance.</returns>
        public static RecommendationList FromByteArray(byte[] bytes)
        {
            var reader = new MessageReader<MessageCode.Server>(bytes);
            var code = reader.ReadCode();

            if (code != MessageCode.Server.GetRecommendations && code != MessageCode.Server.GetGlobalRecommendations)
            {
                throw new MessageException($"Message Code mismatch creating {nameof(RecommendationsResponse)} (expected: {(int)MessageCode.Server.GetRecommendations} or {(int)MessageCode.Server.GetGlobalRecommendations}, received: {(int)code})");
            }

            var recommendations = ReadRecommendations(reader);
            var unrecommendations = ReadRecommendations(reader);

            return new RecommendationList(recommendations, unrecommendations);
        }

        private static IReadOnlyCollection<Recommendation> ReadRecommendations(MessageReader<MessageCode.Server> reader)
        {
            var count = ProtocolCountReader.ReadCount(reader, "recommendation", minimumBytesPerItem: 8);
            var recommendations = new List<Recommendation>();

            for (int i = 0; i < count; i++)
            {
                recommendations.Add(new Recommendation(reader.ReadString(), reader.ReadInteger()));
            }

            return recommendations.AsReadOnly();
        }
    }
}
