using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
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
	public async Task Build_StrictTags_NoneMode_DisallowedTag_DoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("any-tag"));
		var strict = CreateStrictOptions(AspireC4StrictMode.None, tags: []);

		// Act / Assert — strict.Mode is None, so no validation occurs
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictTags_StateTagsExempt_DoesNotThrow()
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
	public async Task Build_StrictTags_HashPrefixNormalized_DoesNotThrow()
	{
		// Arrange — allowed list uses bare name; tag added with # prefix
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("#external"));
		var strict = CreateStrictOptions(AspireC4StrictMode.Tags, tags: ["external"]);

		// Act / Assert — "external" and "#external" normalise to the same value
		await Assert.That(() => ModelBuilder.Build([resource], strict: strict)).ThrowsNothing();
	}

	[Test]
	public async Task Build_StrictTags_NullStrict_DoesNotThrow()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation(resource.Name).WithTag("anything"));

		// Act / Assert — null strict means no validation
		await Assert.That(() => ModelBuilder.Build([resource], strict: null)).ThrowsNothing();
	}
}
