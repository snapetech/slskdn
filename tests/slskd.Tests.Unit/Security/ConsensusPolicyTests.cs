// <copyright file="ConsensusPolicyTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Security;

using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Security;
using Xunit;

public class ConsensusPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_Allows_Operation_That_Does_Not_Require_Consensus()
    {
        var policy = new ConsensusPolicy(NullLogger<ConsensusPolicy>.Instance);

        var decision = await policy.EvaluateAsync(new SecurityContext
        {
            PeerId = "peer-1",
            Operation = "mesh-search",
        });

        Assert.True(decision.Allowed);
        Assert.Equal("consensus not required", decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_Denies_Consensus_Gated_Operation_When_Backend_Is_Unavailable()
    {
        var policy = new ConsensusPolicy(NullLogger<ConsensusPolicy>.Instance);

        var decision = await policy.EvaluateAsync(new SecurityContext
        {
            PeerId = "peer-1",
            Operation = "content-consensus-publish",
        });

        Assert.False(decision.Allowed);
        Assert.Equal("consensus verification unavailable", decision.Reason);
    }
}
