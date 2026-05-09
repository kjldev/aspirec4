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

var azureManagerRedis = builder
	.AddAzureManagedRedis("azure-redis")
	// Run as container when local
	.RunAsContainer()
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(label: "Redis", technology: "Azure Redis", description: "A managed Redis instance for testing");

var azurePostgres = builder
	.AddAzurePostgresFlexibleServer("azure-postgres")
	// Run as container when local
	.RunAsContainer()
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(static c4 =>
		c4.WithLabel("Postgres").WithDescription("A managed Postgres instance for testing")
	);

var redis = builder.AddRedis("redis").WithLikeC4Details(description: "For testing Azure Redis vs. local Redis");
var postgres = builder
	.AddPostgres("postgres")
	.WithLikeC4Details(description: "For testing Azure Postgres vs. local Postgres");

builder
	.AddNodeApp("node-app", "../../../samples/node-app", "index.js")
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(
		label: "Node App",
		technology: "Node.js",
		description: "A sample Node.js application that connects to Azure Redis and Azure Postgres",
		icon: "tech:nodejs"
	)
	.WithPnpm(install: true)
	.WithHttpEndpoint(env: "PORT")
	// These references will be used to generate the connections in the C4 model and also ensure that the application waits for these dependencies to be ready before starting.
	.WithLikeC4Reference(azureManagerRedis, opts => opts.WithLabel("Caches sessions").WithTechnology("Redis Protocol"), withAspireReference: true)
	.WaitFor(azureManagerRedis)
	.WithLikeC4Reference(azurePostgres, opts => opts.WithLabel("Persists data").WithTechnology("PostgreSQL / JDBC"), withAspireReference: true)
	.WaitFor(azurePostgres);

var app = builder.Build();

await app.RunAsync();

// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
