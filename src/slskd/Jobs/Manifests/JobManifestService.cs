namespace slskd.Jobs.Manifests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    public interface IJobManifestService
    {
        /// <summary>
        ///     Export a manifest to YAML under jobs/active or jobs/completed.
        /// </summary>
        /// <param name="manifest">The manifest to write.</param>
        /// <param name="completed">Whether to place under completed/ instead of active/.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Full path to the written file.</returns>
        Task<string> ExportAsync(JobManifest manifest, bool completed = false, CancellationToken ct = default);

        /// <summary>
        ///     Import and validate a manifest from YAML.
        /// </summary>
        /// <param name="filePath">Path to YAML manifest.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The deserialized manifest.</returns>
        Task<JobManifest> ImportAsync(string filePath, CancellationToken ct = default);
    }

    /// <summary>
    ///     YAML-based manifest import/export utility.
    /// </summary>
    public class JobManifestService : IJobManifestService
    {
        private readonly IJobManifestValidator validator;
        private readonly ISerializer serializer;
        private readonly IDeserializer deserializer;
        private readonly string jobsRoot;

        public JobManifestService(IJobManifestValidator validator)
        {
            this.validator = validator;

            serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .Build();

            deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            // Place manifests under <AppDirectory>/jobs/{active|completed}
            jobsRoot = Path.Combine(Program.AppDirectory ?? ".", "jobs");
        }

        public async Task<string> ExportAsync(JobManifest manifest, bool completed = false, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var (isValid, errors) = validator.Validate(manifest);
            if (!isValid)
            {
                throw new ArgumentException($"Manifest invalid: {string.Join("; ", errors)}", nameof(manifest));
            }

            var folder = Path.Combine(jobsRoot, completed ? "completed" : "active");
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"{manifest.JobId}.yaml");
            var yaml = serializer.Serialize(manifest);
            await File.WriteAllTextAsync(path, yaml, ct).ConfigureAwait(false);
            return path;
        }

        public async Task<JobManifest> ImportAsync(string filePath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath is required", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Manifest file not found", filePath);
            }

            var yaml = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            var manifest = deserializer.Deserialize<JobManifest>(yaml);

            var (isValid, errors) = validator.Validate(manifest);
            if (!isValid)
            {
                throw new ArgumentException($"Manifest invalid: {string.Join("; ", errors)}", nameof(filePath));
            }

            return manifest;
        }
    }
}
















