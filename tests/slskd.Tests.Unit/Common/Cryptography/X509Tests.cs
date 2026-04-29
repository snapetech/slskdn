// <copyright file="X509Tests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Common.Cryptography;

using slskd.Cryptography;
using Xunit;

public class X509Tests
{
    [Fact]
    public void TryValidate_WhenCertificateIsInvalid_DoesNotLeakParserDetails()
    {
        var isValid = X509.TryValidate("not-a-real-certificate", "secret", out var result);

        Assert.False(isValid);
        Assert.Equal("Invalid certificate", result);
        Assert.DoesNotContain("secret", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", result, StringComparison.OrdinalIgnoreCase);
    }
}
