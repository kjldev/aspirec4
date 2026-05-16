import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

// Add LikeC4 visualization to the application. This will allow us to visualize the components and their relationships in a C4 model.
await builder.addAspireC4({
	configure: async (opts) => {
		// Validate the C4 model before starting the application to catch any issues early.
		await opts.validateBeforeStart.set(true);
		await opts.title.set("AspireC4 Test App");
		await opts.viewTitle.set("AspireC4 Architecture");
		await opts.viewDescription.set(
			`
This **LikeC4** view was automatically generated from the **Aspire** resource graph, using the **AspireC4** hosting extension.

For more details on all of these tools and components, see:

- [Aspire](https://aspire.dev/)
- [LikeC4](https://likec4.dev/)
- [AspireC4](https://kjl.dev/projects/aspirec4/)
`
		);
	},
});

// Azure managed resources (containers when local).
const azureManagerRedis = await builder
	.addAzureManagedRedis("azure-redis")
	// Run as container when local
	.runAsContainer({
		configureContainer: async (c) => {
			await c.withRedisCommander({
				configureContainer: async (commanderContainer) => {
					await commanderContainer.withLikeC4Details({
						label: "Redis Commander",
						summary: "Local Redis Web Interface",
					});
				},
			});
		},
	})
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.withLikeC4Details({
		label: "Azure Redis",
		technology: "Azure Redis",
		description: `A **Managed Azure** Redis instance allowing fast access to previously cached data and values.

Used with the **Cache Aside** pattern, where the application can check Redis for cached data before falling back to the primary data store (Postgres in this case).

Cache usage will be non-critical and short-lived, ideal for session caching or caching frequently accessed data that doesn't require strong consistency.

Callers must:

- Assume cache is empty
- Populate with a TTL (Time To Live) to prevent stale data
- Ensure keys follow the pattern: \`{service}:{key}\``,
		summary: "Short term caching, used for cross-instance caching",
		configure: async (node) => {
			await node
				.withLinkNode(
					"https://learn.microsoft.com/azure/azure-cache-for-redis/cache-overview",
					{ title: "Learn more about Azure Redis" }
				)
				.withLinkNode("https://azure.com/", { title: "Learn more about Azure" })
				.withLinkNode("https://redis.io/", { title: "Learn more about Redis" });
		},
	});

const azurePostgres = await builder
	.addAzurePostgresFlexibleServer("azure-postgres")
	// Run as container when local
	.runAsContainer({
		configureContainer: async (c) => {
			await c.withPgWeb({
				configureContainer: async (pgC) => {
					await pgC.withLikeC4Details({
						label: "PgWeb",
						summary: "Local Postgres Web Interface",
					});
				},
			});
		},
	})
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.withLikeC4Details({
		label: "Azure Postgres",
		description: `An **Azure Managed** Postgres instance for testing`,
		summary: "Azure Managed Postgres Flexible Server",
		configure: async (node) => {
			await node
				.withLinkNode(
					"https://learn.microsoft.com/azure/postgresql/flexible-server/overview",
					{ title: "Learn more about Azure Postgres Flexible Server" }
				)
				.withLinkNode("https://www.postgresql.org/", {
					title: "Learn more about Postgres",
				})
				.withLinkNode("https://azure.com/", { title: "Learn more about Azure" })
				.withMetadataNode("Azure SKU", "Flexible Server x 1 (NON-PROD)")
				.withMetadataNode("Azure SKU", "Flexible Server x 2 (PROD)")
				.withMetadataNode("Use Case", "Primary data store");
		},
	});

// Local Dev/ Sync versions...
const localRedis = await builder
	.addRedis("local-redis")
	.withLikeC4Details({
		description: `For testing **locally**, uses Redis as a container.

When using Azure Managed Redis with \`.RunAsContainer()\`, the application will differenciate between that and a real Redis resource using \`.AddRedis(...)\` and pick the correct icon/ technology.`,
		summary: "Local redis for development",
		configure: async (node) => {
			await node
				.withLinkNode("https://redis.io/", { title: "Learn more about Redis" })
				.withTag("local-dev");
		},
	})
	.withLikeC4Group("Local Dev/ Sync Group");

const localPostgres = await builder
	.addPostgres("local-postgres")
	.withLikeC4Details({
		description: `For testing Azure Postgres vs. local Postgres`,
		summary: "Local Postgres for development",
		configure: async (node) => {
			await node
				.withLinkNode("https://www.postgresql.org/", {
					title: "Learn more about Postgres",
				})
				.withTag("local-dev");
		},
	})
	.withLikeC4Group("Local Dev/ Sync Group");

// Our app...
const nodeApp = await builder
	.addNodeApp("node-app", "../node-app/", "index.ts")
	// Add LikeC4 details to the component for better visualization in the C4 model.
	.withLikeC4Details({
		label: "Sample Node App",
		description:
			"A sample Node.js application that connects to Azure Redis and Azure Postgres",
	})
	.withNpm({ install: true })
	.withHttpEndpoint({ env: "PORT" })
	.withUrlForEndpoint("http", async (url) => {
		url.url = "/health";
	})
	// These references will be used to generate the connections in the C4 model and also ensure that the application waits for these dependencies to be ready before starting.
	.withLikeC4Reference(azureManagerRedis, {
		configure: async (opts) => {
			await opts
				.withLabel("Caches sessions")
				.withTechnology("Redis Protocol")
				.withKind("RESP");
		},
	})
	.waitFor(azureManagerRedis)
	.withLikeC4Reference(localRedis, {
		configure: async (opts) => {
			await opts
				.withLabel("Caches  sessions (local)")
				.withTechnology("Redis Protocol")
				.withKind("RESP");
		},
	})
	.waitFor(localRedis)
	.withLikeC4Reference(azurePostgres, {
		configure: async (opts) => {
			await opts
				.withLabel("Persists data")
				.withTechnology("PostgreSQL / JDBC")
				.withKind("tcp-ip");
		},
	})
	.waitFor(azurePostgres)
	.withLikeC4Reference(localPostgres, {
		configure: async (opts) => {
			await opts
				.withLabel("Persists data (local)")
				.withTechnology("PostgreSQL / JDBC")
				.withKind("tcp-ip");
		},
	})
	.waitFor(localPostgres);

await localPostgres.withLikeC4Reference(azurePostgres, {
	configure: async (opts) => {
		await opts
			.withLabel("syncs with")
			.withTechnology("PostgreSQL / JDBC")
			.withKind("tcp-ip");
	},
});
await localRedis.withLikeC4Reference(azureManagerRedis, {
	configure: async (opts) => {
		await opts
			.withLabel("syncs with")
			.withTechnology("Redis Protocol")
			.withKind("RESP");
	},
});

await builder.build().run();
