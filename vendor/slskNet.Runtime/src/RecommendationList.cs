// <copyright file="RecommendationList.cs" company="JP Dillingham">
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
    ///     A list of recommendations and unrecommendations from the server.
    /// </summary>
    public class RecommendationList
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RecommendationList"/> class.
        /// </summary>
        /// <param name="recommendations">The recommendations.</param>
        /// <param name="unrecommendations">The unrecommendations.</param>
        public RecommendationList(IReadOnlyCollection<Recommendation> recommendations, IReadOnlyCollection<Recommendation> unrecommendations)
        {
            Recommendations = recommendations;
            Unrecommendations = unrecommendations;
        }

        /// <summary>
        ///     Gets the recommendations.
        /// </summary>
        public IReadOnlyCollection<Recommendation> Recommendations { get; }

        /// <summary>
        ///     Gets the unrecommendations.
        /// </summary>
        public IReadOnlyCollection<Recommendation> Unrecommendations { get; }
    }
}
