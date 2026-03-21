// <copyright file="HashDbOptimizationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.HashDb.Optimization;

using slskd.HashDb.Optimization;
using Xunit;

public class HashDbOptimizationServiceTests
{
    [Fact]
    public void TryNormalizeProfileQuery_AcceptsSelectQuery()
    {
        var result = HashDbOptimizationService.TryNormalizeProfileQuery(
            "  select  *   from HashDb where size = @size  ",
            out var normalizedQuery,
            out var error);

        Assert.True(result);
        Assert.Equal("select * from HashDb where size = @size", normalizedQuery);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryNormalizeProfileQuery_RejectsMultipleStatements()
    {
        var result = HashDbOptimizationService.TryNormalizeProfileQuery(
            "SELECT * FROM HashDb; DROP TABLE HashDb",
            out _,
            out var error);

        Assert.False(result);
        Assert.Equal("Only single-statement SELECT queries are allowed.", error);
    }

    [Fact]
    public void TryNormalizeProfileQuery_RejectsMutatingStatements()
    {
        var result = HashDbOptimizationService.TryNormalizeProfileQuery(
            "DELETE FROM HashDb",
            out _,
            out var error);

        Assert.False(result);
        Assert.Equal("Only read-only SELECT queries are allowed.", error);
    }
}
