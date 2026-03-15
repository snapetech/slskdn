// <copyright file="SafeControllerFeatureProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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

namespace slskd.Common.CodeQuality
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.AspNetCore.Mvc.ApplicationParts;
    using Microsoft.AspNetCore.Mvc.Controllers;

    /// <summary>
    ///     A controller feature provider that gracefully handles assembly load failures
    ///     when enumerating types for controller discovery.
    ///
    ///     Required because the MSBuild task classes (CodeAnalysisBuildTask, etc.) live in this
    ///     assembly and inherit from <c>Microsoft.Build.Utilities.Task</c>, a build-time-only
    ///     dependency.  Patched versions of <c>Microsoft.Build.Utilities.Core</c> (18.x+) target
    ///     net9.0+, so their runtime DLL is absent from a net8.0 output directory.  ASP.NET Core's
    ///     default scanner calls <c>Module.GetTypes()</c> which throws before any individual type
    ///     is inspected.  This provider inherits <see cref="ControllerFeatureProvider"/> so the
    ///     ASP.NET Core guard (<c>!manager.FeatureProviders.OfType&lt;ControllerFeatureProvider&gt;().Any()</c>)
    ///     considers it present and does not add an additional default provider.  It also explicitly
    ///     reimplements <see cref="IApplicationFeatureProvider{ControllerFeature}"/> so that the
    ///     safe implementation is used for interface dispatch rather than the base-class version.
    /// </summary>
    internal class SafeControllerFeatureProvider
        : ControllerFeatureProvider, IApplicationFeatureProvider<ControllerFeature>
    {
        // Explicitly reimplement the interface so that interface-dispatch calls (which is how
        // ApplicationPartManager invokes feature providers) hit this safe implementation instead
        // of the base class's ControllerFeatureProvider.PopulateFeature, which calls
        // Module.GetTypes() and throws FileNotFoundException when a build-time assembly is absent.
        void IApplicationFeatureProvider<ControllerFeature>.PopulateFeature(
            IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
            {
                IEnumerable<TypeInfo> types;
                try
                {
                    // Normal path: works when all dependent assemblies are present at runtime
                    types = part.Types.ToList();
                }
                catch
                {
                    // Module.GetTypes() threw (likely FileNotFoundException for a missing build-time
                    // assembly). Fall back to Assembly.GetTypes() which wraps per-type failures in
                    // ReflectionTypeLoadException and returns the types that did load successfully.
                    if (part is not AssemblyPart assemblyPart)
                        continue;

                    try
                    {
                        types = assemblyPart.Assembly.GetTypes().Select(t => t.GetTypeInfo());
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).Select(t => t!.GetTypeInfo());
                    }
                    catch
                    {
                        continue;
                    }
                }

                foreach (var type in types)
                {
                    try
                    {
                        if (IsController(type) && !feature.Controllers.Contains(type))
                            feature.Controllers.Add(type);
                    }
                    catch (Exception)
                    {
                        // Skip types whose base classes or attributes can't be resolved
                    }
                }
            }
        }
    }
}
