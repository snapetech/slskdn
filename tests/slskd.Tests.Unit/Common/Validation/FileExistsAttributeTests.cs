// <copyright file="FileExistsAttributeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Validation;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

public class FileExistsAttributeTests
{
    private sealed class TestOptions
    {
        [FileExists]
        public string File { get; init; } = string.Empty;
    }

    [Fact]
    public void Empty_string_is_treated_as_unset()
    {
        var options = new TestOptions { File = string.Empty };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void Whitespace_string_is_treated_as_unset()
    {
        var options = new TestOptions { File = "   " };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }
}
