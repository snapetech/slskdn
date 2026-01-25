// <copyright file="FeatureNotImplementedException.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd;

using System;

/// <summary>
///     Thrown when an incomplete or not-yet-implemented feature is invoked.
///     The exception handler maps this to HTTP 501 Not Implemented.
/// </summary>
/// <remarks>
///     §11: Do not register incomplete features, or fail at startup, or return 501 at runtime.
///     Use this instead of <see cref="NotImplementedException"/> for feature-disabled paths
///     so the global handler can return 501 with a clear message.
/// </remarks>
public sealed class FeatureNotImplementedException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FeatureNotImplementedException"/> class.
    /// </summary>
    /// <param name="message">Human-readable description of the unimplemented feature.</param>
    public FeatureNotImplementedException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FeatureNotImplementedException"/> class.
    /// </summary>
    /// <param name="message">Human-readable description of the unimplemented feature.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public FeatureNotImplementedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
