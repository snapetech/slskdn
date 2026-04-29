// <copyright file="WrappedOptionsMonitor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
