// <copyright file="BridgeFlowTypesTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Realm.Bridge
{
    using System.Linq;
    using slskd.Mesh.Realm.Bridge;
    using Xunit;

    /// <summary>
    ///     Tests for T-REALM-04: BridgeFlowTypes.
    /// </summary>
    public class BridgeFlowTypesTests
    {
        [Theory]
        [InlineData("activitypub:read", true)]
        [InlineData("activitypub:write", true)]
        [InlineData("metadata:read", true)]
        [InlineData("search:read", true)]
        [InlineData("content:read", true)]
        [InlineData("content:share", true)]
        [InlineData("social:read", true)]
        [InlineData("social:interact", true)]
        public void AllFlows_ContainsExpectedFlows(string flow, bool expected)
        {
            // Assert
            Assert.Equal(expected, BridgeFlowTypes.AllFlows.Contains(flow));
        }

        [Theory]
        [InlineData("activitypub:read", true)]
        [InlineData("metadata:read", true)]
        [InlineData("search:read", true)]
        [InlineData("activitypub:write", false)]
        [InlineData("content:share", false)]
        [InlineData("social:interact", false)]
        public void SafeFlows_ContainsExpectedFlows(string flow, bool expected)
        {
            // Assert
            Assert.Equal(expected, BridgeFlowTypes.SafeFlows.Contains(flow));
        }

        [Theory]
        [InlineData("activitypub:write", true)]
        [InlineData("content:share", true)]
        [InlineData("social:interact", true)]
        [InlineData("activitypub:read", false)]
        [InlineData("metadata:read", false)]
        [InlineData("search:read", false)]
        public void DangerousFlows_ContainsExpectedFlows(string flow, bool expected)
        {
            // Assert
            Assert.Equal(expected, BridgeFlowTypes.DangerousFlows.Contains(flow));
        }

        [Theory]
        [InlineData("governance:root", true)]
        [InlineData("governance:read", true)]
        [InlineData("governance:write", true)]
        [InlineData("config:read", true)]
        [InlineData("config:write", true)]
        [InlineData("mcp:read", true)]
        [InlineData("mcp:write", true)]
        [InlineData("mcp:control", true)]
        [InlineData("replication:read", true)]
        [InlineData("replication:write", true)]
        [InlineData("replication:fullcopy", true)]
        [InlineData("activitypub:read", false)]
        [InlineData("metadata:read", false)]
        public void AlwaysForbiddenFlows_ContainsExpectedFlows(string flow, bool expected)
        {
            // Assert
            Assert.Equal(expected, BridgeFlowTypes.AlwaysForbiddenFlows.Contains(flow));
        }

        [Theory]
        [InlineData("activitypub:read", true)]
        [InlineData("metadata:read", true)]
        [InlineData("governance:write", true)]
        [InlineData("invalid", false)]
        [InlineData("no-colon", false)]
        [InlineData("too:many:colons:here", false)]
        [InlineData(":empty-category", false)]
        [InlineData("empty-action:", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidFlow_ValidatesFlowFormat(string flow, bool expected)
        {
            // Act
            var result = BridgeFlowTypes.IsValidFlow(flow);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("activitypub:read", "activitypub", "read")]
        [InlineData("metadata:read", "metadata", "read")]
        [InlineData("governance:write", "governance", "write")]
        [InlineData("content:share", "content", "share")]
        public void GetFlowCategory_ReturnsCorrectCategory(string flow, string expectedCategory, string expectedAction)
        {
            // Act
            var category = BridgeFlowTypes.GetFlowCategory(flow);
            var action = BridgeFlowTypes.GetFlowAction(flow);

            // Assert
            Assert.Equal(expectedCategory, category);
            Assert.Equal(expectedAction, action);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData("no-colon")]
        [InlineData(":empty-category")]
        [InlineData("empty-action:")]
        [InlineData("")]
        [InlineData(null)]
        public void GetFlowCategory_WithInvalidFlow_ReturnsNull(string flow)
        {
            // Act
            var category = BridgeFlowTypes.GetFlowCategory(flow);
            var action = BridgeFlowTypes.GetFlowAction(flow);

            // Assert
            Assert.Null(category);
            Assert.Null(action);
        }

        [Theory]
        [InlineData("activitypub:read", "activitypub")]
        [InlineData("metadata:read", "metadata")]
        [InlineData("search:read", "search")]
        [InlineData("content:share", "content")]
        [InlineData("social:interact", "social")]
        public void GetFlowCategory_GroupsFlowsByCategory(string flow, string expectedCategory)
        {
            // Act
            var category = BridgeFlowTypes.GetFlowCategory(flow);

            // Assert
            Assert.Equal(expectedCategory, category);
        }

        [Fact]
        public void AllFlows_NoDuplicates()
        {
            // Assert
            var uniqueFlows = BridgeFlowTypes.AllFlows.Distinct().Count();
            Assert.Equal(BridgeFlowTypes.AllFlows.Count, uniqueFlows);
        }

        [Fact]
        public void SafeFlows_SubsetOfAllFlows()
        {
            // Assert - All safe flows should be in the complete set
            foreach (var safeFlow in BridgeFlowTypes.SafeFlows)
            {
                Assert.Contains(safeFlow, BridgeFlowTypes.AllFlows);
            }
        }

        [Fact]
        public void DangerousFlows_SubsetOfAllFlows()
        {
            // Assert - All dangerous flows should be in the complete set
            foreach (var dangerousFlow in BridgeFlowTypes.DangerousFlows)
            {
                Assert.Contains(dangerousFlow, BridgeFlowTypes.AllFlows);
            }
        }

        [Fact]
        public void AlwaysForbiddenFlows_DisjointFromSafeFlows()
        {
            // Assert - Forbidden flows should not be in safe flows
            foreach (var forbiddenFlow in BridgeFlowTypes.AlwaysForbiddenFlows)
            {
                Assert.DoesNotContain(forbiddenFlow, BridgeFlowTypes.SafeFlows);
            }
        }

        [Fact]
        public void AlwaysForbiddenFlows_DisjointFromDangerousFlows()
        {
            // Assert - Forbidden flows should not be in dangerous flows
            foreach (var forbiddenFlow in BridgeFlowTypes.AlwaysForbiddenFlows)
            {
                Assert.DoesNotContain(forbiddenFlow, BridgeFlowTypes.DangerousFlows);
            }
        }

        [Fact]
        public void FlowConstants_MatchExpectedValues()
        {
            // Assert - Verify the string constants are correct
            Assert.Equal("activitypub:read", BridgeFlowTypes.ActivityPubRead);
            Assert.Equal("activitypub:write", BridgeFlowTypes.ActivityPubWrite);
            Assert.Equal("metadata:read", BridgeFlowTypes.MetadataRead);
            Assert.Equal("search:read", BridgeFlowTypes.SearchRead);
        }
    }
}


