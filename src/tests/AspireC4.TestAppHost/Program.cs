var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddAzureManagedRedis("redis").RunAsContainer();
var postgres = builder.AddAzurePostgresFlexibleServer("posgres").RunAsContainer();

var nodeApp = builder.AddNodeApp("node-app", "../../../samples/node-app", "index.js").WithPnpm();

nodeApp.WithReference(redis).WithReference(postgres);

var app = builder.Build();

await app.RunAsync();

// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
