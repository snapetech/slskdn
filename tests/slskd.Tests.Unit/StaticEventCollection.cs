// <copyright file="StaticEventCollection.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit;

using Xunit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StaticEventCollection
{
    public const string Name = "StaticEvent";
}
