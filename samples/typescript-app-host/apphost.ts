/*
	Currently the TypeScript bindings/ module generation isn't working
	for the AspireC4 project. Will come back to this once it's resolved.
*/

import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

// Azure resources...
const azureCache = await builder
	.addAzureManagedRedis("azure-redis")
	.runAsContainer();
const azurePostgres = await builder
	.addAzurePostgresFlexibleServer("azure-postgres")
	.runAsContainer();

// Local resources...
const localRedis = await builder.addRedis("local-redis");
const localPostgres = await builder.addPostgres("local-postgres");

// Our app...
const nodeApp = await builder
	.addNodeApp("node-app", "../node-app/", "index.ts")
	.withNpm({
		install: true,
	})
	.withHttpEndpoint({
		env: "PORT",
	})
	.withUrlForEndpoint("http", async (url) => {
		url.url = "/health";
	})
	// Azure resources...
	.withReference(azureCache)
	.withReference(azurePostgres)
	.waitFor(azureCache)
	.waitFor(azurePostgres)
	// Local resources...
	.withReference(localPostgres)
	.withReference(localRedis)
	.waitFor(localPostgres)
	.waitFor(localRedis);

await builder.build().run();
