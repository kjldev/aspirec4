using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4RelationshipDetailsAnnotationTests
{
	// ── WithLikeC4Reference<IResourceWithEnvironment> tests ───────────────────
	// The IResourceWithEnvironment overload is disambiguated from the generic IResource overload
	// by passing the optional parameter (unique to this overload) or by using an explicit cast.

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_AddsLikeC4AnnotationWithTargetName()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		api.WithLikeC4Reference(db, configure: null, optional: false);

		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().SingleOrDefault();
		await Assert.That(annotation).IsNotNull();
		await Assert.That(annotation!.TargetName).IsEqualTo("db");
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_AlsoAddsAspireWithReference()
	{
		// Passing optional: false forces the IResourceWithEnvironment overload which calls WithReference.
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		api.WithLikeC4Reference(db, configure: null, optional: false);

		var hasEnvCallback = api.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Any();
		await Assert.That(hasEnvCallback).IsTrue();
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_WithConfigure_PropagatesLabelAndKind()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		api.WithLikeC4Reference(db, a => a.WithLabel("uses").WithKind("async"), optional: false);

		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().Last();
		await Assert.That(annotation.Label).IsEqualTo("uses");
		await Assert.That(annotation.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_ReturnsOriginalBuilder()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		var result = api.WithLikeC4Reference(db, configure: null, optional: false);

		await Assert.That(result).IsEqualTo(api);
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_WithNullConfigure_AnnotationHasNoExtraProperties()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		api.WithLikeC4Reference(db, configure: null, optional: false);

		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().Last();
		await Assert.That(annotation.Label).IsNull();
		await Assert.That(annotation.Kind).IsNull();
		await Assert.That(annotation.Technology).IsNull();
	}

	[Test]
	public async Task WithLikeC4Reference_IResourceWithEnvironment_BothAnnotationsAdded()
	{
		// Verifies the overload adds both the LikeC4 diagram annotation AND the Aspire runtime reference.
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var db = appBuilder.AddResource(new TestConnectionStringResource("db"));

		api.WithLikeC4Reference(db, a => a.WithLabel("stores data").WithTechnology("SQL"), optional: false);

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
	public async Task FluentMethods_SetConfiguredValues()
	{
		var annotation = new LikeC4RelationshipDetailsAnnotation("queue")
			.WithLabel("calls")
			.WithTechnology("gRPC")
			.WithDescription("bidirectional streaming")
			.WithKind("async");

		await Assert.That(annotation.Label).IsEqualTo("calls");
		await Assert.That(annotation.Technology).IsEqualTo("gRPC");
		await Assert.That(annotation.Description).IsEqualTo("bidirectional streaming");
		await Assert.That(annotation.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithKind_SetToNull_KindIsNull()
	{
		var annotation = new LikeC4RelationshipDetailsAnnotation("target").WithKind(null);

		await Assert.That(annotation.Kind).IsNull();
	}

	[Test]
	public async Task WithLikeC4Reference_WithKind_PropagatesKindToAnnotation()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var queue = appBuilder.AddExecutable("queue", "node", ".");

		api.WithLikeC4Reference(queue, a => a.WithKind("async"));

		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().Last();
		await Assert.That(annotation.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithTag_WithHashPrefix_NormalizesTag()
	{
		var annotation = new LikeC4RelationshipDetailsAnnotation("target").WithTag("#internal");

		await Assert.That(annotation.Tags).Contains("internal");
		await Assert.That(annotation.Tags).DoesNotContain("#internal");
	}

	[Test]
	public async Task WithTag_WithOnlyHash_Throws()
	{
		await Assert
			.That(() => new LikeC4RelationshipDetailsAnnotation("target").WithTag("#"))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task WithNavigateTo_SetsNavigateTo()
	{
		var annotation = new LikeC4RelationshipDetailsAnnotation("target").WithNavigateTo("my-view");

		await Assert.That(annotation.NavigateTo).IsEqualTo("my-view");
	}
}
