// <copyright file="RetryIncompleteStrategy.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

namespace slskd.Transfers;

/// <summary>
///     Strategy for retrying incomplete files.
/// </summary>
public enum RetryIncompleteStrategy
{
    /// <summary>
    ///     Overwrite the existing file.
    /// </summary>
    Overwrite = 0,

    /// <summary>
    ///     Resume the transfer using the size of the incomplete file as the initial offset.
    /// </summary>
    Resume = 1,
}
