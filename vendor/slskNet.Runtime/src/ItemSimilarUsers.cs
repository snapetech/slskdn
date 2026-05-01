// <copyright file="ItemSimilarUsers.cs" company="JP Dillingham">
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

namespace Soulseek
{
    using System.Collections.Generic;

    /// <summary>
    ///     Users similar to a specific item.
    /// </summary>
    public class ItemSimilarUsers
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ItemSimilarUsers"/> class.
        /// </summary>
        /// <param name="item">The item for which similar users were requested.</param>
        /// <param name="usernames">The similar usernames.</param>
        public ItemSimilarUsers(string item, IReadOnlyCollection<string> usernames)
        {
            Item = item;
            Usernames = usernames;
        }

        /// <summary>
        ///     Gets the item for which similar users were requested.
        /// </summary>
        public string Item { get; }

        /// <summary>
        ///     Gets the similar usernames.
        /// </summary>
        public IReadOnlyCollection<string> Usernames { get; }
    }
}
