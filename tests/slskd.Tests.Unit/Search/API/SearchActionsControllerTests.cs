// <copyright file="SearchActionsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Search.API;

using System.Reflection;
using slskd.Search.API;
using Xunit;

public class SearchActionsControllerTests
{
    [Theory]
    [InlineData("0", true, 0, 0)]
    [InlineData("2:3", true, 2, 3)]
    [InlineData(" 2 : 3 ", true, 2, 3)]
    [InlineData("0:-1", false, 0, 0)]
    [InlineData("-1", false, 0, 0)]
    [InlineData("-1:0", false, 0, 0)]
    [InlineData("abc", false, 0, 0)]
    [InlineData("1:two", false, 0, 0)]
    public void TryParseItemId_ValidatesResponseAndNonNegativeFileIndex(string itemId, bool expectedResult, int expectedResponseIndex, int expectedFileIndex)
    {
        var method = typeof(SearchActionsController).GetMethod("TryParseItemId", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var args = new object[] { itemId, 0, 0 };
        var result = (bool)method!.Invoke(null, args)!;

        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.Equal(expectedResponseIndex, (int)args[1]);
            Assert.Equal(expectedFileIndex, (int)args[2]);
        }
    }
}
