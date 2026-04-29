// <copyright file="IUsernameMatcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Users;

public interface IUsernameMatcher
{
    bool IsMatch(string username);
}
