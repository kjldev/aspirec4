var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddAzureManagedRedis("redis").RunAsContainer().WithLikeC4Details(label: "Redis", technology: "Azure Redis", description: "A managed Redis instance for testing");
var postgres = builder.AddAzurePostgresFlexibleServer("posgres").RunAsContainer().WithLikeC4Details(label: "Postgres", technology: "Azure Postgres", description: "A managed Postgres instance for testing");

builder
	.AddNodeApp("node-app", "../../../samples/node-app", "index.js")
	.WithPnpm()
	.WithHttpEndpoint(env: "PORT")
	.WithLikeC4Details(label: "Node App", technology: "Node.js", description: "A sample Node.js application that connects to Redis and Postgres")
	.WithReference(redis)
	.WaitFor(redis)
	.WithReference(postgres)
	.WaitFor(postgres);

builder.AddLikeC4Visualization();

var app = builder.Build();

await app.RunAsync();

// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
