namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4NodeDetailsAnnotationTests
{
	[Test]
	public async Task Constructor_WithLabel_SetsLabel()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service");

		await Assert.That(annotation.Label).IsEqualTo("My Service");
		await Assert.That(annotation.Technology).IsNull();
		await Assert.That(annotation.Description).IsNull();
		await Assert.That(annotation.AutoIconEnabled).IsNull();
	}

	[Test]
	public async Task FluentMethods_SetConfiguredValues()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service")
			.WithTechnology("ASP.NET Core")
			.WithDescription("A web service")
			.WithSummary("A summary")
			.WithIcon("tech:dotnet")
			.WithAutoIcon(false)
			.WithKind("service");

		await Assert.That(annotation.Label).IsEqualTo("My Service");
		await Assert.That(annotation.Technology).IsEqualTo("ASP.NET Core");
		await Assert.That(annotation.Description).IsEqualTo("A web service");
		await Assert.That(annotation.Summary).IsEqualTo("A summary");
		await Assert.That(annotation.Icon).IsEqualTo("tech:dotnet");
		await Assert.That(annotation.AutoIconEnabled).IsFalse();
		await Assert.That(annotation.Kind).IsEqualTo("service");
	}

	[Test]
	public async Task WithLabel_UpdatesLabel()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("original").WithLabel("updated");

		await Assert.That(annotation.Label).IsEqualTo("updated");
	}

	[Test]
	public async Task Constructor_WithEmptyLabel_Throws()
	{
		await Assert.That(() => new LikeC4NodeDetailsAnnotation("")).Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_WithWhiteSpaceLabel_Throws()
	{
		await Assert.That(() => new LikeC4NodeDetailsAnnotation("   ")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithIcon_WithWhiteSpace_Throws()
	{
		await Assert
			.That(() => new LikeC4NodeDetailsAnnotation("My Service").WithIcon("   "))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task WithTag_WithHashPrefix_NormalizesTag()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service").WithTag("#external");

		await Assert.That(annotation.Tags).Contains("external");
		await Assert.That(annotation.Tags).DoesNotContain("#external");
	}

	[Test]
	public async Task WithTag_WithHashAndNonHashVersionsOfSameTag_BothNormalized()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service").WithTag("#external").WithTag("external");

		// Both normalise to "external" — neither entry should carry the '#' prefix.
		await Assert.That(annotation.Tags.All(t => !t.StartsWith('#'))).IsTrue();
	}

	[Test]
	public async Task WithTag_WithHashOnly_Throws()
	{
		await Assert.That(() => new LikeC4NodeDetailsAnnotation("My Service").WithTag("#")).Throws<ArgumentException>();
	}

	[Test]
	public async Task DiagramOptions_DefaultsToAutoIconsEnabled()
	{
		var options = new AspireC4DiagramOptions();

		await Assert.That(options.AutoIconsEnabled).IsTrue();
	}

	[Test]
	public async Task WithLikeC4Details_ActionOverload_ConfiguresAnnotation()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var resourceBuilder = appBuilder.AddExecutable("worker", "dotnet", ".");

		resourceBuilder.WithLikeC4Details(a =>
			a.WithTechnology(".NET").WithDescription("Background job").WithAutoIcon(false)
		);

		var annotation = resourceBuilder.Resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().Last();

		await Assert.That(annotation.Label).IsEqualTo("worker");
		await Assert.That(annotation.Technology).IsEqualTo(".NET");
		await Assert.That(annotation.Description).IsEqualTo("Background job");
		await Assert.That(annotation.AutoIconEnabled).IsFalse();
	}
}

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
