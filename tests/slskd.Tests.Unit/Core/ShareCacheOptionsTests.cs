// <copyright file="ShareCacheOptionsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Core;

using Xunit;

public class ShareCacheOptionsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(6, 3)]
    [InlineData(8, 4)]
    [InlineData(16, 4)]
    public void GetDefaultWorkers_ReturnsConservativeDefault(int processorCount, int expectedWorkers)
    {
        var workers = slskd.Options.SharesOptions.ShareCacheOptions.GetDefaultWorkers(processorCount);

        Assert.Equal(expectedWorkers, workers);
    }
}
