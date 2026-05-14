using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
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
}
