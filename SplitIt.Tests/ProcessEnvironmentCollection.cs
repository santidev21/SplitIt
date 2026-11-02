namespace SplitIt.Tests;

/// <summary>
/// Test classes that set process-wide environment variables (JWT/CORS settings read by
/// Program.cs before WebApplicationFactory can override configuration) join this collection so
/// xUnit runs them one at a time instead of racing on the same variables.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "ProcessEnvironment";
}
