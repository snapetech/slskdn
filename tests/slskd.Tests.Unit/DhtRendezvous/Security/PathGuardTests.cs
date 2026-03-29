// <copyright file="PathGuardTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous.Security;

using Xunit;
using DhtPathGuard = slskd.DhtRendezvous.Security.PathGuard;

public class PathGuardTests
{
    private const string TestRoot = "/home/user/downloads";

    [Theory]
    [InlineData("music%2f..%2f..%2fetc%2fpasswd")]
    [InlineData("%252e%252e%252fetc%252fpasswd")]
    public void SanitizeAndValidate_RejectsEncodedTraversal(string peerPath)
    {
        var result = DhtPathGuard.SanitizeAndValidate(peerPath, TestRoot);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("music%2f..%2f..%2fetc%2fpasswd")]
    [InlineData("%252e%252e%252fetc%252fpasswd")]
    public void ValidatePeerPath_RejectsEncodedTraversal(string peerPath)
    {
        var result = DhtPathGuard.ValidatePeerPath(peerPath, TestRoot);

        Assert.False(result.IsValid);
        Assert.Equal(slskd.DhtRendezvous.Security.PathGuard.PathViolationType.DirectoryTraversal, result.ViolationType);
    }
}
