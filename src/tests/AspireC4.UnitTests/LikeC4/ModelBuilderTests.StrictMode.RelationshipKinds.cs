using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
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
}
