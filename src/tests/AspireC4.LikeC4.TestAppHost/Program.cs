var builder = DistributedApplication.CreateBuilder(args);
await builder.Build().RunAsync();

// Marker class so integration tests can reference this assembly.
public sealed partial class TestAppHostProgram { }
