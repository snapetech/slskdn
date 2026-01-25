// <copyright file="HotspotAnalysis.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Analysis of code hotspots for refactoring opportunities.
    /// </summary>
    /// <remarks>
    ///     H-CODE04: Refactor Hotspots (OPTIONAL, Guided).
    ///     Identifies files and classes that would benefit from refactoring.
    /// </remarks>
    public static class HotspotAnalysis
    {
        /// <summary>
        ///     Criteria for identifying hotspots.
        /// </summary>
        private static readonly HotspotCriteria[] Criteria = new[]
        {
            new HotspotCriteria
            {
                Name = "LargeFile",
                Description = "Files with excessive lines of code",
                Severity = HotspotSeverity.Medium,
                Condition = (Func<string, bool>)(file => GetLineCount(file) > 1000),
                Recommendation = "Consider splitting into multiple focused classes"
            },

            new HotspotCriteria
            {
                Name = "LargeClass",
                Description = "Classes with too many public methods",
                Severity = HotspotSeverity.Medium,
                Condition = (Func<Type, bool>)(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Length > 20),
                Recommendation = "Consider extracting methods into separate classes with single responsibilities"
            },

            new HotspotCriteria
            {
                Name = "HighComplexity",
                Description = "Classes with many dependencies (too many constructor parameters)",
                Severity = HotspotSeverity.High,
                Condition = (Func<Type, bool>)(type => GetConstructorParameterCount(type) > 10),
                Recommendation = "Consider parameter object pattern or dependency injection refactoring"
            },

            new HotspotCriteria
            {
                Name = "MultipleResponsibilities",
                Description = "Classes that handle multiple concerns",
                Severity = HotspotSeverity.High,
                Condition = (Func<Type, bool>)(type => HasMultipleResponsibilities(type)),
                Recommendation = "Apply Single Responsibility Principle - split into focused classes"
            },

            new HotspotCriteria
            {
                Name = "FrequentChanges",
                Description = "Files that have been frequently modified (indicating complexity)",
                Severity = HotspotSeverity.Medium,
                Condition = (Func<string, bool>)(file => GetGitChangeCount(file) > 10), // Would need git analysis
                Recommendation = "Consider simplifying the change-prone areas"
            },

            new HotspotCriteria
            {
                Name = "EventHandlerOverload",
                Description = "Classes with many event handlers",
                Severity = HotspotSeverity.Medium,
                Condition = (Func<Type, bool>)(type => GetEventHandlerCount(type) > 15),
                Recommendation = "Consider event handler aggregation or mediator pattern"
            }
        };

        /// <summary>
        ///     Analyzes the codebase for hotspots that could benefit from refactoring.
        /// </summary>
        /// <param name="assembly">The assembly to analyze.</param>
        /// <param name="logger">Optional logger.</param>
        /// <returns>Analysis of identified hotspots.</returns>
        public static HotspotAnalysisResult AnalyzeAssembly(Assembly assembly, ILogger? logger = null)
        {
            var result = new HotspotAnalysisResult
            {
                AssemblyName = assembly.GetName().Name ?? "Unknown",
                AnalysisTimestamp = DateTimeOffset.UtcNow
            };

            var types = assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsAbstract && !t.IsInterface &&
                           !t.Namespace?.Contains("Test", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            var hotspots = new List<Hotspot>();

            // Analyze types
            foreach (var type in types)
            {
                var typeHotspots = AnalyzeType(type, logger);
                hotspots.AddRange(typeHotspots);
            }

            // Analyze files
            var sourceFiles = GetSourceFiles(assembly);
            foreach (var file in sourceFiles)
            {
                var fileHotspots = AnalyzeFile(file, types, logger);
                hotspots.AddRange(fileHotspots);
            }

            result.Hotspots = hotspots.OrderByDescending(h => h.Severity).ThenBy(h => h.Name).ToList();
            result.Summary = GenerateSummary(result.Hotspots);

            logger?.LogInformation(
                "[HotspotAnalysis] Found {Count} hotspots in {Assembly}",
                result.Hotspots.Count, result.AssemblyName);

            return result;
        }

        private static IEnumerable<Hotspot> AnalyzeType(Type type, ILogger? logger)
        {
            var hotspots = new List<Hotspot>();

            foreach (var criterion in Criteria.Where(c => c.Condition is Func<Type, bool>))
            {
                var typeCondition = (Func<Type, bool>)criterion.Condition;
                if (typeCondition(type))
                {
                    hotspots.Add(new Hotspot
                    {
                        Name = $"{type.Name} ({criterion.Name})",
                        Location = type.FullName ?? "Unknown",
                        Type = HotspotType.Class,
                        Severity = criterion.Severity,
                        Issues = new[] { criterion.Description },
                        Recommendations = new[] { criterion.Recommendation },
                        Metrics = new Dictionary<string, object>
                        {
                            ["MethodCount"] = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Length,
                            ["DependencyCount"] = GetConstructorParameterCount(type),
                            ["EventHandlerCount"] = GetEventHandlerCount(type)
                        }
                    });
                }
            }

            return hotspots;
        }

        private static IEnumerable<Hotspot> AnalyzeFile(string filePath, List<Type> types, ILogger? logger)
        {
            var hotspots = new List<Hotspot>();

            foreach (var criterion in Criteria.Where(c => c.Condition is Func<string, bool>))
            {
                var fileCondition = (Func<string, bool>)criterion.Condition;
                if (fileCondition(filePath))
                {
                    var typesInFile = types.Where(t => GetSourceFile(t) == filePath).ToList();

                    hotspots.Add(new Hotspot
                    {
                        Name = $"{Path.GetFileName(filePath)} ({criterion.Name})",
                        Location = filePath,
                        Type = HotspotType.File,
                        Severity = criterion.Severity,
                        Issues = new[] { criterion.Description },
                        Recommendations = new[] { criterion.Recommendation },
                        Metrics = new Dictionary<string, object>
                        {
                            ["LineCount"] = GetLineCount(filePath),
                            ["TypeCount"] = typesInFile.Count,
                            ["TotalMethods"] = typesInFile.Sum(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Length)
                        }
                    });
                }
            }

            return hotspots;
        }

        private static int GetLineCount(string filePath)
        {
            try
            {
                return File.ReadAllLines(filePath).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static int GetConstructorParameterCount(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            return constructors.Any() ? constructors.Max(c => c.GetParameters().Length) : 0;
        }

        private static int GetEventHandlerCount(Type type)
        {
            return type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Count(m => m.Name.Contains("_") && (m.Name.Contains("Event") || m.Name.Contains("Handler")));
        }

        private static bool HasMultipleResponsibilities(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var methodNames = methods.Select(m => m.Name.ToLowerInvariant()).ToList();

            // Count different responsibility categories
            var responsibilityCategories = new[]
            {
                new[] { "save", "persist", "store", "write" },           // Data persistence
                new[] { "load", "read", "fetch", "get", "query" },      // Data retrieval
                new[] { "validate", "check", "verify" },                // Validation
                new[] { "send", "receive", "transmit", "handle" },      // Communication
                new[] { "process", "execute", "run", "start" },         // Processing
                new[] { "encrypt", "decrypt", "hash", "sign" },         // Security
                new[] { "log", "trace", "debug", "info" },              // Logging
                new[] { "parse", "serialize", "convert", "transform" }  // Data transformation
            };

            var matchedCategories = responsibilityCategories
                .Count(category => category.Any(keyword =>
                    methodNames.Any(name => name.Contains(keyword))));

            // If a class handles 4+ different responsibility categories, it's likely doing too much
            return matchedCategories >= 4;
        }

        private static int GetGitChangeCount(string filePath)
        {
            // Placeholder - would need git integration to get actual change count
            // For now, return 0 (would be implemented with git log --follow -- filePath | wc -l)
            return 0;
        }

        private static string GetSourceFile(Type type)
        {
            // Placeholder - would need source file mapping
            // For now, return a placeholder path
            return $"{type.Namespace?.Replace(".", "/") ?? "Unknown"}/{type.Name}.cs";
        }

        private static List<string> GetSourceFiles(Assembly assembly)
        {
            // Placeholder - would enumerate actual source files
            // For now, return known large files
            return new List<string>
            {
                "src/slskd/Application.cs",
                "src/slskd/Mesh/MeshTransportService.cs",
                "src/slskd/VirtualSoulfind/v2/Planning/MultiSourcePlanner.cs"
            };
        }

        private static HotspotAnalysisSummary GenerateSummary(List<Hotspot> hotspots)
        {
            return new HotspotAnalysisSummary
            {
                TotalHotspots = hotspots.Count,
                CriticalHotspots = hotspots.Count(h => h.Severity == HotspotSeverity.Critical),
                HighHotspots = hotspots.Count(h => h.Severity == HotspotSeverity.High),
                MediumHotspots = hotspots.Count(h => h.Severity == HotspotSeverity.Medium),
                LowHotspots = hotspots.Count(h => h.Severity == HotspotSeverity.Low),
                TopIssues = hotspots
                    .GroupBy(h => h.Issues.FirstOrDefault() ?? "Unknown")
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
    }

    /// <summary>
    ///     Criteria for identifying a hotspot.
    /// </summary>
    internal sealed class HotspotCriteria
    {
        /// <summary>
        ///     Gets the name of the criterion.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        ///     Gets the description of the criterion.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        ///     Gets the severity level.
        /// </summary>
        public HotspotSeverity Severity { get; init; }

        /// <summary>
        ///     Gets the condition to check (either for files or types).
        /// </summary>
        public object? Condition { get; init; }

        /// <summary>
        ///     Gets the recommendation for fixing the hotspot.
        /// </summary>
        public string? Recommendation { get; init; }
    }

    /// <summary>
    ///     Result of hotspot analysis.
    /// </summary>
    public sealed class HotspotAnalysisResult
    {
        /// <summary>
        ///     Gets the name of the analyzed assembly.
        /// </summary>
        public string? AssemblyName { get; init; }

        /// <summary>
        ///     Gets the timestamp when analysis was performed.
        /// </summary>
        public DateTimeOffset AnalysisTimestamp { get; init; }

        /// <summary>
        ///     Gets the list of identified hotspots.
        /// </summary>
        public List<Hotspot> Hotspots { get; set; } = new();

        /// <summary>
        ///     Gets the summary of the analysis.
        /// </summary>
        public HotspotAnalysisSummary? Summary { get; set; }
    }

    /// <summary>
    ///     Summary of hotspot analysis.
    /// </summary>
    public sealed class HotspotAnalysisSummary
    {
        /// <summary>
        ///     Gets the total number of hotspots.
        /// </summary>
        public int TotalHotspots { get; init; }

        /// <summary>
        ///     Gets the number of critical hotspots.
        /// </summary>
        public int CriticalHotspots { get; init; }

        /// <summary>
        ///     Gets the number of high-severity hotspots.
        /// </summary>
        public int HighHotspots { get; init; }

        /// <summary>
        ///     Gets the number of medium-severity hotspots.
        /// </summary>
        public int MediumHotspots { get; init; }

        /// <summary>
        ///     Gets the number of low-severity hotspots.
        /// </summary>
        public int LowHotspots { get; init; }

        /// <summary>
        ///     Gets the top issues by frequency.
        /// </summary>
        public Dictionary<string, int> TopIssues { get; init; } = new();
    }

    /// <summary>
    ///     Represents a code hotspot.
    /// </summary>
    public sealed class Hotspot
    {
        /// <summary>
        ///     Gets the name of the hotspot.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        ///     Gets the location (file path or type name).
        /// </summary>
        public string? Location { get; init; }

        /// <summary>
        ///     Gets the type of hotspot.
        /// </summary>
        public HotspotType Type { get; init; }

        /// <summary>
        ///     Gets the severity level.
        /// </summary>
        public HotspotSeverity Severity { get; init; }

        /// <summary>
        ///     Gets the list of issues identified.
        /// </summary>
        public IEnumerable<string> Issues { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Gets the list of recommendations.
        /// </summary>
        public IEnumerable<string> Recommendations { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Gets additional metrics about the hotspot.
        /// </summary>
        public Dictionary<string, object> Metrics { get; init; } = new();
    }

    /// <summary>
    ///     Type of hotspot.
    /// </summary>
    public enum HotspotType
    {
        /// <summary>
        ///     File-level hotspot.
        /// </summary>
        File,

        /// <summary>
        ///     Class-level hotspot.
        /// </summary>
        Class,

        /// <summary>
        ///     Method-level hotspot.
        /// </summary>
        Method
    }

    /// <summary>
    ///     Severity levels for hotspots.
    /// </summary>
    public enum HotspotSeverity
    {
        /// <summary>
        ///     Low priority - nice to refactor but not critical.
        /// </summary>
        Low,

        /// <summary>
        ///     Medium priority - should consider refactoring.
        /// </summary>
        Medium,

        /// <summary>
        ///     High priority - strong candidate for refactoring.
        /// </summary>
        High,

        /// <summary>
        ///     Critical priority - must refactor for maintainability.
        /// </summary>
        Critical
    }
}


