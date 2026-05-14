namespace Aspire.Hosting.AspireC4.LikeC4;

/// <summary>
/// Data-driven icon inference tests. Each <see cref="IconTestScenario"/> declares the resource
/// configuration (control) and the expected LikeC4 icon string (desired state). To add coverage
/// for a new resource, add a single entry to <see cref="Scenarios"/>.
/// </summary>
public sealed partial class IconInferenceTests
{
	/// <summary>
	/// The canonical set of icon inference scenarios. Add new entries here to extend coverage.
	/// </summary>
	public static IEnumerable<IconTestScenario> Scenarios() =>
		[
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
			new(() => (CreateSnapshotResource("servicebus", "Azure.ServiceBus"), null), "azure:azure-service-bus"),
			new(() => (CreateSnapshotResource("cosmos", "Azure.CosmosDb"), null), "azure:azure-cosmos-db"),
			new(() => (CreateSnapshotResource("keyvault", "Azure.KeyVault"), null), "azure:key-vaults"),
			new(() => (CreateSnapshotResource("eventhubs", "Azure.EventHubs"), null), "azure:event-hubs"),
			new(() => (CreateSnapshotResource("storage", "Azure.StorageAccount"), null), "azure:storage-accounts"),
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
		var model = ModelBuilder.Build(resources);

		// Assert
		var element = model.Elements.Single(e => e.Name == visible.Name);
		await Assert.That(element.Icon).IsEqualTo(scenario.ExpectedIcon);
	}
}
