namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4RelationshipDetailsAnnotationTests
{
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
