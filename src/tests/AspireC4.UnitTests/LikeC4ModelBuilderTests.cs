using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4ModelBuilderTests
{
	[Test]
	public async Task Build_WithNoResources_ReturnsEmptyModel()
	{
		var model = LikeC4ModelBuilder.Build([]);

		await Assert.That(model.Elements).IsEmpty();
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ProjectResource_MapsToComponentKind()
	{
		var resource = CreateProjectResource("api");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Component);
		await Assert.That(model.Elements[0].Name).IsEqualTo("api");
	}

	[Test]
	public async Task Build_ContainerResource_MapsToContainerKind()
	{
		var resource = CreateContainerResource("redis");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Container);
	}

	[Test]
	public async Task Build_ExecutableResource_MapsToExecutableKind()
	{
		var resource = new ExecutableResource("worker", "dotnet", ".");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Executable);
	}

	[Test]
	public async Task Build_ResourceWithConnectionString_MapsToDatabaseKind()
	{
		var resource = new TestDatabaseResource("db");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Database);
	}

	[Test]
	public async Task Build_UnknownResource_MapsToSystemKind()
	{
		var resource = new TestSystemResource("ext");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.System);
	}

	[Test]
	public async Task Build_ResourceWithExcludeAnnotation_IsSkipped()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new ExcludeFromLikeC4Annotation());

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements).IsEmpty();
	}

	[Test]
	public async Task Build_HiddenResource_IsSkipped()
	{
		var resource = new TestSystemResource("hidden");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "System",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements).IsEmpty();
	}

	[Test]
	public async Task Build_ResourceWithNodeDetailsAnnotation_UsesAnnotationValues()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API Service", "ASP.NET Core", "Handles HTTP requests", "bootstrap:gear")
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var element = model.Elements[0];
		await Assert.That(element.Label).IsEqualTo("API Service");
		await Assert.That(element.Technology).IsEqualTo("ASP.NET Core");
		await Assert.That(element.Description).IsEqualTo("Handles HTTP requests");
		await Assert.That(element.Icon).IsEqualTo("bootstrap:gear");
	}

	[Test]
	public async Task Build_ResourceWithNoNodeDetailsAnnotation_UsesResourceNameAsLabel()
	{
		var resource = CreateProjectResource("my-api");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Label).IsEqualTo("my-api");
	}

	[Test]
	public async Task Build_ProjectResource_InfersDotnetIcon()
	{
		var resource = CreateProjectResource("api");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_AzureResource_InfersBundledAzureIcon()
	{
		var resource = new TestSystemResource("redis");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Azure.Redis", Properties = [] })
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_AzureSurrogateContainer_InfersAzureIconFromName()
	{
		// RunAsContainer() produces a ContainerResource named after the Azure resource (e.g. "azure-redis")
		var resource = CreateContainerResource("azure-redis");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_PlainRedisContainer_UsesGenericTechIcon()
	{
		var resource = CreateContainerResource("redis");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_PostgresResource_UsesGenericTechIcon()
	{
		var resource = CreateContainerResource("postgres");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("Postgres", "Azure Postgres", "Managed PostgreSQL"));

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:postgresql");
	}

	[Test]
	public async Task Build_WithProjectAutoIconsDisabled_DoesNotInferIcon()
	{
		var resource = CreateProjectResource("api");

		var model = LikeC4ModelBuilder.Build([resource], autoIconsEnabled: false);

		await Assert.That(model.Elements[0].Icon).IsNull();
	}

	[Test]
	public async Task Build_WithPerResourceAutoIconDisabled_DoesNotInferIcon()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API", ".NET", "HTTP API", icon: null, autoIconEnabled: false)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsNull();
	}

	[Test]
	public async Task Build_WithPerResourceAutoIconEnabled_OverridesProjectSetting()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API", ".NET", "HTTP API", icon: null, autoIconEnabled: true)
		);

		var model = LikeC4ModelBuilder.Build([resource], autoIconsEnabled: false);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_ExplicitIcon_IsUsedWhenAutoIconsDisabled()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API", ".NET", "HTTP API", "bootstrap:gear", autoIconEnabled: false)
		);

		var model = LikeC4ModelBuilder.Build([resource], autoIconsEnabled: false);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("bootstrap:gear");
	}

	[Test]
	public async Task Build_ResourceRelationshipAnnotation_CreatesRelationship()
	{
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		var rel = model.Relationships[0];
		await Assert.That(rel.SourceName).IsEqualTo("api");
		await Assert.That(rel.TargetName).IsEqualTo("db");
	}

	[Test]
	public async Task Build_WaitForRelationship_IsSkipped()
	{
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "WaitFor"));

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_DuplicateRelationship_IsDeduped()
	{
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Build_RelationshipToExcludedTarget_IsSkipped()
	{
		var api = CreateProjectResource("api");
		var hidden = new TestSystemResource("infra");
		hidden.Annotations.Add(new ExcludeFromLikeC4Annotation());
		api.Annotations.Add(new ResourceRelationshipAnnotation(hidden, "Reference"));

		var model = LikeC4ModelBuilder.Build([api, hidden]);

		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ResourceWithParent_SetsParentName()
	{
		var parent = CreateContainerResource("postgres");
		var child = new TestChildResource("postgres-db", parent);

		var model = LikeC4ModelBuilder.Build([parent, child]);

		var childElement = model.Elements.Single(e => e.Name == "postgres-db");
		await Assert.That(childElement.ParentName).IsEqualTo("postgres");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_OverridesLabel()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("db", label: "Reads from", technology: null, description: null)
		);

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships[0].Label).IsEqualTo("Reads from");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsTechnologyAndDescription()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation(
				"db",
				label: null,
				technology: "PostgreSQL",
				description: "Stores user records"
			)
		);

		var model = LikeC4ModelBuilder.Build([api, db]);

		var rel = model.Relationships[0];
		await Assert.That(rel.Technology).IsEqualTo("PostgreSQL");
		await Assert.That(rel.Description).IsEqualTo("Stores user records");
		await Assert.That(rel.Label).IsNull();
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_MatchesSurrogateName()
	{
		// The WithLikeC4Reference target name is the hidden Azure resource name;
		// the effective target is the surrogate container — both share the same name "redis".
		var hiddenAzureRedis = new TestSystemResource("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var visibleContainerRedis = CreateContainerResource("redis");

		var nodeApp = new ExecutableResource("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "Reference"));
		nodeApp.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation(
				"redis",
				label: "Caches sessions",
				technology: "Redis Protocol",
				description: null
			)
		);

		var model = LikeC4ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		var rel = model.Relationships[0];
		await Assert.That(rel.Label).IsEqualTo("Caches sessions");
		await Assert.That(rel.Technology).IsEqualTo("Redis Protocol");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_LastAnnotationWins()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("db", label: "First", technology: null, description: null)
		);
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("db", label: "Last", technology: null, description: null)
		);

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships[0].Label).IsEqualTo("Last");
	}

	[Test]
	public async Task Build_NonReferenceRelationshipType_SetsLabel()
	{
		var api = CreateProjectResource("api");
		var queue = new TestSystemResource("queue");
		api.Annotations.Add(new ResourceRelationshipAnnotation(queue, "Publishes"));

		var model = LikeC4ModelBuilder.Build([api, queue]);

		await Assert.That(model.Relationships[0].Label).IsEqualTo("Publishes");
	}

	[Test]
	public async Task Build_ReferenceToHiddenResourceWithVisibleSurrogate_ResolvesViaSurrogateName()
	{
		// Simulates AddAzureManagedRedis("redis").RunAsContainer():
		// - The original Azure resource is hidden (IsHidden = true)
		// - A container surrogate with the same name "redis" is visible
		// - The node-app's WithReference points to the hidden Azure resource
		var hiddenAzureRedis = new TestSystemResource("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var visibleContainerRedis = CreateContainerResource("redis");

		var nodeApp = new ExecutableResource("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "Reference"));

		var model = LikeC4ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].SourceName).IsEqualTo("node-app");
		await Assert.That(model.Relationships[0].TargetName).IsEqualTo("redis");
	}

	[Test]
	public async Task Build_WaitForToHiddenSurrogate_IsSkipped()
	{
		// WaitFor relationships to a hidden Azure resource should still be skipped,
		// not accidentally resolved and included via the surrogate.
		var hiddenAzureRedis = new TestSystemResource("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);
		var visibleContainerRedis = CreateContainerResource("redis");

		var nodeApp = new ExecutableResource("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "WaitFor"));

		var model = LikeC4ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ReferenceToHiddenResourceWithNoSurrogate_IsSkipped()
	{
		// If the Azure resource is hidden and there is no visible surrogate with the same
		// name, the relationship should be dropped entirely (not produce a broken edge).
		var hiddenResource = new TestSystemResource("orphaned-azure-resource");
		hiddenResource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Something",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var nodeApp = new ExecutableResource("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenResource, "Reference"));

		var model = LikeC4ModelBuilder.Build([hiddenResource, nodeApp]);

		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_MultipleReferencesToSurrogateSameName_DeduplicatesRelationship()
	{
		// Both a WaitFor (hidden) and a Reference (hidden) to the same Azure resource should
		// produce exactly one relationship to the surrogate (WaitFor is filtered; Reference resolves).
		var hiddenAzureRedis = new TestSystemResource("redis");
		hiddenAzureRedis.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Azure.Redis",
					Properties = [],
					IsHidden = true,
				}
			)
		);
		var visibleContainerRedis = CreateContainerResource("redis");

		var nodeApp = new ExecutableResource("node-app", "node", ".");
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "Reference"));
		nodeApp.Annotations.Add(new ResourceRelationshipAnnotation(hiddenAzureRedis, "WaitFor"));

		var model = LikeC4ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].SourceName).IsEqualTo("node-app");
		await Assert.That(model.Relationships[0].TargetName).IsEqualTo("redis");
	}

	[Test]
	public async Task Build_WithResourceStates_ElementsReflectStates()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		var states = new Dictionary<string, LikeC4ResourceState>(StringComparer.OrdinalIgnoreCase)
		{
			["api"] = LikeC4ResourceState.Running,
			["db"] = LikeC4ResourceState.Error,
		};

		var model = LikeC4ModelBuilder.Build([api, db], states);

		var apiElement = model.Elements.Single(e => e.Name == "api");
		var dbElement = model.Elements.Single(e => e.Name == "db");
		await Assert.That(apiElement.State).IsEqualTo(LikeC4ResourceState.Running);
		await Assert.That(dbElement.State).IsEqualTo(LikeC4ResourceState.Error);
	}

	[Test]
	public async Task Build_WithNoResourceStates_ElementsDefaultToUnknown()
	{
		var api = CreateProjectResource("api");

		var model = LikeC4ModelBuilder.Build([api]);

		await Assert.That(model.Elements[0].State).IsEqualTo(LikeC4ResourceState.Unknown);
	}

	[Test]
	public async Task Build_WithPartialResourceStates_UnknownForMissingEntries()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		var states = new Dictionary<string, LikeC4ResourceState>(StringComparer.OrdinalIgnoreCase)
		{
			["api"] = LikeC4ResourceState.Starting,
		};

		var model = LikeC4ModelBuilder.Build([api, db], states);

		var dbElement = model.Elements.Single(e => e.Name == "db");
		await Assert.That(dbElement.State).IsEqualTo(LikeC4ResourceState.Unknown);
	}

	[Test]
	public async Task GetVisibleResourceNames_ExcludesHiddenAndAnnotatedResources()
	{
		var visible = CreateProjectResource("api");

		var hidden = new TestSystemResource("infra");
		hidden.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "Internal",
					Properties = [],
					IsHidden = true,
				}
			)
		);

		var excluded = CreateContainerResource("excluded");
		excluded.Annotations.Add(new ExcludeFromLikeC4Annotation());

		var names = LikeC4ModelBuilder.GetVisibleResourceNames([visible, hidden, excluded]);

		await Assert.That(names).Contains("api");
		await Assert.That(names).DoesNotContain("infra");
		await Assert.That(names).DoesNotContain("excluded");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_IsEmittedWithoutResourceRelationshipAnnotation()
	{
		// Simulates postgres.WithLikeC4Reference(azurePostgres, opts => ...) where no
		// WithReference was called — a purely diagram-level relationship.
		var local = CreateContainerResource("postgres");
		var azure = CreateContainerResource("azure-postgres");
		local.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation(
				"azure-postgres",
				label: "syncs with",
				technology: "PostgreSQL / JDBC",
				description: null
			)
		);

		var model = LikeC4ModelBuilder.Build([local, azure]);

		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		var rel = model.Relationships[0];
		await Assert.That(rel.SourceName).IsEqualTo("postgres");
		await Assert.That(rel.TargetName).IsEqualTo("azure-postgres");
		await Assert.That(rel.Label).IsEqualTo("syncs with");
		await Assert.That(rel.Technology).IsEqualTo("PostgreSQL / JDBC");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_IsDeduplicatedWhenResourceRelationshipAlsoPresent()
	{
		// If WithReference AND WithLikeC4Reference are both called, only one edge should appear.
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("db", label: "Queries", technology: "SQL", description: null)
		);

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Queries");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsKind()
	{
		var api = CreateProjectResource("api");
		var queue = new TestSystemResource("queue");
		api.Annotations.Add(new ResourceRelationshipAnnotation(queue, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation(
				"queue",
				label: null,
				technology: null,
				description: null,
				kind: "async"
			)
		);

		var model = LikeC4ModelBuilder.Build([api, queue]);

		await Assert.That(model.Relationships[0].Kind).IsEqualTo("async");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_PropagatesKind()
	{
		var api = CreateProjectResource("api");
		var queue = new TestSystemResource("queue");
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation(
				"queue",
				label: "Publishes",
				technology: "AMQP",
				description: null,
				kind: "async"
			)
		);

		var model = LikeC4ModelBuilder.Build([api, queue]);

		await Assert.That(model.Relationships[0].Kind).IsEqualTo("async");
	}

	[Test]
	public async Task Build_RelationshipWithNoKind_KindIsNull()
	{
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships[0].Kind).IsNull();
	}

	static ProjectResource CreateProjectResource(string name)
	{
		var resource = new ProjectResource(name);
		return resource;
	}

	static ContainerResource CreateContainerResource(string name)
	{
		var resource = new ContainerResource(name);
		return resource;
	}

	sealed class TestDatabaseResource(string name) : Resource(name), IResourceWithConnectionString
	{
		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"host=localhost");
	}

	sealed class TestSystemResource(string name) : Resource(name);

	sealed class TestChildResource(string name, IResource parent) : Resource(name), IResourceWithParent
	{
		public IResource Parent { get; } = parent;
	}
}
