// <copyright file="RetryIncompleteStrategy.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Transfers;

/// <summary>
///     Determines how an existing incomplete file is handled when retrying a direct Soulseek download.
/// </summary>
public enum RetryIncompleteStrategy
{
    /// <summary>
    ///     Replace any existing incomplete file before retrying.
    /// </summary>
    Overwrite,

    /// <summary>
    ///     Continue from the existing incomplete file length when possible.
    /// </summary>
    Resume,
}
