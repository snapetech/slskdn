// <copyright file="EnumAttributeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Validation;

using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

public class EnumAttributeTests
{
    private enum TestMode
    {
        Alpha,
        Beta,
    }

    [Fact]
    public void GetValidationResult_WithInvalidScalarValue_ReturnsSanitizedMessage()
    {
        var attribute = new EnumAttribute(typeof(TestMode));
        var context = new ValidationContext(new object()) { DisplayName = "Mode" };

        var result = attribute.GetValidationResult("Gamma", context);

        Assert.NotNull(result);
        Assert.Equal("The Mode field is invalid", result!.ErrorMessage);
        Assert.DoesNotContain("Alpha", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gamma", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetValidationResult_WithInvalidArrayValue_ReturnsSanitizedMessage()
    {
        var attribute = new EnumAttribute(typeof(TestMode));
        var context = new ValidationContext(new object()) { DisplayName = "Modes" };

        var result = attribute.GetValidationResult(new[] { "Alpha", "Gamma" }, context);

        Assert.NotNull(result);
        Assert.Equal("The Modes field contains invalid values", result!.ErrorMessage);
        Assert.DoesNotContain("Alpha", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Gamma", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
