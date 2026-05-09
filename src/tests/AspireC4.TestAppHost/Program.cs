var builder = DistributedApplication.CreateBuilder(args);

// Add LikeC4 visualization to the application. This will allow us to visualize the components and their relationships in a C4 model.
builder.AddLikeC4Visualization(configure: opts =>
{
	var title = builder.Configuration["LikeC4:Title"];
	if (!string.IsNullOrWhiteSpace(title))
	{
		opts.Title = title;
	}

	var outputDirectory = builder.Configuration["LikeC4:OutputDirectory"];
	if (!string.IsNullOrWhiteSpace(outputDirectory))
	{
		opts.OutputDirectory = outputDirectory;
	}

	var fileName = builder.Configuration["LikeC4:FileName"];
	if (!string.IsNullOrWhiteSpace(fileName))
	{
		opts.FileName = fileName;
	}
});

var redis = builder
	.AddAzureManagedRedis("redis")
	// Run as container when local
	.RunAsContainer()
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(label: "Redis", technology: "Azure Redis", description: "A managed Redis instance for testing");

var postgres = builder
	.AddAzurePostgresFlexibleServer("postgres")
	// Run as container when local
	.RunAsContainer()
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(static c4 =>
		c4.WithLabel("Postgres").WithDescription("A managed Postgres instance for testing")
	);

builder
	.AddNodeApp("node-app", "../../../samples/node-app", "index.js")
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(
		label: "Node App",
		technology: "Node.js",
		description: "A sample Node.js application that connects to Redis and Postgres",
		icon: "tech:nodejs"
	)
	.WithPnpm(install: true)
	.WithHttpEndpoint(env: "PORT")
	// These references will be used to generate the connections in the C4 model and also ensure that the application waits for these dependencies to be ready before starting.
	.WithReference(redis)
	.WaitFor(redis)
	.WithLikeC4Reference(redis, opts => opts
		.WithLabel("Caches sessions")
		.WithTechnology("Redis Protocol"))
	.WithReference(postgres)
	.WaitFor(postgres)
	.WithLikeC4Reference(postgres, opts => opts
		.WithLabel("Persists data")
		.WithTechnology("PostgreSQL / JDBC"));

var app = builder.Build();

await app.RunAsync();

// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
