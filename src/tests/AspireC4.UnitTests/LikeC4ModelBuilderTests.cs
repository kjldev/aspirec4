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
	public async Task Build_PostgresResource_WithAzureTechnology_InfersAzureIcon()
	{
		// When the user labels the technology "Azure Postgres", the cloud phase detects "azure"
		// and infers an azure postgres icon instead of the generic tech:postgresql.
		var resource = CreateContainerResource("postgres");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("Postgres", "Azure Postgres", "Managed PostgreSQL"));

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon!).StartsWith("azure:");
		await Assert.That(model.Elements[0].Icon!).Contains("postgre");
	}

	[Test]
	public async Task Build_ContainerWithLibraryRedisImage_InfersRedisIcon()
	{
		// Regression: "library/redis" was matching "heroku-redis" due to Jaccard tie-breaking.
		// "library" is a stop token, leaving query ["redis"] which should score "redis" at 1.0
		// and "heroku-redis" at 0.5 (unmatched "heroku" token penalty).
		var resource = CreateContainerResource("myredis");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "library/redis" });

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_ContainerWithLibraryPostgresImage_InfersPostgresqlIcon()
	{
		// Regression: "library/postgres" was matching "testing-library" because "library"
		// dominated the score. Stop tokens now strip "library", leaving query ["postgres"]
		// which prefix-matches "postgresql" at 0.64 and non-prefix-matches "postgraphile" at 0.40.
		var resource = CreateContainerResource("mypostgres");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "library/postgres" });

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:postgresql");
	}

	[Test]
	public async Task Build_NodeAppInstallerExecutable_InfersNodejsIcon()
	{
		// Regression: "node-app-installer" → queryTokens were ["node", "installer"] because
		// "installer" was not a stop token. The 2-token query diluted the score: 0.533 / 2 = 0.267
		// (below MinScore 0.35). Adding "installer" to QueryStopTokens leaves ["node"] → 0.533 ✓.
		var resource = new ExecutableResource("node-app-installer", "node", ".");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	[Test]
	public async Task Build_AzurePostgresRunAsContainer_InfersAzurePostgresIcon()
	{
		// Regression: "azure-postgres" container (from RunAsContainer()) was matching tech:postgresql
		// instead of an azure icon. The hidden Azure resource's type name provides additional query
		// tokens ("flexible", "server") that allow the matcher to score the correct azure icon.
		var visibleContainer = CreateContainerResource("azure-postgres");
		visibleContainer.Annotations.Add(new ContainerImageAnnotation { Image = "library/postgres" });

		var hiddenAzureResource = new AzurePostgresFlexibleServerResource("azure-postgres");
		hiddenAzureResource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "AzurePostgresFlexibleServer",
					IsHidden = true,
					Properties = [],
				}
			)
		);

		var model = LikeC4ModelBuilder.Build([visibleContainer, hiddenAzureResource]);

		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-database-postgre-sql-server");
	}

	[Test]
	public async Task Build_AzureManagedRedisRunAsContainer_InfersAzureManagedRedisIcon()
	{
		// Same pattern as Azure Postgres: visible ContainerResource backed by a hidden Azure resource.
		// The hidden resource's type name ("AzureManagedRedisResource") contributes "managed" and "redis"
		// as query tokens, scoring a perfect match against "azure-managed-redis".
		var visibleContainer = CreateContainerResource("azure-redis");
		visibleContainer.Annotations.Add(new ContainerImageAnnotation { Image = "library/redis" });

		var hiddenAzureResource = new AzureManagedRedisResource("azure-redis");
		hiddenAzureResource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot
				{
					ResourceType = "AzureManagedRedis",
					IsHidden = true,
					Properties = [],
				}
			)
		);

		var model = LikeC4ModelBuilder.Build([visibleContainer, hiddenAzureResource]);

		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_NodeAppExecutable_InfersNodejsIcon()
	{
		// Regression: "node-app" was matching "node-sass" because both "node" and "sass" (unmatched)
		// gave a lower score than the corrected unmatched-token penalty.
		// After stop tokens strip "app", query is ["node"]; "nodejs" wins at 0.533 over "node-sass" at 0.5.
		var resource = new ExecutableResource("node-app", "node", ".");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
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
	public async Task Build_WithCustomIconResolver_OverridesAutoInference()
	{
		var resource = CreateProjectResource("api");

		LikeC4IconResolver resolver = ctx => ctx.Resource is ProjectResource ? "tech:dotnet" : null;

		var model = LikeC4ModelBuilder.Build([resource], iconResolvers: [resolver]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_NullResultFallsBackToAutoInference()
	{
		var resource = CreateContainerResource("redis");

		// Resolver explicitly declines; auto-inference should still pick tech:redis.
		LikeC4IconResolver resolver = _ => null;

		var model = LikeC4ModelBuilder.Build([resource], iconResolvers: [resolver]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_ContextExposesHiddenOriginal()
	{
		IResource? capturedHidden = null;

		LikeC4IconResolver resolver = ctx =>
		{
			capturedHidden = ctx.HiddenOriginal;
			return null;
		};

		var visibleContainer = CreateContainerResource("azure-redis");
		var hiddenAzureResource = new AzureManagedRedisResource("azure-redis");
		var snapshot = new ResourceSnapshotAnnotation(
			new CustomResourceSnapshot
			{
				ResourceType = "AzureManagedRedisResource",
				Properties = [],
				IsHidden = true,
			}
		);
		hiddenAzureResource.Annotations.Add(snapshot);

		LikeC4ModelBuilder.Build([hiddenAzureResource, visibleContainer], iconResolvers: [resolver]);

		await Assert.That(capturedHidden).IsTypeOf<AzureManagedRedisResource>();
	}

	[Test]
	public async Task Build_WithMultipleCustomIconResolvers_FirstNonNullWins()
	{
		var resource = CreateProjectResource("api");
		var callOrder = new List<int>();

		LikeC4IconResolver first = _ =>
		{
			callOrder.Add(1);
			return null;
		};
		LikeC4IconResolver second = _ =>
		{
			callOrder.Add(2);
			return "tech:custom";
		};
		LikeC4IconResolver third = _ =>
		{
			callOrder.Add(3);
			return "tech:should-not-reach";
		};

		var model = LikeC4ModelBuilder.Build([resource], iconResolvers: [first, second, third]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:custom");
		await Assert.That(callOrder).IsEquivalentTo([1, 2]);
	}

	[Test]
	public async Task Build_NodeAnnotation_WithHashPrefixedTag_NormalizesTagInModel()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: ["#external"],
				links: [],
				metadata: []
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Tags).Contains("external");
		await Assert.That(model.Elements[0].Tags).DoesNotContain("#external");
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
	public async Task Build_RelationshipDetailsAnnotation_SetsNavigateTo()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation(
				"db",
				label: null,
				technology: null,
				description: null,
				navigateTo: "db-detail-flow"
			)
		);

		var model = LikeC4ModelBuilder.Build([api, db]);

		await Assert.That(model.Relationships[0].NavigateTo).IsEqualTo("db-detail-flow");
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

	[Test]
	public async Task Build_AwsLambdaResource_InfersAwsIcon()
	{
		var resource = new TestSystemResource("fn");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "AWS.Lambda", Properties = [] })
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("aws:lambda");
	}

	[Test]
	public async Task Build_GcpPubSubResource_InfersGcpIcon()
	{
		var resource = new TestSystemResource("queue");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "GCP.PubSub", Properties = [] })
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("gcp:pub-sub");
	}

	[Test]
	public async Task Build_RabbitMqContainer_InfersTechIcon()
	{
		var resource = CreateContainerResource("rabbitmq");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:rabbitmq");
	}

	[Test]
	public async Task Build_MongoDbContainer_InfersTechIcon()
	{
		var resource = CreateContainerResource("mongodb");

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:mongodb");
	}

	// ── AutoIncludeAspireMetadata tests ───────────────────────────────────────

	[Test]
	public async Task Build_AutoMetadata_Default_InjectsAspireName()
	{
		var resource = CreateProjectResource("my-api");

		var model = LikeC4ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Any(m => m.Key == "aspire-name" && m.Value == "my-api")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_Default_InjectsAspireType()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Project", Properties = [] })
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Any(m => m.Key == "aspire-type" && m.Value == "Project")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_None_DoesNotInjectMetadata()
	{
		var resource = CreateProjectResource("api");

		var model = LikeC4ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.None);

		await Assert.That(model.Elements[0].Metadata).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_MetadataOnly_NoAutoLinks()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http")
		);

		var model = LikeC4ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.Metadata);

		await Assert.That(model.Elements[0].Links).IsEmpty();
		await Assert.That(model.Elements[0].Metadata.Any(m => m.Key == "aspire-name")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_LinksOnly_NoAspireNameMetadata()
	{
		var resource = CreateProjectResource("api");

		var model = LikeC4ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.Links);

		await Assert.That(model.Elements[0].Metadata.Any(m => m.Key == "aspire-name")).IsFalse();
	}

	[Test]
	public async Task Build_AutoMetadata_AllocatedHttpEndpoint_InjectsLink()
	{
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);

		var model = LikeC4ModelBuilder.Build([resource]);

		var links = model.Elements[0].Links;
		var injected = links.FirstOrDefault(l => l.Uri == "http://localhost:5000");
		await Assert.That(injected).IsNotNull();
		await Assert.That(injected!.Title).IsEqualTo("Endpoint: http");
	}

	[Test]
	public async Task Build_AutoMetadata_UnallocatedEndpoint_DoesNotInjectLink()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http")
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Links).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_NonHttpEndpoint_DoesNotInjectLink()
	{
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "grpc", name: "grpc");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5001);
		resource.Annotations.Add(endpoint);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements[0].Links).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_UserMetadataPreservedAndNotDuplicated()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new LikeC4Metadata("aspire-name", "custom-override")]
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var aspireNameEntries = model.Elements[0].Metadata.Where(m => m.Key == "aspire-name").ToList();
		// User-provided entry present, auto-generated one should not duplicate it
		await Assert.That(aspireNameEntries).Count().IsEqualTo(1);
		await Assert.That(aspireNameEntries[0].Value).IsEqualTo("custom-override");
	}

	[Test]
	public async Task Build_AutoMetadata_UserLinksPreservedAndEndpointNotDuplicated()
	{
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [new LikeC4Link("http://localhost:5000", "My Service")],
				metadata: []
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var httpLinks = model.Elements[0].Links.Where(l => l.Uri == "http://localhost:5000").ToList();
		// Should appear only once — auto-injected duplicate is suppressed
		await Assert.That(httpLinks).Count().IsEqualTo(1);
		await Assert.That(httpLinks[0].Title).IsEqualTo("My Service");
	}

	// ── NormaliseMetadataBehaviour tests ──────────────────────────────────────

	[Test]
	public async Task Build_NormaliseMetadata_Default_ReplacesSpaceWithUnderscore()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new("Azure SKU", "Standard")]
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Any(m => m.Key == "Azure_SKU" && m.Value == "Standard")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_DuplicateKeysAreTurnedIntoArrays()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new("Azure SKU", "Entry 1"), new("Azure SKU", "Entry 2")]
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		await Assert.That(model.Elements.Count).IsEqualTo(1);

		var meta = model.Elements[0].Metadata;
		// Both keys normalise to "Azure_SKU"; the builder deduplicates keeping the first value.
		await Assert.That(meta.Where(m => m.Key == "Azure_SKU").Count()).IsEqualTo(1);
		await Assert.That(meta.Any(m => m.Key == "Azure_SKU" && m.Value == "Entry 1")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Default_PreservesValidChars()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new LikeC4Metadata("valid-key_123", "value")]
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Any(m => m.Key == "valid-key_123")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseLowercase_LowercasesKey()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new LikeC4Metadata("Azure SKU", "Standard")]
			)
		);

		var model = LikeC4ModelBuilder.Build(
			[resource],
			normaliseMetadataBehaviour: NormaliseMetadataBehaviour.NormaliseLowercase
		);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Any(m => m.Key == "azure_sku" && m.Value == "Standard")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Throw_ThrowsOnInvalidKey()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new LikeC4Metadata("Azure SKU", "Standard")]
			)
		);

		await Assert
			.That(() =>
				LikeC4ModelBuilder.Build([resource], normaliseMetadataBehaviour: NormaliseMetadataBehaviour.Throw)
			)
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Throw_AcceptsValidKey()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new LikeC4Metadata("valid-key_123", "value")]
			)
		);

		var model = LikeC4ModelBuilder.Build([resource], normaliseMetadataBehaviour: NormaliseMetadataBehaviour.Throw);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Any(m => m.Key == "valid-key_123")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Normalise_ThrowsOnNullKey()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new LikeC4Metadata(null!, "value")]
			)
		);

		await Assert.That(() => LikeC4ModelBuilder.Build([resource])).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Default_ReplacesMultipleInvalidChars()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				metadata: [new LikeC4Metadata("My Key (v2)!", "value")]
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Any(m => m.Key == "My_Key__v2__")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_DuplicateNormalisedKeys_KeepsFirst()
	{
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation(
				"API",
				technology: null,
				description: null,
				summary: null,
				icon: null,
				autoIconEnabled: null,
				kind: null,
				tags: [],
				links: [],
				// "Azure SKU" and "Azure_SKU" both normalise to "Azure_SKU"
				metadata: [new("Azure SKU", "first"), new("Azure_SKU", "second")]
			)
		);

		var model = LikeC4ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		var normalised = meta.Where(m => m.Key == "Azure_SKU").ToList();
		await Assert.That(normalised).Count().IsEqualTo(1);
		await Assert.That(normalised[0].Value).IsEqualTo("first");
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

	// Named to match real Aspire Azure resource class names so the icon matcher
	// receives the correct type tokens via the hidden-original lookup.
	sealed class AzurePostgresFlexibleServerResource(string name) : Resource(name);

	sealed class AzureManagedRedisResource(string name) : Resource(name);
}
