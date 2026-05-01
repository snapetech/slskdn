// <copyright file="ProtocolCountReader.cs" company="JP Dillingham">
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
    ///     Reads and validates protocol collection counts.
    /// </summary>
    internal static class ProtocolCountReader
    {
        /// <summary>
        ///     Reads a protocol collection count from the specified <paramref name="reader"/>.
        /// </summary>
        /// <param name="reader">The reader from which to read.</param>
        /// <param name="collectionName">The name of the collection being read.</param>
        /// <param name="minimumBytesPerItem">The minimum encoded bytes required for each collection item.</param>
        /// <returns>The validated count.</returns>
        public static int ReadCount(MessageReader<MessageCode.Server> reader, string collectionName, int minimumBytesPerItem)
        {
            var count = reader.ReadInteger();

            if (count < 0)
            {
                throw new MessageException($"Invalid {collectionName} count: {count}");
            }

            if (minimumBytesPerItem < 1)
            {
                throw new MessageException($"Invalid minimum item size for {collectionName}: {minimumBytesPerItem}");
            }

            var maximumPossibleCount = reader.Remaining / minimumBytesPerItem;

            if (count > maximumPossibleCount)
            {
                throw new MessageException($"Invalid {collectionName} count: {count} exceeds the maximum possible count of {maximumPossibleCount} for the remaining payload");
            }

            return count;
        }
    }
}
