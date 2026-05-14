using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	[Test]
	public async Task Build_StrictAll_MultipleViolations_ThrowsOnFirst()
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
	public async Task Build_StrictAll_ValidInput_DoesNotThrow()
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
}
