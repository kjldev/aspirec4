using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Data-driven icon inference tests. Each <see cref="IconTestScenario"/> declares the resource
/// configuration (control) and the expected LikeC4 icon string (desired state). To add coverage
/// for a new resource, add a single entry to <see cref="Scenarios"/>.
/// </summary>
public sealed partial class LikeC4IconInferenceTests
{
	/// <summary>
	/// The canonical set of icon inference scenarios. Add new entries here to extend coverage.
	/// </summary>
	public static IEnumerable<IconTestScenario> Scenarios() =>
		[
			// ── Azure Redis ──────────────────────────────────────────────────────────────────────

			new(() => (CreateSnapshotResource("redis", "Azure.ManagedRedis"), null), "azure:azure-managed-redis"),
			new(
				() =>
					(
						CreateContainerResource("azure-redis"),
						AddHiddenSnapshot<AzureManagedRedisResource>(new("azure-redis"), "AzureManagedRedis")
					),
				"azure:azure-managed-redis"
			),
			new(
				() =>
					(
						CreateContainerResource("azure-cache"),
						AddHiddenSnapshot<AzureRedisCacheResource>(new("azure-cache"), "AzureRedisCache")
					),
				"azure:cache-redis"
			),
			// ── Azure SQL ────────────────────────────────────────────────────────────────────────
			// Note: query tokens ["sql", "server"] score equally against both "sql-server" and
			// "arc-sql-server" icons. "arc-sql-server" appears first in the manifest, so it wins
			// the tie. To pin the more specific icon in a real app, use .WithLikeC4Details(icon:).

			new(() => (CreateSnapshotResource("sqlserver", "Azure.SqlServer"), null), "azure:arc-sql-server"),
			new(
				() =>
					(
						CreateContainerResource("azure-sql"),
						AddHiddenSnapshot<AzureSqlServerResource>(new("azure-sql"), "AzureSqlServer")
					),
				"azure:arc-sql-server"
			),
			// ── Azure Postgres ───────────────────────────────────────────────────────────────────

			new(
				() => (CreateSnapshotResource("postgres", "Azure.PostgresFlexibleServer"), null),
				"azure:azure-database-postgre-sql-server"
			),
			new(
				() =>
					(
						CreateContainerResource("azure-postgres"),
						AddHiddenSnapshot<AzurePostgresFlexibleServerResource>(
							new("azure-postgres"),
							"AzurePostgresFlexibleServer"
						)
					),
				"azure:azure-database-postgre-sql-server"
			),
			// ── Azure Service Bus ────────────────────────────────────────────────────────────────

			new(() => (CreateSnapshotResource("servicebus", "Azure.ServiceBus"), null), "azure:azure-service-bus"),
			// ── Azure Cosmos DB ──────────────────────────────────────────────────────────────────

			new(() => (CreateSnapshotResource("cosmos", "Azure.CosmosDb"), null), "azure:azure-cosmos-db"),
			// ── Azure Key Vault ──────────────────────────────────────────────────────────────────

			new(() => (CreateSnapshotResource("keyvault", "Azure.KeyVault"), null), "azure:key-vaults"),
			// ── Azure Event Hubs ─────────────────────────────────────────────────────────────────

			new(() => (CreateSnapshotResource("eventhubs", "Azure.EventHubs"), null), "azure:event-hubs"),
			// ── Azure Storage ────────────────────────────────────────────────────────────────────

			new(() => (CreateSnapshotResource("storage", "Azure.StorageAccount"), null), "azure:storage-accounts"),
			// ── Tech: generic containers ─────────────────────────────────────────────────────────

			new(() => (CreateContainerResource("redis"), null), "tech:redis"),
			new(() => (CreateContainerResource("postgres"), null), "tech:postgresql"),
			new(() => (CreateContainerResource("rabbitmq"), null), "tech:rabbitmq"),
			new(() => (CreateContainerResource("mongodb"), null), "tech:mongodb"),
			new(() => (CreateContainerResource("mysql"), null), "tech:mysql"),
		];

	[Test]
	[MethodDataSource(nameof(Scenarios))]
	[DisplayName("Inferring LikeC4 icon from Aspire resource: ${scenario.Name}")]
	public async Task Build_WithConfiguredResource_InfersExpectedIcon(IconTestScenario scenario)
	{
		// Arrange
		var (visible, hidden) = scenario.CreateResources();
		IReadOnlyList<IResource> resources = hidden is null ? [visible] : [visible, hidden];

		// Act
		var model = LikeC4ModelBuilder.Build(resources);

		// Assert
		var element = model.Elements.Single(e => e.Name == visible.Name);
		await Assert.That(element.Icon).IsEqualTo(scenario.ExpectedIcon);
	}
}
