// <copyright file="CommonExtensionsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common;

using System.Linq;
using Xunit;

public class CommonExtensionsTests
{
    [Fact]
    public void DiffWith_WhenBothSidesAreNull_ReturnsNoDifferences()
    {
        var differences = ((object?)null).DiffWith(null);

        Assert.Empty(differences);
    }

    [Fact]
    public void DiffWith_WhenOneSideIsNull_ReturnsRootDifference()
    {
        var right = new { Enabled = true };

        var difference = ((object?)null).DiffWith(right, "feature").Single();

        Assert.Null(difference.Property);
        Assert.Equal("feature", difference.FQN);
        Assert.Null(difference.Left);
        Assert.Same(right, difference.Right);
    }
}
