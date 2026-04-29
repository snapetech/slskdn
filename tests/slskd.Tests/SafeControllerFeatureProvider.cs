// <copyright file="SafeControllerFeatureProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Common.CodeQuality;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

internal class SafeControllerFeatureProvider
    : ControllerFeatureProvider, IApplicationFeatureProvider<ControllerFeature>
{
    void IApplicationFeatureProvider<ControllerFeature>.PopulateFeature(
        IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var part in parts.OfType<IApplicationPartTypeProvider>())
        {
            IEnumerable<TypeInfo> types;
            try
            {
                types = part.Types.ToList();
            }
            catch
            {
                if (part is not AssemblyPart assemblyPart)
                {
                    continue;
                }

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
                    {
                        feature.Controllers.Add(type);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
