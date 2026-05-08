var builder = DistributedApplication.CreateBuilder(args);

// Add LikeC4 visualization to the application.
// This will automatically generate a C4 model of the application and serve as a visualization resource.
builder.AddLikeC4Visualization();

var redis = builder.AddAzureManagedRedis("redis")
	// Run as container when local
	.RunAsContainer()
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(
		label: "Redis",
		technology: "Azure Redis",
		description: "A managed Redis instance for testing"
);
var postgres = builder.AddAzurePostgresFlexibleServer("posgres")
	// Run as container when local
	.RunAsContainer()
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(
		label: "Postgres",
		technology: "Azure Postgres",
		description: "A managed Postgres instance for testing"
);

builder
	.AddNodeApp("node-app", "../../../samples/node-app", "index.js")
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(
			label: "Node App",
			technology: "Node.js",
			description: "A sample Node.js application that connects to Redis and Postgres"
	)
	.WithPnpm(install: true)
	.WithHttpEndpoint(env: "PORT")
	// These references will be used to generate the connections in the C4 model and also ensure that the application waits for these dependencies to be ready before starting.
	.WithReference(redis)
	.WaitFor(redis)
	.WithReference(postgres)
	.WaitFor(postgres);

var app = builder.Build();

await app.RunAsync();



// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
