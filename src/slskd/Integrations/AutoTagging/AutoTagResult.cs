// <copyright file="AutoTagResult.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.AutoTagging
{
    /// <summary>
    ///     Result of an auto-tagging pass.
    /// </summary>
    public sealed record AutoTagResult(string FilePath, string Title, string Artist, bool Updated);
}



