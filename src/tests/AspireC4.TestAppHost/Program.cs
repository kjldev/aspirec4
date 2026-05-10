var builder = DistributedApplication.CreateBuilder(args);

// Add LikeC4 visualization to the application. This will allow us to visualize the components and their relationships in a C4 model.
builder.AddAspireC4(configure: opts =>
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
	.WithLikeC4Details(opts =>
		opts.WithLabel("Redis")
			.WithTechnology("Azure Redis")
			.WithDescription(
				@"A **Managed Azure** Redis instance allowing fast access to previously cached data and values.

Used with the **Cache Aside** pattern, where the application can check Redis for cached data before falling back to the primary data store (Postgres in this case).

Cache usage will be non-critical and short-lived, ideal for session caching or caching frequently accessed data that doesn't require strong consistency.

Callers must:

- Assume cache is empty
- Populate with a TTL (Time To Live) to prevent stale data
- Ensure keys follow the pattern: `{service}:{key}`
"
			)
			.WithSummary("Short term caching, used for cross-instance caching")
			.WithLink(
				"https://learn.microsoft.com/azure/azure-cache-for-redis/cache-overview",
				"Learn more about Azure Redis"
			)
	);

var azurePostgres = builder
	.AddAzurePostgresFlexibleServer("azure-postgres")
	// Run as container when local
	.RunAsContainer()
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.WithLikeC4Details(static c4 =>
		c4.WithLabel("Postgres")
			.WithSummary("Azure Managed Postgres Flexible Server")
			.WithDescription("An **Azure Managed** Postgres instance for testing")
			.WithLink(
				"https://learn.microsoft.com/azure/postgresql/flexible-server/overview",
				"Learn more about Azure Postgres Flexible Server"
			)
			.WithMetadata("Azure SKU", "Flexible Server")
			.WithMetadata("Use Case", "Primary data store")
	);

var redis = builder
	.AddRedis("redis")
	.WithLikeC4Details(
		description: "For testing **locally**, uses Redis as a container",
		summary: "Local redis for development"
	)
	.WithLikeC4Group("Local Dev/ Sync Group");
var postgres = builder
	.AddPostgres("postgres")
	.WithLikeC4Details(
		description: "For testing Azure Postgres vs. local Postgres",
		summary: "Local Postgres for development"
	)
	.WithLikeC4Group("Local Dev/ Sync Group");

var nodeApp = builder
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
	.WithLikeC4Reference(
		azureManagerRedis,
		opts => opts.WithLabel("Caches sessions").WithTechnology("Redis Protocol").WithKind("RESP")
	)
	.WaitFor(azureManagerRedis)
	.WithLikeC4Reference(
		redis,
		opts => opts.WithLabel("Caches  sessions (local)").WithTechnology("Redis Protocol").WithKind("RESP")
	)
	.WaitFor(redis)
	.WithLikeC4Reference(
		azurePostgres,
		opts => opts.WithLabel("Persists data").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
	)
	.WaitFor(azurePostgres)
	.WithLikeC4Reference(
		postgres,
		opts => opts.WithLabel("Persists data (local)").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
	)
	.WaitFor(postgres);

postgres.WithLikeC4Reference(
	azurePostgres,
	opts => opts.WithLabel("syncs with").WithTechnology("PostgreSQL / JDBC").WithKind("tcp-ip")
);
redis.WithLikeC4Reference(
	azureManagerRedis,
	opts => opts.WithLabel("syncs with").WithTechnology("Redis Protocol").WithKind("RESP")
);

var app = builder.Build();

await app.RunAsync();

// Marker class so integration tests can reference this assembly.
sealed partial class TestAppHostProgram { }
