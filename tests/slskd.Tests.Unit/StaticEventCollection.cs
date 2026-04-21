namespace slskd.Tests.Unit;

using Xunit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StaticEventCollection
{
    public const string Name = "StaticEvent";
}
