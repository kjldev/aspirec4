using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	[Test]
	public async Task Build_WithNoResources_ReturnsEmptyModel()
	{
		// Arrange
		// Act
		var model = ModelBuilder.Build([]);

		// Assert
		await Assert.That(model.Elements).IsEmpty();
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ProjectResource_MapsToComponentKind()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Component);
		await Assert.That(model.Elements[0].Name).IsEqualTo("api");
	}

	[Test]
	public async Task Build_ContainerResource_MapsToContainerKind()
	{
		// Arrange
		var resource = CreateContainerResource("redis");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Container);
	}

	[Test]
	public async Task Build_ExecutableResource_MapsToExecutableKind()
	{
		// Arrange
		var resource = new ExecutableResource("worker", "dotnet", ".");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Executable);
	}

	[Test]
	public async Task Build_ResourceWithConnectionString_MapsToDatabaseKind()
	{
		// Arrange
		var resource = new TestDatabaseResource("db");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.Database);
	}

	[Test]
	public async Task Build_UnknownResource_MapsToSystemKind()
	{
		// Arrange
		var resource = new TestSystemResource("ext");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Kind).IsEqualTo(LikeC4ElementKind.System);
	}

	[Test]
	public async Task Build_ResourceWithExcludeAnnotation_IsSkipped()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new ExcludeFromLikeC4Annotation());

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements).IsEmpty();
	}

	[Test]
	public async Task Build_HiddenResource_IsSkipped()
	{
		// Arrange
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

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements).IsEmpty();
	}

	[Test]
	public async Task Build_ResourceWithNodeDetailsAnnotation_UsesAnnotationValues()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API Service")
				.WithTechnology("ASP.NET Core")
				.WithDescription("Handles HTTP requests")
				.WithIcon("bootstrap:gear")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Label).IsEqualTo("API Service");
		await Assert.That(element.Technology).IsEqualTo("ASP.NET Core");
		await Assert.That(element.Description).IsEqualTo("Handles HTTP requests");
		await Assert.That(element.Icon).IsEqualTo("bootstrap:gear");
	}

	[Test]
	public async Task Build_ResourceWithNoNodeDetailsAnnotation_UsesResourceNameAsLabel()
	{
		// Arrange
		var resource = CreateProjectResource("my-api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Label).IsEqualTo("my-api");
	}

	[Test]
	public async Task Build_ProjectResource_InfersDotnetIcon()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_AzureResource_InfersBundledAzureIcon()
	{
		// Arrange
		var resource = new TestSystemResource("redis");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Azure.Redis", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_AzureSurrogateContainer_InfersAzureIconFromName()
	{
		// Arrange
		// RunAsContainer() produces a ContainerResource named after the Azure resource (e.g. "azure-redis")
		var resource = CreateContainerResource("azure-redis");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_PlainRedisContainer_UsesGenericTechIcon()
	{
		// Arrange
		var resource = CreateContainerResource("redis");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_PostgresResource_WithAzureTechnology_InfersAzureIcon()
	{
		// Arrange
		// When the user labels the technology "Azure Postgres", the cloud phase detects "azure"
		// and infers an azure postgres icon instead of the generic tech:postgresql.
		var resource = CreateContainerResource("postgres");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("Postgres")
				.WithTechnology("Azure Postgres")
				.WithDescription("Managed PostgreSQL")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon!).StartsWith("azure:");
		await Assert.That(model.Elements[0].Icon!).Contains("postgre");
	}

	[Test]
	public async Task Build_ContainerWithLibraryRedisImage_InfersRedisIcon()
	{
		// Arrange
		// Regression: "library/redis" was matching "heroku-redis" due to Jaccard tie-breaking.
		// "library" is a stop token, leaving query ["redis"] which should score "redis" at 1.0
		// and "heroku-redis" at 0.5 (unmatched "heroku" token penalty).
		var resource = CreateContainerResource("myredis");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "library/redis" });

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_ContainerWithLibraryPostgresImage_InfersPostgresqlIcon()
	{
		// Arrange
		// Regression: "library/postgres" was matching "testing-library" because "library"
		// dominated the score. Stop tokens now strip "library", leaving query ["postgres"]
		// which prefix-matches "postgresql" at 0.64 and non-prefix-matches "postgraphile" at 0.40.
		var resource = CreateContainerResource("mypostgres");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "library/postgres" });

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:postgresql");
	}

	[Test]
	public async Task Build_NodeAppInstallerExecutable_InfersNodejsIcon()
	{
		// Arrange
		// Regression: "node-app-installer" → queryTokens were ["node", "installer"] because
		// "installer" was not a stop token. The 2-token query diluted the score: 0.533 / 2 = 0.267
		// (below MinScore 0.35). Adding "installer" to QueryStopTokens leaves ["node"] → 0.533 ✓.
		var resource = new ExecutableResource("node-app-installer", "node", ".");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	[Test]
	public async Task Build_JavaScriptInstallerResource_InfersNodejsIcon()
	{
		// Arrange
		// Regression: JavaScriptInstallerResource (the real type behind .WithPnpm() in
		// Aspire.Hosting.JavaScript) was returning tech:java.  CamelCase-splitting
		// "JavaScriptInstallerResource" yields "java" + "script", and the lone "java" token
		// exactly matched tech:java at score 0.5.
		// Fix: the tokeniser now merges adjacent "java"+"script" bigrams into "javascript"
		// and a TokenAlias redirects "javascript" → "node", scoring tech:nodejs instead.
		// Using a neutral resource name ("js-tools") so the type-name path is the primary signal.
		var resource = new JavaScriptInstallerResource("js-tools");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	[Test]
	public async Task Build_PnpmInstallerResource_InfersPnpmIcon()
	{
		// Arrange
		// Best-overall scoring: when the resource name identifies a specific package manager
		// (e.g. "pnpm-installer"), its exact match (score 1.0) correctly beats the generic
		// type-name inference ("nodejs" at 0.533).  The result is more accurate — a pnpm
		// installer really should show the pnpm icon.
		var resource = new JavaScriptInstallerResource("pnpm-installer");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Executable", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:pnpm");
	}

	[Test]
	public async Task Build_JavaApplicationResource_StillInfersJavaIcon()
	{
		// Arrange
		// Regression guard: the "java"+"script" bigram merge must not affect genuine Java
		// resources where "java" appears without a following "script" token.
		var resource = new TestJavaAppResource("java-app");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(
				new CustomResourceSnapshot { ResourceType = "JavaApplication", Properties = [] }
			)
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:java");
	}

	[Test]
	public async Task Build_AzurePostgresRunAsContainer_InfersAzurePostgresIcon()
	{
		// Arrange
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

		// Act
		var model = ModelBuilder.Build([visibleContainer, hiddenAzureResource]);

		// Assert
		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-database-postgre-sql-server");
	}

	[Test]
	public async Task Build_AzureManagedRedisRunAsContainer_InfersAzureManagedRedisIcon()
	{
		// Arrange
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

		// Act
		var model = ModelBuilder.Build([visibleContainer, hiddenAzureResource]);

		// Assert
		await Assert.That(model.Elements).Count().IsEqualTo(1);
		await Assert.That(model.Elements[0].Icon).IsEqualTo("azure:azure-managed-redis");
	}

	[Test]
	public async Task Build_NodeAppExecutable_InfersNodejsIcon()
	{
		// Arrange
		// Regression: "node-app" was matching "node-sass" because both "node" and "sass" (unmatched)
		// gave a lower score than the corrected unmatched-token penalty.
		// After stop tokens strip "app", query is ["node"]; "nodejs" wins at 0.533 over "node-sass" at 0.5.
		var resource = new ExecutableResource("node-app", "node", ".");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	[Test]
	public async Task Build_WithProjectAutoIconsDisabled_DoesNotInferIcon()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], autoIconsEnabled: false);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsNull();
	}

	[Test]
	public async Task Build_WithPerResourceAutoIconDisabled_DoesNotInferIcon()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API")
				.WithTechnology(".NET")
				.WithDescription("HTTP API")
				.WithAutoIcon(false)
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsNull();
	}

	[Test]
	public async Task Build_WithPerResourceAutoIconEnabled_OverridesProjectSetting()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API").WithTechnology(".NET").WithDescription("HTTP API").WithAutoIcon(true)
		);

		// Act
		var model = ModelBuilder.Build([resource], autoIconsEnabled: false);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_ExplicitIcon_IsUsedWhenAutoIconsDisabled()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API")
				.WithTechnology(".NET")
				.WithDescription("HTTP API")
				.WithIcon("bootstrap:gear")
				.WithAutoIcon(false)
		);

		// Act
		var model = ModelBuilder.Build([resource], autoIconsEnabled: false);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("bootstrap:gear");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_OverridesAutoInference()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		static string? Resolver(IconResolverContext ctx) => ctx.Resource is ProjectResource ? "tech:dotnet" : null;

		// Act
		var model = ModelBuilder.Build([resource], iconResolvers: [Resolver]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_NullResultFallsBackToAutoInference()
	{
		// Arrange
		var resource = CreateContainerResource("redis");

		// Resolver explicitly declines; auto-inference should still pick tech:redis.
		static string? Resolver(IconResolverContext _) => null;

		// Act
		var model = ModelBuilder.Build([resource], iconResolvers: [Resolver]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	[Test]
	public async Task Build_WithCustomIconResolver_ContextExposesHiddenOriginal()
	{
		// Arrange
		IResource? capturedHidden = null;

		string? Resolver(IconResolverContext ctx)
		{
			capturedHidden = ctx.HiddenOriginal;
			return null;
		}

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

		// Act
		ModelBuilder.Build([hiddenAzureResource, visibleContainer], iconResolvers: [Resolver]);

		// Assert
		await Assert.That(capturedHidden).IsTypeOf<AzureManagedRedisResource>();
	}

	[Test]
	public async Task Build_WithMultipleCustomIconResolvers_FirstNonNullWins()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		var callOrder = new List<int>();

		string? First(IconResolverContext _)
		{
			callOrder.Add(1);
			return null;
		}

		string? Second(IconResolverContext _)
		{
			callOrder.Add(2);
			return "tech:custom";
		}

		string? Third(IconResolverContext _)
		{
			callOrder.Add(3);
			return "tech:should-not-reach";
		}

		// Act
		var model = ModelBuilder.Build([resource], iconResolvers: [First, Second, Third]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:custom");
		await Assert.That(callOrder).IsEquivalentTo([1, 2]);
	}

	[Test]
	public async Task Build_NodeAnnotation_WithHashPrefixedTag_NormalizesTagInModel()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithTag("#external"));

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Tags).Contains("external");
		await Assert.That(model.Elements[0].Tags).DoesNotContain("#external");
	}

	[Test]
	public async Task Build_ResourceRelationshipAnnotation_CreatesRelationship()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		var rel = model.Relationships[0];
		await Assert.That(rel.SourceName).IsEqualTo("api");
		await Assert.That(rel.TargetName).IsEqualTo("db");
	}

	[Test]
	public async Task Build_WaitForRelationship_IsSkipped()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "WaitFor"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_DuplicateRelationship_IsDeduped()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
	}

	[Test]
	public async Task Build_RelationshipToExcludedTarget_IsSkipped()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var hidden = new TestSystemResource("infra");
		hidden.Annotations.Add(new ExcludeFromLikeC4Annotation());
		api.Annotations.Add(new ResourceRelationshipAnnotation(hidden, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, hidden]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ResourceWithParent_SetsParentName()
	{
		// Arrange
		var parent = CreateContainerResource("postgres");
		var child = new TestChildResource("postgres-db", parent);

		// Act
		var model = ModelBuilder.Build([parent, child]);

		var childElement = model.Elements.Single(e => e.Name == "postgres-db");
		// Assert
		await Assert.That(childElement.ParentName).IsEqualTo("postgres");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_OverridesLabel()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("Reads from"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Reads from");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsNavigateTo()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithNavigateTo("db-detail-flow"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].NavigateTo).IsEqualTo("db-detail-flow");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsTechnologyAndDescription()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("db")
				.WithTechnology("PostgreSQL")
				.WithDescription("Stores user records")
		);

		// Act
		var model = ModelBuilder.Build([api, db]);

		var rel = model.Relationships[0];
		// Assert
		await Assert.That(rel.Technology).IsEqualTo("PostgreSQL");
		await Assert.That(rel.Description).IsEqualTo("Stores user records");
		await Assert.That(rel.Label).IsNull();
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_MatchesSurrogateName()
	{
		// Arrange
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
			new LikeC4RelationshipDetailsAnnotation("redis")
				.WithLabel("Caches sessions")
				.WithTechnology("Redis Protocol")
		);

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		var rel = model.Relationships[0];
		// Assert
		await Assert.That(rel.Label).IsEqualTo("Caches sessions");
		await Assert.That(rel.Technology).IsEqualTo("Redis Protocol");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_LastAnnotationWins()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("First"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("Last"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Last");
	}

	[Test]
	public async Task Build_NonReferenceRelationshipType_SetsLabel()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var queue = new TestSystemResource("queue");
		api.Annotations.Add(new ResourceRelationshipAnnotation(queue, "Publishes"));

		// Act
		var model = ModelBuilder.Build([api, queue]);

		// Assert
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Publishes");
	}

	[Test]
	public async Task Build_ReferenceToHiddenResourceWithVisibleSurrogate_ResolvesViaSurrogateName()
	{
		// Arrange
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

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].SourceName).IsEqualTo("node-app");
		await Assert.That(model.Relationships[0].TargetName).IsEqualTo("redis");
	}

	[Test]
	public async Task Build_WaitForToHiddenSurrogate_IsSkipped()
	{
		// Arrange
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

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_ReferenceToHiddenResourceWithNoSurrogate_IsSkipped()
	{
		// Arrange
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

		// Act
		var model = ModelBuilder.Build([hiddenResource, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).IsEmpty();
	}

	[Test]
	public async Task Build_MultipleReferencesToSurrogateSameName_DeduplicatesRelationship()
	{
		// Arrange
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

		// Act
		var model = ModelBuilder.Build([hiddenAzureRedis, visibleContainerRedis, nodeApp]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].SourceName).IsEqualTo("node-app");
		await Assert.That(model.Relationships[0].TargetName).IsEqualTo("redis");
	}

	[Test]
	public async Task Build_WithResourceStates_ElementsReflectStates()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		var states = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
		{
			["api"] = KnownResourceStates.Running,
			["db"] = KnownResourceStates.FailedToStart,
		};

		// Act
		var model = ModelBuilder.Build([api, db], states);

		var apiElement = model.Elements.Single(e => e.Name == "api");
		var dbElement = model.Elements.Single(e => e.Name == "db");
		// Assert
		await Assert.That(apiElement.State).IsEqualTo(KnownResourceStates.Running);
		await Assert.That(dbElement.State).IsEqualTo(KnownResourceStates.FailedToStart);
	}

	[Test]
	public async Task Build_WithNoResourceStates_ElementsDefaultToNull()
	{
		// Arrange
		var api = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([api]);

		// Assert
		await Assert.That(model.Elements[0].State).IsNull();
	}

	[Test]
	public async Task Build_WithPartialResourceStates_NullForMissingEntries()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		var states = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
		{
			["api"] = KnownResourceStates.Starting,
		};

		// Act
		var model = ModelBuilder.Build([api, db], states);

		var dbElement = model.Elements.Single(e => e.Name == "db");
		// Assert
		await Assert.That(dbElement.State).IsNull();
	}

	[Test]
	public async Task GetVisibleResourceNames_ExcludesHiddenAndAnnotatedResources()
	{
		// Arrange
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

		var names = ModelBuilder.GetVisibleResourceNames([visible, hidden, excluded]);

		// Act
		// Assert
		await Assert.That(names).Contains("api");
		await Assert.That(names).DoesNotContain("infra");
		await Assert.That(names).DoesNotContain("excluded");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_IsEmittedWithoutResourceRelationshipAnnotation()
	{
		// Arrange
		// Simulates postgres.WithLikeC4Reference(azurePostgres, opts => ...) where no
		// WithReference was called — a purely diagram-level relationship.
		var local = CreateContainerResource("postgres");
		var azure = CreateContainerResource("azure-postgres");
		local.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("azure-postgres")
				.WithLabel("syncs with")
				.WithTechnology("PostgreSQL / JDBC")
		);

		// Act
		var model = ModelBuilder.Build([local, azure]);

		// Assert
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
		// Arrange
		// If WithReference AND WithLikeC4Reference are both called, only one edge should appear.
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("db").WithLabel("Queries").WithTechnology("SQL"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships).Count().IsEqualTo(1);
		await Assert.That(model.Relationships[0].Label).IsEqualTo("Queries");
	}

	[Test]
	public async Task Build_RelationshipDetailsAnnotation_SetsKind()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var queue = new TestSystemResource("queue");
		api.Annotations.Add(new ResourceRelationshipAnnotation(queue, "Reference"));
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation("queue").WithKind("async"));

		// Act
		var model = ModelBuilder.Build([api, queue]);

		// Assert
		await Assert.That(model.Relationships[0].Kind).IsEqualTo("async");
	}

	[Test]
	public async Task Build_DiagramOnlyRelationship_PropagatesKind()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var queue = new TestSystemResource("queue");
		api.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation("queue")
				.WithLabel("Publishes")
				.WithTechnology("AMQP")
				.WithKind("async")
		);

		// Act
		var model = ModelBuilder.Build([api, queue]);

		// Assert
		await Assert.That(model.Relationships[0].Kind).IsEqualTo("async");
	}

	[Test]
	public async Task Build_RelationshipWithNoKind_KindIsNull()
	{
		// Arrange
		var api = CreateProjectResource("api");
		var db = new TestDatabaseResource("db");
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));

		// Act
		var model = ModelBuilder.Build([api, db]);

		// Assert
		await Assert.That(model.Relationships[0].Kind).IsNull();
	}

	[Test]
	public async Task Build_AwsLambdaResource_InfersAwsIcon()
	{
		// Arrange
		var resource = new TestSystemResource("fn");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "AWS.Lambda", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("aws:lambda");
	}

	[Test]
	public async Task Build_GcpPubSubResource_InfersGcpIcon()
	{
		// Arrange
		var resource = new TestSystemResource("queue");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "GCP.PubSub", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("gcp:pub-sub");
	}

	[Test]
	public async Task Build_RabbitMqContainer_InfersTechIcon()
	{
		// Arrange
		var resource = CreateContainerResource("rabbitmq");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:rabbitmq");
	}

	[Test]
	public async Task Build_MongoDbContainer_InfersTechIcon()
	{
		// Arrange
		var resource = CreateContainerResource("mongodb");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:mongodb");
	}

	// ── AutoIncludeAspireMetadata tests ───────────────────────────────────────

	[Test]
	public async Task Build_AutoMetadata_Default_InjectsAspireName()
	{
		// Arrange
		var resource = CreateProjectResource("my-api");

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "aspire-name" && m.Value == "my-api")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_Default_InjectsAspireType()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new ResourceSnapshotAnnotation(new CustomResourceSnapshot { ResourceType = "Project", Properties = [] })
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "aspire-type" && m.Value == "Project")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_None_DoesNotInjectMetadata()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.None);

		// Assert
		await Assert.That(model.Elements[0].Metadata).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_MetadataOnly_NoAutoLinks()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http")
		);

		// Act
		var model = ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.Metadata);

		// Assert
		await Assert.That(model.Elements[0].Links).IsEmpty();
		await Assert.That(model.Elements[0].Metadata.Any(m => m.Key == "aspire-name")).IsTrue();
	}

	[Test]
	public async Task Build_AutoMetadata_LinksOnly_NoAspireNameMetadata()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], aspireMetadataInclusion: AspireMetadataInclusion.Links);

		// Assert
		await Assert.That(model.Elements[0].Metadata.Any(m => m.Key == "aspire-name")).IsFalse();
	}

	[Test]
	public async Task Build_AutoMetadata_AllocatedHttpEndpoint_InjectsLink()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);

		// Act
		var model = ModelBuilder.Build([resource]);

		var links = model.Elements[0].Links;
		var injected = links.FirstOrDefault(l => l.Uri == "http://localhost:5000");
		// Assert
		await Assert.That(injected).IsNotNull();
		await Assert.That(injected!.Title).IsEqualTo("Endpoint: http");
	}

	[Test]
	public async Task Build_AutoMetadata_UnallocatedEndpoint_DoesNotInjectLink()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Links).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_NonHttpEndpoint_DoesNotInjectLink()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "grpc", name: "grpc");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5001);
		resource.Annotations.Add(endpoint);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Links).IsEmpty();
	}

	[Test]
	public async Task Build_AutoMetadata_UserMetadataPreservedAndNotDuplicated()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("aspire-name", "custom-override"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var aspireNameEntries = model.Elements[0].Metadata.Where(m => m.Key == "aspire-name").ToList();
		// User-provided entry present, auto-generated one should not duplicate it
		// Assert
		await Assert.That(aspireNameEntries).Count().IsEqualTo(1);
		await Assert.That(aspireNameEntries[0].Value).IsEqualTo("custom-override");
	}

	[Test]
	public async Task Build_AutoMetadata_UserLinksPreservedAndEndpointNotDuplicated()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API").WithLink("http://localhost:5000", "My Service")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		var httpLinks = model.Elements[0].Links.Where(l => l.Uri == "http://localhost:5000").ToList();
		// Should appear only once — auto-injected duplicate is suppressed
		// Assert
		await Assert.That(httpLinks).Count().IsEqualTo(1);
		await Assert.That(httpLinks[0].Title).IsEqualTo("My Service");
	}

	// ── NormaliseMetadataBehaviour tests ──────────────────────────────────────

	[Test]
	public async Task Build_NormaliseMetadata_Default_ReplacesSpaceWithUnderscore()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("Azure SKU", "Standard"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "Azure_SKU" && m.Value == "Standard")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_DuplicateKeysAreNormalisedAndOutput()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API")
				.WithMetadata("Azure SKU", "Entry 1")
				.WithMetadata("Azure SKU", "Entry 2")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements.Count).IsEqualTo(1);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Count(m => m.Key == "Azure_SKU")).IsEqualTo(2);
	}

	[Test]
	public async Task Build_NormaliseMetadata_Default_PreservesValidChars()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("valid-key_123", "value"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "valid-key_123")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseLowercase_LowercasesKey()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("Azure SKU", "Standard"));

		// Act
		var model = ModelBuilder.Build(
			[resource],
			normaliseMetadataBehaviour: NormaliseMetadataBehaviour.NormaliseLowercase
		);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "azure_sku" && m.Value == "Standard")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Throw_ThrowsOnInvalidKey()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("Azure SKU", "Standard"));

		// Act
		// Assert
		await Assert
			.That(() => ModelBuilder.Build([resource], normaliseMetadataBehaviour: NormaliseMetadataBehaviour.Throw))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Throw_AcceptsValidKey()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("valid-key_123", "value"));

		// Act
		var model = ModelBuilder.Build([resource], normaliseMetadataBehaviour: NormaliseMetadataBehaviour.Throw);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "valid-key_123")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Normalise_ThrowsOnNullKey()
	{
		// Arrange
		// Act
		// Assert
		await Assert
			.That(() => new LikeC4NodeDetailsAnnotation("API").WithMetadata(null!, "value"))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Default_ReplacesMultipleInvalidChars()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("My Key (v2)!", "value"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "My_Key__v2__")).IsTrue();
	}

	// ── Dashboard deep-link tests ─────────────────────────────────────────────

	// ── StateTagMap tests ─────────────────────────────────────────────────────

	[Test]
	public async Task Build_StateTagMap_RunningOverride_PrependsStateTagToElementTags()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.Running } };
		var stateTagMap = new Dictionary<string, string?> { [KnownResourceStates.Running] = "custom-running-tag" };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).Contains("custom-running-tag");
	}

	[Test]
	public async Task Build_StateTagMap_NullOverride_DoesNotPrependTag()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.Running } };
		var stateTagMap = new Dictionary<string, string?> { [KnownResourceStates.Running] = null };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).IsEmpty();
	}

	[Test]
	public async Task Build_StateTagMap_NullMap_AutoDerivesStateTagOnElement()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.Running } };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: null);

		var element = model.Elements[0];
		// Assert
		await Assert.That(element.Tags).Contains("aspire-run-state-running");
	}

	[Test]
	public async Task Build_StateTagMap_StateTagPrependsBeforeUserTags()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithTag("backend").WithTag("v2"));

		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.FailedToStart } };
		var stateTagMap = new Dictionary<string, string?> { [KnownResourceStates.FailedToStart] = "custom-error-tag" };

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		var tags = model.Elements[0].Tags;
		// Assert
		await Assert.That(tags[0]).IsEqualTo("custom-error-tag");
		await Assert.That(tags).Contains("backend");
		await Assert.That(tags).Contains("v2");
	}

	[Test]
	public async Task Build_StateTagMap_CustomTagName_UsedAsStateTag()
	{
		// Arrange
		var resource = CreateContainerResource("api");
		var states = new Dictionary<string, string?> { { "api", KnownResourceStates.RuntimeUnhealthy } };
		var stateTagMap = new Dictionary<string, string?>
		{
			[KnownResourceStates.RuntimeUnhealthy] = "my-custom-failed-tag",
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap);

		// Assert
		await Assert.That(model.Elements[0].Tags).Contains("my-custom-failed-tag");
	}

	[Test]
	public async Task Build_DashboardLinks_WithBaseUrl_InjectsConsoleAndStructuredLogLinks()
	{
		// Arrange
		var resource = CreateProjectResource("my-api");

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: "https://localhost:15086");

		var links = model.Elements[0].Links;
		var consoleLink = links.FirstOrDefault(l => l.Title == "Dashboard: Console Logs");
		var structuredLink = links.FirstOrDefault(l => l.Title == "Dashboard: Structured Logs");

		// Assert
		await Assert.That(consoleLink).IsNotNull();
		await Assert.That(structuredLink).IsNotNull();
		await Assert.That(consoleLink!.Uri).IsEqualTo("https://localhost:15086/consolelogs/resource/my-api");
		await Assert.That(structuredLink!.Uri).IsEqualTo("https://localhost:15086/structuredlogs/resource/my-api");
	}

	[Test]
	public async Task Build_DashboardLinks_WithTokenAndBaseUrl_GeneratesLoginRedirectUrls()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build(
			[resource],
			dashboardBaseUrl: "https://localhost:15086",
			dashboardBrowserToken: "secret-token"
		);

		var links = model.Elements[0].Links;
		var consoleLink = links.First(l => l.Title == "Dashboard: Console Logs");
		var structuredLink = links.First(l => l.Title == "Dashboard: Structured Logs");

		var consolePath = Uri.EscapeDataString("/consolelogs/resource/api");
		var structuredPath = Uri.EscapeDataString("/structuredlogs/resource/api");
		var encodedToken = Uri.EscapeDataString("secret-token");

		// Assert
		await Assert
			.That(consoleLink.Uri)
			.IsEqualTo($"https://localhost:15086/login?t={encodedToken}&returnUrl={consolePath}");
		await Assert
			.That(structuredLink.Uri)
			.IsEqualTo($"https://localhost:15086/login?t={encodedToken}&returnUrl={structuredPath}");
	}

	[Test]
	public async Task Build_DashboardLinks_WithSpecialCharsInResourceName_EncodesNameInUrl()
	{
		// Arrange
		var resource = CreateProjectResource("my api+service");

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: "https://localhost:15086");

		var consoleLink = model.Elements[0].Links.FirstOrDefault(l => l.Title == "Dashboard: Console Logs");
		// Assert
		await Assert.That(consoleLink).IsNotNull();
		await Assert
			.That(consoleLink!.Uri)
			.IsEqualTo($"https://localhost:15086/consolelogs/resource/{Uri.EscapeDataString("my api+service")}");
	}

	[Test]
	public async Task Build_DashboardLinks_WithNoDashboardUrl_NoLinksInjected()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: null);

		var dashboardLinks = model
			.Elements[0]
			.Links.Where(l => l.Title?.StartsWith("Dashboard:", StringComparison.Ordinal) == true);
		// Assert
		await Assert.That(dashboardLinks).IsEmpty();
	}

	[Test]
	public async Task Build_DashboardLinks_Disabled_NoLinksInjectedEvenWithBaseUrl()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build(
			[resource],
			includeDashboardLinks: false,
			dashboardBaseUrl: "https://localhost:15086"
		);

		var dashboardLinks = model
			.Elements[0]
			.Links.Where(l => l.Title?.StartsWith("Dashboard:", StringComparison.Ordinal) == true);
		// Assert
		await Assert.That(dashboardLinks).IsEmpty();
	}

	[Test]
	public async Task Build_DashboardLinks_LinksInclusionDisabled_NoLinksInjected()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		// Act
		var model = ModelBuilder.Build(
			[resource],
			aspireMetadataInclusion: AspireMetadataInclusion.Metadata,
			dashboardBaseUrl: "https://localhost:15086"
		);

		var dashboardLinks = model
			.Elements[0]
			.Links.Where(l => l.Title?.StartsWith("Dashboard:", StringComparison.Ordinal) == true);
		// Assert
		await Assert.That(dashboardLinks).IsEmpty();
	}

	[Test]
	public async Task Build_DashboardLinks_UserLinkWithSameUriNotDuplicated()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		var expectedUrl = "https://localhost:15086/consolelogs/resource/api";
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithLink(expectedUrl, "My custom link"));

		// Act
		var model = ModelBuilder.Build([resource], dashboardBaseUrl: "https://localhost:15086");

		var matchingLinks = model.Elements[0].Links.Where(l => l.Uri == expectedUrl).ToList();
		// Assert
		await Assert.That(matchingLinks).Count().IsEqualTo(1);
		await Assert.That(matchingLinks[0].Title).IsEqualTo("My custom link");
	}

	// ── Snapshot URL endpoint link tests ─────────────────────────────────────

	[Test]
	public async Task Build_SnapshotEndpointUrls_UsedInsteadOfAllocatedEndpoint()
	{
		// Arrange
		// When resourceSnapshotUrls are provided they should take precedence over
		// EndpointAnnotation.AllocatedEndpoint (which may reflect an internal/wrong port).
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 9999); // wrong port
		resource.Annotations.Add(endpoint);

		var snapshotUrls = new Dictionary<string, IReadOnlyList<(string Url, string Name)>>(
			StringComparer.OrdinalIgnoreCase
		)
		{
			["api"] = [("http://localhost:5000", "http")],
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: snapshotUrls);

		var links = model.Elements[0].Links;
		// Assert
		await Assert.That(links.Any(l => l.Uri == "http://localhost:5000")).IsTrue();
		await Assert.That(links.Any(l => l.Uri == "http://localhost:9999")).IsFalse();
		await Assert.That(links.First(l => l.Uri == "http://localhost:5000").Title).IsEqualTo("Endpoint: http");
	}

	[Test]
	public async Task Build_SnapshotEndpointUrls_MultipleEndpoints_AllInjected()
	{
		// Arrange
		var resource = CreateProjectResource("api");

		var snapshotUrls = new Dictionary<string, IReadOnlyList<(string Url, string Name)>>(
			StringComparer.OrdinalIgnoreCase
		)
		{
			["api"] = [("http://localhost:5000", "http"), ("https://localhost:5001", "https")],
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: snapshotUrls);

		var links = model.Elements[0].Links;
		// Assert
		await Assert.That(links.Any(l => l.Uri == "http://localhost:5000" && l.Title == "Endpoint: http")).IsTrue();
		await Assert.That(links.Any(l => l.Uri == "https://localhost:5001" && l.Title == "Endpoint: https")).IsTrue();
	}

	[Test]
	public async Task Build_SnapshotEndpointUrls_WhenNull_FallsBackToAllocatedEndpoint()
	{
		// Arrange
		// No snapshot URLs → should fall back to EndpointAnnotation as before.
		var resource = CreateProjectResource("api");
		var endpoint = new EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, uriScheme: "http", name: "http");
		endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", 5000);
		resource.Annotations.Add(endpoint);

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: null);

		var links = model.Elements[0].Links;
		// Assert
		await Assert.That(links.Any(l => l.Uri == "http://localhost:5000")).IsTrue();
	}

	[Test]
	public async Task Build_SnapshotEndpointUrls_DeduplicatesWithUserLinks()
	{
		// Arrange
		// If the user manually added the same URL via WithLink, it should not appear twice.
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithLink("http://localhost:5000", "My link"));

		var snapshotUrls = new Dictionary<string, IReadOnlyList<(string Url, string Name)>>(
			StringComparer.OrdinalIgnoreCase
		)
		{
			["api"] = [("http://localhost:5000", "http")],
		};

		// Act
		var model = ModelBuilder.Build([resource], resourceSnapshotUrls: snapshotUrls);

		var matchingLinks = model.Elements[0].Links.Where(l => l.Uri == "http://localhost:5000").ToList();
		// Assert
		await Assert.That(matchingLinks).Count().IsEqualTo(1);
		await Assert.That(matchingLinks[0].Title).IsEqualTo("My link");
	}

	// ── NodeAppResource duplicate-token regression tests ──────────────────────

	[Test]
	public async Task Build_NodeAppResource_FullNamespace_InfersNodejsIcon()
	{
		// Arrange
		// Regression: Aspire.Hosting.JavaScript.NodeAppResource tokenises as
		// "aspire" + "hosting" + "javascript"→"node" + "node" + "app" + "resource".
		// After stop-token removal that gave ["node", "node"] — duplicate tokens inflated
		// the node-sass score above nodejs.  After .Distinct(), query is ["node"] → nodejs wins.
		var resource = new NodeAppResource("my-node-app");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
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

	// Named to match the real Aspire.Hosting.JavaScript.JavaScriptInstallerResource so that
	// the icon matcher sees "JavaScript" in the type's short name and triggers the bigram fix.
	sealed class JavaScriptInstallerResource(string name) : Resource(name);

	// Generic Java app — "java" appears without a following "script" token, so tech:java
	// should still be inferred.
	sealed class TestJavaAppResource(string name) : Resource(name);

	// ── Uppercase-run CamelCase + best-overall scoring regression tests ──────

	[Test]
	public async Task Build_RabbitMQTypeName_InfersRabbitmqIcon()
	{
		// Arrange
		// Regression: "RabbitMQContainerResource" previously normalised to "rabbit mqcontainer resource"
		// because the uppercase-uppercase-lowercase boundary (MQ→C) was not split.
		// Fix: NormalizeForIconLookup now detects the transition and produces
		// "rabbit mq container resource" → stop ["container","resource"] → queryTokens ["rabbit","mq"].
		// effectiveQueryLength = 1 (only "rabbit" ≥ MinContainmentLength=3; "mq" is excluded from
		// the denominator but "rabbit" prefix-matches "rabbitmq" at 0.6/1 = 0.6 → tech:rabbitmq.
		var resource = new RabbitMQContainerResource("my-queue");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:rabbitmq");
	}

	[Test]
	public async Task Build_MySQLTypeName_InfersMysqlIcon()
	{
		// Arrange
		// The CamelCase fix for uppercase-run boundaries produces "my sql database resource"
		// from "MySQLDatabaseResource" (previously "my sqldatabase resource").
		// In practice, MySQL resources are named "mysql" or similar — the resource name is the
		// primary signal and produces an exact match for tech:mysql.
		var resource = new MySQLDatabaseResource("mysql");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:mysql");
	}

	[Test]
	public async Task Build_BestOverallScoring_ResourceNameWinsOverNoisyTypeName()
	{
		// Arrange
		// Best-overall scoring: even when a noisy early candidate (type FullName) produces
		// query tokens that score marginally above MinScore for a wrong icon, the clean resource
		// name candidate scores higher overall and wins.
		// This resource has a generic type name but a clear resource name "mongodb".
		var resource = new GenericDatabaseContainerResource("mongodb");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:mongodb");
	}

	[Test]
	public async Task Build_NumericOnlyTokensFiltered_FromIconMatching()
	{
		// Arrange
		// Pure numeric tokens like "7" or "16" (e.g. from Docker tag tokenisation of
		// "redis-7" or version-suffixed names) must not inflate queryTokens.Length
		// and dilute the score of legitimate tokens.
		// Resource name "redis-7" → tokens ["redis","7"] → filter "7" → ["redis"] → exact match.
		var resource = CreateContainerResource("redis-7");
		resource.Annotations.Add(new ContainerImageAnnotation { Image = "redis" });

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:redis");
	}

	// Named to simulate a RabbitMQ container resource so the type-name candidate path is exercised.
	sealed class RabbitMQContainerResource(string name) : Resource(name);

	// Named to simulate a MySQL database resource.
	sealed class MySQLDatabaseResource(string name) : Resource(name);

	// Generic type name — the resource name provides the icon signal.
	sealed class GenericDatabaseContainerResource(string name) : Resource(name);

	// Named to match the real Aspire.Hosting.JavaScript.NodeAppResource so that the icon
	// matcher tokenises "Node" + "App" separately, and "JavaScript" in the parent namespace
	// is tokenised separately too — producing duplicate "node" tokens before the Distinct() fix.
	sealed class NodeAppResource(string name) : Resource(name);
}
