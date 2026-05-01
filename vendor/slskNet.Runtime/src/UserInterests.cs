// <copyright file="UserInterests.cs" company="JP Dillingham">
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
    ///     A user's liked and hated interests.
    /// </summary>
    public class UserInterests
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserInterests"/> class.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="liked">The liked interests.</param>
        /// <param name="hated">The hated interests.</param>
        public UserInterests(string username, IReadOnlyCollection<string> liked, IReadOnlyCollection<string> hated)
        {
            Username = username;
            Liked = liked;
            Hated = hated;
        }

        /// <summary>
        ///     Gets the hated interests.
        /// </summary>
        public IReadOnlyCollection<string> Hated { get; }

        /// <summary>
        ///     Gets the liked interests.
        /// </summary>
        public IReadOnlyCollection<string> Liked { get; }

        /// <summary>
        ///     Gets the username.
        /// </summary>
        public string Username { get; }
    }
}
