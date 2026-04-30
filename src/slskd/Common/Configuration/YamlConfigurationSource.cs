// <copyright file="YamlConfigurationSource.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

// <copyright file="YamlConfigurationSource.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Configuration
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.FileProviders.Physical;
    using YamlDotNet.Core;
    using YamlDotNet.RepresentationModel;
    using YamlDotNet.Serialization;

    /// <summary>
    ///     Extension methods for adding <see cref="YamlConfigurationProvider"/>.
    /// </summary>
    public static class YamlConfigurationExtensions
    {
        /// <summary>
        ///     Adds a YAML configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add .</param>
        /// <param name="path">
        ///     Path relative to the base path stored in <see cref="IConfigurationBuilder.Properties"/> of <paramref name="builder"/>.
        /// </param>
        /// <param name="targetType">The type from which to map properties.</param>
        /// <param name="optional">Whether the file is optional.</param>
        /// <param name="reloadOnChange">Whether the configuration should be reloaded if the file changes.</param>
        /// <param name="normalizeKeys">
        ///     A value indicating whether configuration keys should be normalized (_, - removed, changed to lowercase).
        /// </param>
        /// <param name="provider">The updated <see cref="IFileProvider"/> to use to access the file.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string path, Type targetType, bool optional = true, bool reloadOnChange = false, bool normalizeKeys = true, IFileProvider? provider = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("File path must be a non-empty string.", nameof(path));
            }

            return builder.AddYamlFile(s =>
            {
                s.Path = path;
                s.TargetType = targetType;
                s.Optional = optional;
                s.ReloadOnChange = reloadOnChange;
                s.NormalizeKeys = normalizeKeys;
                s.FileProvider = provider;
                s.ResolveFileProvider();
            });
        }

        /// <summary>
        ///     Adds a YAML configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, Action<YamlConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     A YAML file based <see cref="FileConfigurationProvider"/>.
    /// </summary>
    public class YamlConfigurationProvider : FileConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="YamlConfigurationProvider"/> class.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public YamlConfigurationProvider(YamlConfigurationSource source)
            : base(source)
        {
            TargetType = source.TargetType;
            Namespace = (TargetType.Namespace ?? string.Empty).Split('.').First();
            NormalizeKeys = source.NormalizeKeys;
        }

        private Type TargetType { get; set; }
        private string Namespace { get; set; }
        private bool NormalizeKeys { get; set; }
        private string[] NullValues { get; } = new[] { "~", "null", string.Empty };

        /// <summary>
        ///     Loads the YAML data from a stream.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        public override void Load(Stream stream)
        {
            try
            {
                // clear the data collection before we populate
                // not doing this will cause array and dictionary keys
                // to get "stuck" when the config is reloaded
                Data.Clear();

                // WORKAROUND: PhysicalFileProvider may cache file content, causing stale data.
                // Always read the file directly from disk to bypass any caching issues.
                string? filePath = null;
                if (Source is YamlConfigurationSource yamlSource && !string.IsNullOrEmpty(yamlSource.Path))
                {
                    // Try to resolve the full path from the file provider
                    if (yamlSource.FileProvider is PhysicalFileProvider physicalProvider)
                    {
                        var fileInfo = physicalProvider.GetFileInfo(yamlSource.Path);
                        if (fileInfo.Exists && !string.IsNullOrEmpty(fileInfo.PhysicalPath))
                        {
                            filePath = fileInfo.PhysicalPath;
                        }
                    }

                    // Fallback: try to construct path from file provider root
                    if (string.IsNullOrEmpty(filePath) && yamlSource.FileProvider is PhysicalFileProvider physicalProvider2)
                    {
                        var root = physicalProvider2.Root;
                        filePath = Path.Combine(root, yamlSource.Path);
                    }
                }

                Stream? actualStream = stream;
                MemoryStream? memoryStream = null;
                try
                {
                    // FIX: PhysicalFileProvider may cache file content, causing stale data when files are modified.
                    // Read the file directly from disk to ensure we always get the latest content.
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        var fileContent = File.ReadAllText(filePath);

                        memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
                        actualStream = memoryStream;
                    }

                    using var reader = new StreamReader(actualStream);

                    var yaml = new YamlStream();
                    yaml.Load(reader);

                    if (yaml.Documents.Count > 0)
                    {
                        var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
                        Traverse(rootNode, Namespace, TargetType);
                        AddCompatibilityKeys();
                    }
                }
                finally
                {
                    memoryStream?.Dispose();
                }
            }
            catch (YamlException e)
            {
                throw new FormatException("Could not parse the YAML file.", e);
            }
        }

        private void Traverse(YamlNode root, string? path = null, Type? targetType = null)
        {
            if (root is YamlScalarNode scalar)
            {
                var value = scalar.Value == null || NullValues.Contains(scalar.Value.ToLower()) ? null : scalar.Value;

                if (value != null)
                {
                    var normalizedPath = Normalize(path);
                    var storedValue = scalar.Value == null || NullValues.Contains(scalar.Value.ToLower()) ? null : scalar.Value;
                    if (normalizedPath != null)
                    {
                        Data[normalizedPath] = storedValue;
                    }
                }
            }
            else if (root is YamlMappingNode map)
            {
                foreach (var node in map.Children)
                {
                    var rawKey = ((YamlScalarNode)node.Key).Value;
                    var key = ResolveConfigurationKey(targetType, rawKey);
                    var nextPath = path == null ? key : ConfigurationPath.Combine(path, key ?? string.Empty);
                    Traverse(node.Value, nextPath, GetChildType(targetType, key));
                }
            }
            else if (root is YamlSequenceNode sequence)
            {
                var elementType = GetSequenceElementType(targetType);

                for (int i = 0; i < sequence.Children.Count; i++)
                {
                    Traverse(sequence.Children[i], ConfigurationPath.Combine(path ?? string.Empty, i.ToString()), elementType);
                }
            }
        }

        private string? Normalize(string? str) => NormalizeKeys ? str?.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant() : str;

        private string? ResolveConfigurationKey(Type? targetType, string? rawKey)
        {
            var normalizedKey = Normalize(rawKey);

            if (targetType == null || IsDictionary(targetType))
            {
                return normalizedKey;
            }

            var property = targetType
                .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p =>
                    string.Equals(Normalize(p.Name), normalizedKey, StringComparison.Ordinal) ||
                    string.Equals(GetYamlAlias(p), normalizedKey, StringComparison.Ordinal));

            return property == null ? normalizedKey : Normalize(property.Name);
        }

        private Type? GetChildType(Type? targetType, string? normalizedKey)
        {
            if (targetType == null)
            {
                return null;
            }

            if (IsDictionary(targetType))
            {
                return targetType.GetGenericArguments().Length == 2 ? targetType.GetGenericArguments()[1] : null;
            }

            return targetType
                .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => string.Equals(Normalize(p.Name), normalizedKey, StringComparison.Ordinal))
                ?.PropertyType;
        }

        private static Type? GetSequenceElementType(Type? targetType)
        {
            if (targetType == null)
            {
                return null;
            }

            if (targetType.IsArray)
            {
                return targetType.GetElementType();
            }

            return targetType.IsGenericType ? targetType.GetGenericArguments().FirstOrDefault() : null;
        }

        private string? GetYamlAlias(PropertyInfo property)
        {
            var alias = property.GetCustomAttribute<YamlMemberAttribute>()?.Alias;
            return string.IsNullOrWhiteSpace(alias) ? null : Normalize(alias);
        }

        private void AddCompatibilityKeys()
        {
            foreach (var item in Data.ToArray())
            {
                AddCompatibilityKey(item.Key, $":{Namespace}:transfers:upload:limits:", $":{Namespace}:global:limits:");
                AddCompatibilityKey(item.Key, $":{Namespace}:transfers:groups:", $":{Namespace}:groups:");
                AddCompatibilityKey(item.Key, $":{Namespace}:groups:", $":{Namespace}:transfers:groups:");
                AddCompatibilityKey(item.Key, $":{Namespace}:global:upload:limits:", $":{Namespace}:global:limits:");
                AddCompatibilityKey(item.Key, $":{Namespace}:global:groups:", $":{Namespace}:groups:");
            }

            foreach (var item in Data.ToArray())
            {
                if (!item.Key.StartsWith($"{Namespace}:groups:", StringComparison.Ordinal) &&
                    !item.Key.StartsWith($"{Namespace}:transfers:groups:", StringComparison.Ordinal))
                {
                    continue;
                }

                const string UploadLimits = ":upload:limits:";
                var index = item.Key.IndexOf(UploadLimits, StringComparison.Ordinal);

                if (index < 0)
                {
                    continue;
                }

                var groupLimitKey = item.Key.Remove(index + 1, "upload:".Length);
                if (!Data.ContainsKey(groupLimitKey))
                {
                    Data[groupLimitKey] = item.Value;
                }
            }
        }

        private void AddCompatibilityKey(string sourceKey, string sourcePattern, string targetPattern)
        {
            var prefixedSourceKey = $":{sourceKey}";
            var index = prefixedSourceKey.IndexOf(sourcePattern, StringComparison.Ordinal);
            if (index < 0)
            {
                return;
            }

            var targetKey = prefixedSourceKey.Remove(index, sourcePattern.Length).Insert(index, targetPattern).TrimStart(':');
            if (!Data.ContainsKey(targetKey))
            {
                Data[targetKey] = Data[sourceKey];
            }
        }

        private static bool IsDictionary(Type type) => typeof(IDictionary).IsAssignableFrom(type) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>));
    }

    /// <summary>
    ///     Represents a YAML file as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class YamlConfigurationSource : FileConfigurationSource
    {
        /// <summary>
        ///     Gets or sets the type from which to map properties.
        /// </summary>
        public Type TargetType { get; set; } = typeof(object);

        /// <summary>
        ///     Gets or sets a value indicating whether configuration keys should be normalized (_, - removed, changed to lowercase).
        /// </summary>
        public bool NormalizeKeys { get; set; }

        /// <summary>
        ///     Builds the <see cref="YamlConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="YamlConfigurationProvider"/>.</returns>
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new YamlConfigurationProvider(this);
        }
    }
}
