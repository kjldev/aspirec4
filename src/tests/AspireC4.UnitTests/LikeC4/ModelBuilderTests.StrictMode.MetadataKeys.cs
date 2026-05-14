using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
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
	public async Task Build_StrictMetadataKeys_AutoAspireKeysExempt_DoesNotThrow()
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
}
