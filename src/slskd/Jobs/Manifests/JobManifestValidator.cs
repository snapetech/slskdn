namespace slskd.Jobs.Manifests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public interface IJobManifestValidator
    {
        (bool IsValid, List<string> Errors) Validate(JobManifest manifest);
    }

    public class JobManifestValidator : IJobManifestValidator
    {
        public (bool IsValid, List<string> Errors) Validate(JobManifest manifest)
        {
            var errors = new List<string>();

            if (manifest == null)
            {
                errors.Add("Manifest is null");
                return (false, errors);
            }

            if (manifest.ManifestVersion != "1.0")
            {
                errors.Add($"Unsupported manifest version: {manifest.ManifestVersion}");
            }

            if (string.IsNullOrWhiteSpace(manifest.JobId) || !Guid.TryParse(manifest.JobId, out _))
            {
                errors.Add("Invalid or missing job_id");
            }

            if (manifest.CreatedAt == default)
            {
                errors.Add("created_at is missing");
            }

            switch (manifest.JobType)
            {
                case JobType.MbRelease:
                    ValidateMbReleaseSpec(manifest.Spec as MbReleaseJobSpec, errors);
                    break;
                case JobType.Discography:
                    ValidateDiscographySpec(manifest.Spec as DiscographyJobSpec, errors);
                    break;
                case JobType.LabelCrate:
                    ValidateLabelCrateSpec(manifest.Spec as LabelCrateJobSpec, errors);
                    break;
                case JobType.MultiSource:
                    // Generic jobs: ensure spec exists
                    if (manifest.Spec == null)
                    {
                        errors.Add("Spec is required for multi-source job");
                    }
                    break;
                default:
                    errors.Add($"Unknown job_type: {manifest.JobType}");
                    break;
            }

            ValidateStatus(manifest.Status, errors);

            return (errors.Count == 0, errors);
        }

        private static void ValidateStatus(JobManifestStatus status, List<string> errors)
        {
            if (status == null)
            {
                errors.Add("status is required");
                return;
            }

            var validStates = new[] { "pending", "running", "completed", "failed", "cancelled" };
            if (string.IsNullOrWhiteSpace(status.State) || !validStates.Contains(status.State, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add("status.state must be one of: pending, running, completed, failed, cancelled");
            }

            if (status.BytesTotal < 0 || status.BytesDone < 0)
            {
                errors.Add("status bytes cannot be negative");
            }
        }

        private static void ValidateMbReleaseSpec(MbReleaseJobSpec spec, List<string> errors)
        {
            if (spec == null)
            {
                errors.Add("spec is required for mb_release");
                return;
            }

            if (string.IsNullOrWhiteSpace(spec.MbReleaseId))
            {
                errors.Add("spec.mb_release_id is required");
            }

            if (string.IsNullOrWhiteSpace(spec.TargetDir))
            {
                errors.Add("spec.target_dir is required");
            }

            if (spec.Tracks == null || spec.Tracks.Count == 0)
            {
                errors.Add("spec.tracks must include at least one track");
            }
        }

        private static void ValidateDiscographySpec(DiscographyJobSpec spec, List<string> errors)
        {
            if (spec == null)
            {
                errors.Add("spec is required for discography");
                return;
            }

            if (string.IsNullOrWhiteSpace(spec.ArtistId))
            {
                errors.Add("spec.artist_id is required");
            }

            if (string.IsNullOrWhiteSpace(spec.Profile))
            {
                errors.Add("spec.profile is required");
            }
        }

        private static void ValidateLabelCrateSpec(LabelCrateJobSpec spec, List<string> errors)
        {
            if (spec == null)
            {
                errors.Add("spec is required for label_crate");
                return;
            }

            if (string.IsNullOrWhiteSpace(spec.LabelId) && string.IsNullOrWhiteSpace(spec.LabelName))
            {
                errors.Add("spec.label_id or spec.label_name is required");
            }

            if (spec.Limit <= 0)
            {
                errors.Add("spec.limit must be positive");
            }
        }
    }
}
















