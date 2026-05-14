using Aspire.Hosting.ApplicationModel;
using static Aspire.Hosting.TestHelpers;

namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4RelationshipDetailsAnnotationTests
{
	// ── WithLikeC4Reference<IResourceWithEnvironment> tests ───────────────────
	// The IResourceWithEnvironment overload is disambiguated from the generic IResource overload
	// by passing the optional parameter (unique to this overload) or by using an explicit cast.

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_AddsLikeC4AnnotationWithTargetName()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		// Act
		api.WithLikeC4Reference(db, configure: null, optional: false);

		// Assert
		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().SingleOrDefault();
		await Assert.That(annotation).IsNotNull();
		await Assert.That(annotation!.TargetName).IsEqualTo("db");
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_AlsoAddsAspireWithReference()
	{
		// Arrange
		// Passing optional: false forces the IResourceWithEnvironment overload which calls WithReference.
		var appBuilder = CreateAppBuilder();
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		// Act
		api.WithLikeC4Reference(db, configure: null, optional: false);

		// Assert
		var hasEnvCallback = api.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Any();
		await Assert.That(hasEnvCallback).IsTrue();
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_WithConfigure_PropagatesLabelAndKind()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		// Act
		api.WithLikeC4Reference(db, a => a.WithLabel("uses").WithKind("async"), optional: false);

		// Assert
		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().Last();
		await Assert.That(annotation.Label).IsEqualTo("uses");
		await Assert.That(annotation.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_ReturnsOriginalBuilder()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		// Act
		var result = api.WithLikeC4Reference(db, configure: null, optional: false);

		// Assert
		await Assert.That(result).IsEqualTo(api);
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_WithNullConfigure_AnnotationHasNoExtraProperties()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		// Act
		api.WithLikeC4Reference(db, configure: null, optional: false);

		// Assert
		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().Last();
		await Assert.That(annotation.Label).IsNull();
		await Assert.That(annotation.Kind).IsNull();
		await Assert.That(annotation.Technology).IsNull();
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_BothAnnotationsAdded()
	{
		// Arrange
		// Verifies the overload adds both the LikeC4 diagram annotation AND the Aspire runtime reference.
		var appBuilder = CreateAppBuilder();
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		// Act
		api.WithLikeC4Reference(db, a => a.WithLabel("stores data").WithTechnology("SQL"), optional: false);

		// Assert
		var likeC4Annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().LastOrDefault();
		var hasEnvCallback = api.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Any();
		await Assert.That(likeC4Annotation).IsNotNull();
		await Assert.That(likeC4Annotation!.Label).IsEqualTo("stores data");
		await Assert.That(likeC4Annotation.Technology).IsEqualTo("SQL");
		await Assert.That(hasEnvCallback).IsTrue();
	}

	sealed class TestConnectionStringResource(string name) : Resource(name), IResourceWithConnectionString
	{
		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"host=localhost");
	}

	[Test]
	public async Task WithLabel_WhenCalled_SetsLabel()
	{
		// Arrange
		var annotation = CreateAnnotation("queue");

		// Act
		annotation.WithLabel("calls");

		// Assert
		await Assert.That(annotation.Label).IsEqualTo("calls");
	}

	[Test]
	public async Task WithTechnology_WhenCalled_SetsTechnology()
	{
		// Arrange
		var annotation = CreateAnnotation("queue");

		// Act
		annotation.WithTechnology("gRPC");

		// Assert
		await Assert.That(annotation.Technology).IsEqualTo("gRPC");
	}

	[Test]
	public async Task WithDescription_WhenCalled_SetsDescription()
	{
		// Arrange
		var annotation = CreateAnnotation("queue");

		// Act
		annotation.WithDescription("bidirectional streaming");

		// Assert
		await Assert.That(annotation.Description).IsEqualTo("bidirectional streaming");
	}

	[Test]
	public async Task WithKind_WhenCalled_SetsKind()
	{
		// Arrange
		var annotation = CreateAnnotation("queue");

		// Act
		annotation.WithKind("async");

		// Assert
		await Assert.That(annotation.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithKind_SetToNull_KindIsNull()
	{
		// Arrange
		var annotation = CreateAnnotation("target");

		// Act
		annotation.WithKind(null);

		// Assert
		await Assert.That(annotation.Kind).IsNull();
	}

	[Test]
	public async Task WithLikeC4Reference_WithKind_PropagatesKindToAnnotation()
	{
		// Arrange
		var appBuilder = CreateAppBuilder();
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var queue = appBuilder.AddExecutable("queue", "node", ".");

		// Act
		api.WithLikeC4Reference(queue, a => a.WithKind("async"));

		// Assert
		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().Last();
		await Assert.That(annotation.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithTag_WithHashPrefix_NormalizesTag()
	{
		// Arrange
		var annotation = CreateAnnotation("target");

		// Act
		annotation.WithTag("#internal");

		// Assert
		await Assert.That(annotation.Tags).Contains("internal");
		await Assert.That(annotation.Tags).DoesNotContain("#internal");
	}

	[Test]
	public async Task WithTag_WithOnlyHash_Throws()
	{
		// Arrange
		var annotation = CreateAnnotation("target");

		// Act / Assert
		await Assert.That(() => annotation.WithTag("#")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithNavigateTo_SetsNavigateTo()
	{
		// Arrange
		var annotation = CreateAnnotation("target");

		// Act
		annotation.WithNavigateTo("my-view");

		// Assert
		await Assert.That(annotation.NavigateTo).IsEqualTo("my-view");
	}

	static LikeC4RelationshipDetailsAnnotation CreateAnnotation(string targetName) => new(targetName);
}
