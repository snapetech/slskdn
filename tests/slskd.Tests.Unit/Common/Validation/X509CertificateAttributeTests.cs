// <copyright file="X509CertificateAttributeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Validation;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

public class X509CertificateAttributeTests
{
    private sealed class TestOptions
    {
        [X509Certificate]
        public Options.WebOptions.HttpsOptions.CertificateOptions Certificate { get; init; } = new();
    }

    [Fact]
    public void Invalid_certificate_does_not_leak_parser_details()
    {
        var options = new TestOptions
        {
            Certificate = new Options.WebOptions.HttpsOptions.CertificateOptions
            {
                Pfx = "not-a-real-certificate",
                Password = "secret"
            }
        };

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.False(isValid);
        var error = Assert.Single(results).ErrorMessage;
        Assert.Equal("Invalid HTTPs certificate", error);
        Assert.DoesNotContain("secret", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("base64", error, StringComparison.OrdinalIgnoreCase);
    }
}
