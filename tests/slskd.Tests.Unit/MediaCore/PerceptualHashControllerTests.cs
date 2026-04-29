// <copyright file="PerceptualHashControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.MediaCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.MediaCore;
using slskd.MediaCore.API.Controllers;
using Xunit;

public class PerceptualHashControllerTests
{
    [Fact]
    public async Task ComputeAudioHash_TrimsAlgorithmBeforeParsing()
    {
        var hasher = new Mock<IPerceptualHasher>();
        hasher.Setup(x => x.ComputeAudioHash(It.IsAny<float[]>(), 44100, PerceptualHashAlgorithm.Chromaprint))
            .Returns(new PerceptualHash("Chromaprint", "ABCDEF0123456789", 0xABCDEF0123456789));

        var controller = new PerceptualHashController(Mock.Of<ILogger<PerceptualHashController>>(), hasher.Object);

        var result = await controller.ComputeAudioHash(new AudioHashRequest(new[] { 0.1f, 0.2f }, 44100, " Chromaprint "));

        Assert.IsType<OkObjectResult>(result);
        hasher.Verify(x => x.ComputeAudioHash(It.IsAny<float[]>(), 44100, PerceptualHashAlgorithm.Chromaprint), Times.Once);
    }

    [Fact]
    public async Task ComputeImageHash_TrimsAlgorithmBeforeParsing()
    {
        var hasher = new Mock<IPerceptualHasher>();
        hasher.Setup(x => x.ComputeImageHash(It.IsAny<byte[]>(), 8, 8, PerceptualHashAlgorithm.PHash))
            .Returns(new PerceptualHash("PHash", "ABCDEF0123456789", 0xABCDEF0123456789));

        var controller = new PerceptualHashController(Mock.Of<ILogger<PerceptualHashController>>(), hasher.Object);

        var result = await controller.ComputeImageHash(new ImageHashRequest(new byte[64], 8, 8, " PHash "));

        Assert.IsType<OkObjectResult>(result);
        hasher.Verify(x => x.ComputeImageHash(It.IsAny<byte[]>(), 8, 8, PerceptualHashAlgorithm.PHash), Times.Once);
    }
}
