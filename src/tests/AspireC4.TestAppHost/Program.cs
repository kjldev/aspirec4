var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddAzureManagedRedis("redis").RunAsContainer();
var postgres = builder.AddAzurePostgresFlexibleServer("posgres").RunAsContainer();

builder
	.AddNodeApp("node-app", "../../../samples/node-app", "index.js")
	.WithPnpm()
	.WithHttpEndpoint(env: "PORT")
	.WithReference(redis)
	.WaitFor(redis)
	.WithReference(postgres)
	.WaitFor(postgres);

builder.AddLikeC4Visualization();

var app = builder.Build();

await app.RunAsync();

// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
