using System.Runtime.CompilerServices;

namespace Kadr.TenantIsolation.Tests;

internal static class DockerApiVersion
{
    [ModuleInitializer]
    internal static void PinCompatibleApiVersion()
    {
        // Older Docker Desktop engines (API 1.43) reject newer client defaults (1.44+).
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_API_VERSION")))
        {
            Environment.SetEnvironmentVariable("DOCKER_API_VERSION", "1.43");
        }
    }
}
