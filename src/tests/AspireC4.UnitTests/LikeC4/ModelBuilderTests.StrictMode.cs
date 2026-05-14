using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	// ── Strict mode — Tags ────────────────────────────────────────────────────

	[Test]
	public async Task Build_StrictTags_AllowedTag_DoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("external"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags, tags: ["external"]);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictTags_DisallowedElementTag_ThrowsInvalidOperationException()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("unknown-tag"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags, tags: ["external"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([resource], strict: strict))
			.ThrowsException()
			.WithMessageContaining("unknown-tag", StringComparison.Ordinal);
	}

	[Test]
	public async Task Build_StrictTags_DisallowedRelationshipTag_ThrowsInvalidOperationException()
	{
		// Arrange
		var (api, db) = CreateApiAndDbResources();
		var annotation = new LikeC4RelationshipDetailsAnnotation(db.Name).WithTag("not-allowed");
		api.Annotations.Add(annotation);
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags, tags: ["allowed"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([api, db], strict: strict))
			.ThrowsException()
			.WithMessageContaining("not-allowed", StringComparison.Ordinal);
	}

	[Test]
	public async Task Build_StrictTags_NoneMode_DisallowedTagDoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("any-tag"));
		var strict = CreateStrictOptions(AspireC4StrictMode.None, tags: []);

		// Act / Assert — strict.Mode is None, so no validation occurs
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictTags_StateTagsExemptFromValidation()
	{
		// Arrange — the state tag "aspire-run-state-running" is auto-injected, not user-defined
		var resource = CreateProjectResource("api");
		var states = new Dictionary<string, string?> { ["api"] = KnownResourceStates.Running };
		IReadOnlyDictionary<string, string?> stateTagMap = new Dictionary<string, string?>
		{
			[KnownResourceStates.Running] = "aspire-run-state-running",
		};
		// No tags in allowed list — state tags must be exempt
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags, tags: []);

		// Act / Assert
		await Assert
			.That(() =>
				ModelBuilder.Build([resource], resourceStates: states, stateTagMap: stateTagMap, strict: strict)
			)
			.ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictTags_HashPrefixStrippedBeforeComparison()
	{
		// Arrange — allowed list uses bare name; tag added with # prefix
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("#external"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags, tags: ["external"]);

		// Act / Assert — "external" and "#external" normalise to the same value
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictTags_NullStrict_NoValidation()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("anything"));

		// Act / Assert — null strict means no validation
		await Assert.That(() => ModelBuilder.Build([resource], strict: null)).ThrowsNothing();
	}

	// ── Strict mode — RelationshipKinds ──────────────────────────────────────

	[Test]
	public async Task Build_StrictRelationshipKinds_AllowedKind_DoesNotThrow()
	{
		// Arrange
		var (api, db) = CreateApiAndDbResources();
		var annotation = new LikeC4RelationshipDetailsAnnotation(db.Name).WithKind("async");
		api.Annotations.Add(annotation);
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		var strict = CreateStrictOptions(AspireC4StrictMode.RelationshipKinds, relationshipKinds: ["async"]);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([api, db], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictRelationshipKinds_DisallowedKind_ThrowsInvalidOperationException()
	{
		// Arrange
		var (api, db) = CreateApiAndDbResources();
		var annotation = new LikeC4RelationshipDetailsAnnotation(db.Name).WithKind("grpc");
		api.Annotations.Add(annotation);
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		var strict = CreateStrictOptions(AspireC4StrictMode.RelationshipKinds, relationshipKinds: ["async"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([api, db], strict: strict))
			.ThrowsException()
			.WithMessageContaining("grpc", StringComparison.Ordinal);
	}

	[Test]
	public async Task Build_StrictRelationshipKinds_NullKind_DoesNotThrow()
	{
		// Arrange — relationship without a kind should not be validated
		var (api, db) = CreateApiAndDbResources();
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		var strict = CreateStrictOptions(AspireC4StrictMode.RelationshipKinds, relationshipKinds: []);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([api, db], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictRelationshipKinds_DiagramOnlyRelationship_DisallowedKind_Throws()
	{
		// Arrange — diagram-only relationship (WithLikeC4Reference without WithReference)
		var (api, db) = CreateApiAndDbResources();
		var annotation = new LikeC4RelationshipDetailsAnnotation(db.Name).WithKind("grpc");
		api.Annotations.Add(annotation);
		// Intentionally no ResourceRelationshipAnnotation — this is a diagram-only relationship
		var strict = CreateStrictOptions(AspireC4StrictMode.RelationshipKinds, relationshipKinds: ["async"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([api, db], strict: strict))
			.ThrowsException()
			.WithMessageContaining("grpc", StringComparison.Ordinal);
	}

	// ── Strict mode — Groups ──────────────────────────────────────────────────

	[Test]
	public async Task Build_StrictGroups_AllowedGroup_DoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4GroupAnnotation("Frontend"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: ["Frontend"]);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictGroups_DisallowedGroup_ThrowsInvalidOperationException()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4GroupAnnotation("UnknownGroup"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: ["Frontend", "Backend"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([resource], strict: strict))
			.ThrowsException()
			.WithMessageContaining("UnknownGroup", StringComparison.Ordinal);
	}

	[Test]
	public async Task Build_StrictGroups_NoGroup_DoesNotThrow()
	{
		// Arrange — resource without a group should not be validated
		var resource = CreateProjectResource("api");
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: []);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictGroups_GroupComparisonIsCaseInsensitive()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4GroupAnnotation("frontend"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: ["Frontend"]);

		// Act / Assert — comparison should be case-insensitive
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	// ── Strict mode — MetadataKeys ────────────────────────────────────────────

	[Test]
	public async Task Build_StrictMetadataKeys_AllowedKey_DoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithMetadata("custom-key", "value"));
		var strict = CreateStrictOptions(AspireC4StrictMode.MetadataKeys, metadataKeys: ["custom-key"]);

		// Act / Assert
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictMetadataKeys_DisallowedElementKey_ThrowsInvalidOperationException()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithMetadata("secret-key", "value"));
		var strict = CreateStrictOptions(AspireC4StrictMode.MetadataKeys, metadataKeys: ["allowed-key"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([resource], strict: strict))
			.ThrowsException()
			.WithMessageContaining("secret-key", StringComparison.Ordinal);
	}

	[Test]
	public async Task Build_StrictMetadataKeys_AutoAspireKeysExempt()
	{
		// Arrange — aspire-name and aspire-type are auto-injected and must not be validated
		var resource = CreateProjectResource("api");
		// No user metadata — only auto-injected aspire-name/aspire-type will appear
		var strict = CreateStrictOptions(
			AspireC4StrictMode.MetadataKeys,
			metadataKeys: [] // empty allowed list — auto keys must be excluded
		);

		// Act / Assert
		await Assert
			.That(() =>
				ModelBuilder.Build(
					[resource],
					aspireMetadataInclusion: AspireMetadataInclusion.Metadata,
					strict: strict
				)
			)
			.ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictMetadataKeys_DisallowedRelationshipKey_Throws()
	{
		// Arrange
		var (api, db) = CreateApiAndDbResources();
		var annotation = new LikeC4RelationshipDetailsAnnotation(db.Name).WithMetadata("secret-key", "val");
		api.Annotations.Add(annotation);
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		var strict = CreateStrictOptions(AspireC4StrictMode.MetadataKeys, metadataKeys: ["allowed-key"]);

		// Act / Assert
		await Assert
			.That(() => ModelBuilder.Build([api, db], strict: strict))
			.ThrowsException()
			.WithMessageContaining("secret-key", StringComparison.Ordinal);
	}

	// ── Strict mode — combined flags ──────────────────────────────────────────

	[Test]
	public async Task Build_StrictAll_AllViolationsExceptFirst_SecondViolationThrowsAfterFirst()
	{
		// Arrange — multiple violations; first encountered throws immediately
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("bad-tag"));
		resource.Annotations.Add(new LikeC4GroupAnnotation("bad-group"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags | AspireC4StrictMode.Groups, tags: [], groups: []);

		// Act / Assert — first violation (tags) is thrown immediately
		await Assert
			.That(() => ModelBuilder.Build([resource], strict: strict))
			.ThrowsException()
			.WithMessageContaining("bad-tag", StringComparison.Ordinal);
	}

	[Test]
	public async Task Build_StrictAll_TagsAndRelationshipKindsEnforced_ValidInput_DoesNotThrow()
	{
		// Arrange
		var (api, db) = CreateApiAndDbResources();
		api.Annotations.Add(new LikeC4NodeDetailsAnnotation(api.Name).WithTag("service"));
		var relAnnotation = new LikeC4RelationshipDetailsAnnotation(db.Name).WithKind("async");
		api.Annotations.Add(relAnnotation);
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		var strict = CreateStrictOptions(
			AspireC4StrictMode.All,
			tags: ["service"],
			relationshipKinds: ["async"],
			groups: [],
			metadataKeys: []
		);

		// Act / Assert
		await Assert
			.That(() =>
				ModelBuilder.Build([api, db], aspireMetadataInclusion: AspireMetadataInclusion.None, strict: strict)
			)
			.ThrowsNothing();
	}

	// ── Strict mode — exception message quality ───────────────────────────────

	[Test]
	public async Task Build_StrictTags_ExceptionMessage_ContainsWithAllowedTagHint()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("bad-tag"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags, tags: []);

		// Act
		var exception = await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsException();

		// Assert — message should guide the developer
		await Assert.That(exception!.Message).Contains(nameof(AspireC4DiagramOptionsExtensions.WithAllowedTag));
	}

	[Test]
	public async Task Build_StrictRelationshipKinds_ExceptionMessage_ContainsWithAllowedRelationshipKindHint()
	{
		// Arrange
		var (api, db) = CreateApiAndDbResources();
		api.Annotations.Add(new LikeC4RelationshipDetailsAnnotation(db.Name).WithKind("grpc"));
		api.Annotations.Add(new ResourceRelationshipAnnotation(db, "Reference"));
		var strict = CreateStrictOptions(AspireC4StrictMode.RelationshipKinds, relationshipKinds: []);

		// Act
		var exception = await Assert.That(() => ModelBuilder.Build([api, db], strict: strict)).ThrowsException();

		// Assert
		await Assert
			.That(exception!.Message)
			.Contains(nameof(AspireC4DiagramOptionsExtensions.WithAllowedRelationshipKind));
	}

	[Test]
	public async Task Build_StrictGroups_ExceptionMessage_ContainsWithAllowedGroupHint()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4GroupAnnotation("MyGroup"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Groups, groups: []);

		// Act
		var exception = await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsException();

		// Assert
		await Assert.That(exception!.Message).Contains(nameof(AspireC4DiagramOptionsExtensions.WithAllowedGroup));
	}

	[Test]
	public async Task Build_StrictMetadataKeys_ExceptionMessage_ContainsWithAllowedMetadataKeyHint()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithMetadata("my-key", "val"));
		var strict = CreateStrictOptions(AspireC4StrictMode.MetadataKeys, metadataKeys: []);

		// Act
		var exception = await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsException();

		// Assert
		await Assert.That(exception!.Message).Contains(nameof(AspireC4DiagramOptionsExtensions.WithAllowedMetadataKey));
	}

	// ── Strict mode helpers ───────────────────────────────────────────────────

	static AspireC4StrictOptions CreateStrictOptions(
		AspireC4StrictMode mode,
		IEnumerable<string>? tags = null,
		IEnumerable<string>? relationshipKinds = null,
		IEnumerable<string>? groups = null,
		IEnumerable<string>? metadataKeys = null
	) =>
		new()
		{
			Mode = mode,
			Tags = tags?.ToHashSet(StringComparer.Ordinal) ?? [],
			RelationshipKinds = relationshipKinds?.ToHashSet(StringComparer.Ordinal) ?? [],
			Groups = groups?.ToHashSet(StringComparer.Ordinal) ?? [],
			MetadataKeys = metadataKeys?.ToHashSet(StringComparer.Ordinal) ?? [],
		};

	static (ProjectResource Api, ContainerResource Db) CreateApiAndDbResources()
	{
		var api = CreateProjectResource("api");
		var db = CreateContainerResource("db");
		return (api, db);
	}
}
