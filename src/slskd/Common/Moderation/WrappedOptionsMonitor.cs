// <copyright file="WrappedOptionsMonitor.cs" company="slskd Team">
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

namespace slskd.Common.Moderation
{
    using System;
    using Microsoft.Extensions.Options;

    /// <summary>
    ///     Simple wrapper for IOptions to expose as IOptionsMonitor.
    /// </summary>
    /// <typeparam name="T">The options type.</typeparam>
    /// <remarks>
    ///     This is a helper for DI registration when we have nested options
    ///     (e.g., Options.Moderation) that need to be passed to services
    ///     expecting IOptionsMonitor.
    /// </remarks>
    public class WrappedOptionsMonitor<T> : IOptionsMonitor<T>
        where T : class
    {
        private readonly IOptions<T> _options;

        public WrappedOptionsMonitor(IOptions<T> options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public T CurrentValue => _options.Value;

        public T Get(string? name) => _options.Value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

