// <copyright file="JobManifestValidatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Jobs;

using slskd.Jobs.Manifests;
using Xunit;

public class JobManifestValidatorTests
{
    [Fact]
    public void Validate_WithUnsupportedManifestVersion_ReturnsSanitizedError()
    {
        var validator = new JobManifestValidator();

        var result = validator.Validate(new JobManifest
        {
            ManifestVersion = "9.9",
            JobId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            JobType = JobType.MultiSource,
            Spec = new { },
            Status = new JobManifestStatus { State = "pending" },
        });

        Assert.False(result.IsValid);
        Assert.Contains("Unsupported manifest version", result.Errors);
        Assert.DoesNotContain("9.9", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithUnknownJobType_ReturnsSanitizedError()
    {
        var validator = new JobManifestValidator();

        var result = validator.Validate(new JobManifest
        {
            ManifestVersion = "1.0",
            JobId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            JobType = (JobType)999,
            Status = new JobManifestStatus { State = "pending" },
        });

        Assert.False(result.IsValid);
        Assert.Contains("Unknown job type", result.Errors);
        Assert.DoesNotContain("999", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithInvalidStatusState_ReturnsSanitizedError()
    {
        var validator = new JobManifestValidator();

        var result = validator.Validate(new JobManifest
        {
            ManifestVersion = "1.0",
            JobId = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            JobType = JobType.MultiSource,
            Spec = new { },
            Status = new JobManifestStatus { State = "mystery" },
        });

        Assert.False(result.IsValid);
        Assert.Contains("Invalid status state", result.Errors);
        Assert.DoesNotContain("mystery", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
    }
}
