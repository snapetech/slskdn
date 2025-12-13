namespace slskd.Tests.Integration;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Marker class for WebApplicationFactory with host builder.
/// </summary>
public class ProgramStub
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        // This will be overridden by StubWebApplicationFactory.CreateHostBuilder
        // but needs to exist for WebApplicationFactory to work
        return new HostBuilder();
    }
}
















