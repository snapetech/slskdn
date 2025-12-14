// <copyright file="RefactoringPlan.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Refactoring plan for identified hotspots.
    /// </summary>
    /// <remarks>
    ///     H-CODE04: Refactor Hotspots (OPTIONAL, Guided).
    ///     Provides structured refactoring recommendations based on hotspot analysis.
    /// </remarks>
    public static class RefactoringPlan
    {
        /// <summary>
        ///     Creates a comprehensive refactoring plan based on hotspot analysis.
        /// </summary>
        /// <param name="hotspots">The identified hotspots.</param>
        /// <returns>A structured refactoring plan.</returns>
        public static RefactoringPlanResult CreatePlan(IEnumerable<Hotspot> hotspots)
        {
            var plan = new RefactoringPlanResult
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                Hotspots = hotspots.ToList()
            };

            var recommendations = new List<RefactoringRecommendation>();

            foreach (var hotspot in hotspots)
            {
                var recommendation = CreateRecommendation(hotspot);
                if (recommendation != null)
                {
                    recommendations.Add(recommendation);
                }
            }

            plan.Recommendations = recommendations.OrderByDescending(r => r.Priority).ToList();
            plan.Summary = GenerateSummary(plan.Recommendations);

            return plan;
        }

        private static RefactoringRecommendation? CreateRecommendation(Hotspot hotspot)
        {
            return hotspot.Type switch
            {
                HotspotType.File => CreateFileRefactoring(hotspot),
                HotspotType.Class => CreateClassRefactoring(hotspot),
                HotspotType.Method => CreateMethodRefactoring(hotspot),
                _ => null
            };
        }

        private static RefactoringRecommendation? CreateFileRefactoring(Hotspot hotspot)
        {
            if (hotspot.Name?.Contains("Application.cs") == true)
            {
                return new RefactoringRecommendation
                {
                    Title = "Split Application.cs - The God Class",
                    Target = hotspot.Location ?? "Application.cs",
                    Priority = RefactoringPriority.Critical,
                    Rationale = "Application.cs has 1900+ lines and handles too many responsibilities: event handling, service coordination, state management, and background tasks.",
                    Approach = "Extract specialized handlers and services",
                    Tasks = new[]
                    {
                        "Extract SoulseekEventHandlers.cs for all Client_* event handlers",
                        "Extract ApplicationStateManager.cs for state management",
                        "Extract BackgroundTaskCoordinator.cs for periodic tasks",
                        "Extract ServiceCoordinator.cs for service initialization and dependencies",
                        "Keep Application.cs as thin orchestration layer"
                    },
                    EstimatedEffort = "4-5 days",
                    RiskLevel = "Medium (many event handlers to migrate)",
                    Benefits = new[]
                    {
                        "Improved testability - each handler can be tested independently",
                        "Better separation of concerns",
                        "Reduced merge conflicts in large file",
                        "Easier debugging and maintenance"
                    }
                };
            }

            if (hotspot.Name?.Contains("MeshTransportService.cs") == true)
            {
                return new RefactoringRecommendation
                {
                    Title = "Extract Transport Strategies from MeshTransportService",
                    Target = hotspot.Location ?? "MeshTransportService.cs",
                    Priority = RefactoringPriority.High,
                    Rationale = "MeshTransportService handles multiple transport protocols and selection logic, violating Single Responsibility Principle.",
                    Approach = "Extract transport-specific logic into strategy classes",
                    Tasks = new[]
                    {
                        "Create ITransportStrategy interface",
                        "Extract DirectQuicTransportStrategy.cs",
                        "Extract TorTransportStrategy.cs",
                        "Extract I2PTransportStrategy.cs",
                        "Extract RelayTransportStrategy.cs",
                        "Simplify MeshTransportService to use strategy pattern"
                    },
                    EstimatedEffort = "2-3 days",
                    RiskLevel = "Medium (transport logic is complex)",
                    Benefits = new[]
                    {
                        "Easier to add new transport protocols",
                        "Better testability of individual transports",
                        "Reduced complexity in main service",
                        "Improved maintainability"
                    }
                };
            }

            return new RefactoringRecommendation
            {
                Title = $"Refactor Large File: {hotspot.Name}",
                Target = hotspot.Location ?? "Unknown",
                Priority = RefactoringPriority.Medium,
                Rationale = "File exceeds recommended size limits for maintainability.",
                Approach = "Split into multiple focused files based on responsibility.",
                Tasks = new[] { "Analyze file responsibilities", "Extract cohesive units", "Update imports and references" },
                EstimatedEffort = "1-2 days",
                RiskLevel = "Low",
                Benefits = new[] { "Improved maintainability", "Reduced merge conflicts", "Better code organization" }
            };
        }

        private static RefactoringRecommendation? CreateClassRefactoring(Hotspot hotspot)
        {
            if (hotspot.Name?.Contains("MultipleResponsibilities") == true)
            {
                return new RefactoringRecommendation
                {
                    Title = "Apply Single Responsibility Principle",
                    Target = hotspot.Location ?? "Unknown",
                    Priority = RefactoringPriority.High,
                    Rationale = "Class handles multiple concerns, making it difficult to test and maintain.",
                    Approach = "Extract each responsibility into separate classes",
                    Tasks = new[]
                    {
                        "Identify distinct responsibilities",
                        "Create focused classes for each responsibility",
                        "Extract methods and dependencies",
                        "Update consumers to use new classes"
                    },
                    EstimatedEffort = "2-4 days",
                    RiskLevel = "Medium",
                    Benefits = new[]
                    {
                        "Improved testability",
                        "Better separation of concerns",
                        "Easier maintenance and extension",
                        "Reduced complexity"
                    }
                };
            }

            if (hotspot.Name?.Contains("HighComplexity") == true)
            {
                return new RefactoringRecommendation
                {
                    Title = "Reduce Constructor Complexity",
                    Target = hotspot.Location ?? "Unknown",
                    Priority = RefactoringPriority.Medium,
                    Rationale = "Class has too many dependencies, indicating tight coupling.",
                    Approach = "Use parameter object pattern or facade services",
                    Tasks = new[]
                    {
                        "Create parameter object for related dependencies",
                        "Extract facade services for complex dependency groups",
                        "Apply dependency injection best practices",
                        "Consider factory patterns for complex object creation"
                    },
                    EstimatedEffort = "1-2 days",
                    RiskLevel = "Low",
                    Benefits = new[]
                    {
                        "Reduced coupling",
                        "Improved testability with mocks",
                        "Better dependency management",
                        "Cleaner constructor signatures"
                    }
                };
            }

            if (hotspot.Name?.Contains("EventHandlerOverload") == true)
            {
                return new RefactoringRecommendation
                {
                    Title = "Refactor Event Handler Overload",
                    Target = hotspot.Location ?? "Unknown",
                    Priority = RefactoringPriority.Medium,
                    Rationale = "Class has too many event handlers, making it complex and hard to test.",
                    Approach = "Extract event handlers into dedicated handler classes",
                    Tasks = new[]
                    {
                        "Create IEventHandler interfaces for related events",
                        "Extract EventHandler classes with single responsibilities",
                        "Implement mediator pattern if handlers need coordination",
                        "Update event subscriptions"
                    },
                    EstimatedEffort = "1-3 days",
                    RiskLevel = "Low",
                    Benefits = new[]
                    {
                        "Improved testability of event handling",
                        "Better separation of event processing logic",
                        "Reduced class complexity",
                        "Easier debugging of event flows"
                    }
                };
            }

            return null;
        }

        private static RefactoringRecommendation? CreateMethodRefactoring(Hotspot hotspot)
        {
            return new RefactoringRecommendation
            {
                Title = "Refactor Complex Method",
                Target = hotspot.Location ?? "Unknown",
                Priority = RefactoringPriority.Low,
                Rationale = "Method is too complex or long.",
                Approach = "Extract method or apply composition",
                Tasks = new[] { "Identify extraction opportunities", "Create focused helper methods", "Update method documentation" },
                EstimatedEffort = "0.5-1 days",
                RiskLevel = "Low",
                Benefits = new[] { "Improved readability", "Better testability", "Reduced complexity" }
            };
        }

        private static RefactoringPlanSummary GenerateSummary(List<RefactoringRecommendation> recommendations)
        {
            return new RefactoringPlanSummary
            {
                TotalRecommendations = recommendations.Count,
                CriticalPriority = recommendations.Count(r => r.Priority == RefactoringPriority.Critical),
                HighPriority = recommendations.Count(r => r.Priority == RefactoringPriority.High),
                MediumPriority = recommendations.Count(r => r.Priority == RefactoringPriority.Medium),
                LowPriority = recommendations.Count(r => r.Priority == RefactoringPriority.Low),
                TotalEstimatedEffort = recommendations.Sum(r => ParseEffortDays(r.EstimatedEffort)),
                RiskDistribution = recommendations
                    .GroupBy(r => r.RiskLevel)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        private static double ParseEffortDays(string effort)
        {
            // Parse strings like "1-2 days", "0.5-1 days", "4-5 days"
            var parts = effort.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1 && double.TryParse(parts[0], out var days))
            {
                return days;
            }
            return 1.0; // Default
        }
    }

    /// <summary>
    ///     Result of refactoring plan generation.
    /// </summary>
    public sealed class RefactoringPlanResult
    {
        /// <summary>
        ///     Gets the timestamp when the plan was generated.
        /// </summary>
        public DateTimeOffset GeneratedAt { get; init; }

        /// <summary>
        ///     Gets the hotspots that informed this plan.
        /// </summary>
        public List<Hotspot> Hotspots { get; init; } = new();

        /// <summary>
        ///     Gets the refactoring recommendations.
        /// </summary>
        public List<RefactoringRecommendation> Recommendations { get; set; } = new();

        /// <summary>
        ///     Gets the summary of the plan.
        /// </summary>
        public RefactoringPlanSummary? Summary { get; set; }
    }

    /// <summary>
    ///     Summary of the refactoring plan.
    /// </summary>
    public sealed class RefactoringPlanSummary
    {
        /// <summary>
        ///     Gets the total number of recommendations.
        /// </summary>
        public int TotalRecommendations { get; init; }

        /// <summary>
        ///     Gets the number of critical priority recommendations.
        /// </summary>
        public int CriticalPriority { get; init; }

        /// <summary>
        ///     Gets the number of high priority recommendations.
        /// </summary>
        public int HighPriority { get; init; }

        /// <summary>
        ///     Gets the number of medium priority recommendations.
        /// </summary>
        public int MediumPriority { get; init; }

        /// <summary>
        ///     Gets the number of low priority recommendations.
        /// </summary>
        public int LowPriority { get; init; }

        /// <summary>
        ///     Gets the total estimated effort in days.
        /// </summary>
        public double TotalEstimatedEffort { get; init; }

        /// <summary>
        ///     Gets the distribution of recommendations by risk level.
        /// </summary>
        public Dictionary<string, int> RiskDistribution { get; init; } = new();
    }

    /// <summary>
    ///     A specific refactoring recommendation.
    /// </summary>
    public sealed class RefactoringRecommendation
    {
        /// <summary>
        ///     Gets the title of the recommendation.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        ///     Gets the target file/class to refactor.
        /// </summary>
        public string? Target { get; init; }

        /// <summary>
        ///     Gets the priority level.
        /// </summary>
        public RefactoringPriority Priority { get; init; }

        /// <summary>
        ///     Gets the rationale for the refactoring.
        /// </summary>
        public string? Rationale { get; init; }

        /// <summary>
        ///     Gets the recommended approach.
        /// </summary>
        public string? Approach { get; init; }

        /// <summary>
        ///     Gets the specific tasks to perform.
        /// </summary>
        public IEnumerable<string> Tasks { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Gets the estimated effort.
        /// </summary>
        public string? EstimatedEffort { get; init; }

        /// <summary>
        ///     Gets the risk level.
        /// </summary>
        public string? RiskLevel { get; init; }

        /// <summary>
        ///     Gets the expected benefits.
        /// </summary>
        public IEnumerable<string> Benefits { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    ///     Priority levels for refactoring recommendations.
    /// </summary>
    public enum RefactoringPriority
    {
        /// <summary>
        ///     Low priority - nice to have but not urgent.
        /// </summary>
        Low,

        /// <summary>
        ///     Medium priority - should do when time allows.
        /// </summary>
        Medium,

        /// <summary>
        ///     High priority - important for maintainability.
        /// </summary>
        High,

        /// <summary>
        ///     Critical priority - must do for codebase health.
        /// </summary>
        Critical
    }
}

