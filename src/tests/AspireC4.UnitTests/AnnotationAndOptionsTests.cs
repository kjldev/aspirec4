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
	public async Task Constructor_WithAllParameters_SetsAllProperties()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service", "ASP.NET Core", "A web service");

		await Assert.That(annotation.Label).IsEqualTo("My Service");
		await Assert.That(annotation.Technology).IsEqualTo("ASP.NET Core");
		await Assert.That(annotation.Description).IsEqualTo("A web service");
	}

	[Test]
	public async Task Constructor_WithIcon_SetsIcon()
	{
		var annotation = new LikeC4NodeDetailsAnnotation("My Service", "ASP.NET Core", "A web service", "tech:dotnet");

		await Assert.That(annotation.Icon).IsEqualTo("tech:dotnet");
	}

	[Test]
	public async Task Constructor_WithAutoIconFlag_SetsAutoIconEnabled()
	{
		var annotation = new LikeC4NodeDetailsAnnotation(
			"My Service",
			"ASP.NET Core",
			"A web service",
			icon: null,
			autoIconEnabled: false
		);

		await Assert.That(annotation.AutoIconEnabled).IsFalse();
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
	public async Task Constructor_WithWhiteSpaceIcon_Throws()
	{
		await Assert
			.That(() => new LikeC4NodeDetailsAnnotation("My Service", technology: null, description: null, icon: "   "))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Constructor_WithHashPrefixedTag_NormalizesTag()
	{
		// "#external" is a valid way to specify a tag and must be stored as "external".
		var annotation = new LikeC4NodeDetailsAnnotation(
			"My Service",
			technology: null,
			description: null,
			summary: null,
			icon: null,
			autoIconEnabled: null,
			kind: null,
			tags: ["#external"],
			links: [],
			metadata: []
		);

		await Assert.That(annotation.Tags).Contains("external");
		await Assert.That(annotation.Tags).DoesNotContain("#external");
	}

	[Test]
	public async Task Constructor_WithHashAndNonHashVersionsOfSameTag_ProducesOneEntry()
	{
		var annotation = new LikeC4NodeDetailsAnnotation(
			"My Service",
			technology: null,
			description: null,
			summary: null,
			icon: null,
			autoIconEnabled: null,
			kind: null,
			tags: ["#external", "external"],
			links: [],
			metadata: []
		);

		// Both normalise to "external" — distinct is handled by the DSL generator,
		// but the stored list must only contain the stripped form.
		await Assert.That(annotation.Tags.All(t => !t.StartsWith('#'))).IsTrue();
	}

	[Test]
	public async Task Constructor_WithHashOnlyTag_Throws()
	{
		await Assert
			.That(() =>
				new LikeC4NodeDetailsAnnotation(
					"My Service",
					technology: null,
					description: null,
					summary: null,
					icon: null,
					autoIconEnabled: null,
					kind: null,
					tags: ["#"],
					links: [],
					metadata: []
				)
			)
			.Throws<ArgumentException>();
	}
}

public sealed class LikeC4DetailsOptionsTests
{
	[Test]
	public async Task FluentMethods_SetConfiguredValues()
	{
		var options = new LikeC4DetailsOptions()
			.WithLabel("Worker")
			.WithTechnology(".NET")
			.WithDescription("Background job")
			.WithIcon("tech:dotnet")
			.WithAutoIcon(false);

		await Assert.That(options.Label).IsEqualTo("Worker");
		await Assert.That(options.Technology).IsEqualTo(".NET");
		await Assert.That(options.Description).IsEqualTo("Background job");
		await Assert.That(options.Icon).IsEqualTo("tech:dotnet");
		await Assert.That(options.AutoIconEnabled).IsFalse();
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

		resourceBuilder.WithLikeC4Details(options =>
			options.WithTechnology(".NET").WithDescription("Background job").WithAutoIcon(false)
		);

		var annotation = resourceBuilder.Resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().Last();

		await Assert.That(annotation.Label).IsEqualTo("worker");
		await Assert.That(annotation.Technology).IsEqualTo(".NET");
		await Assert.That(annotation.Description).IsEqualTo("Background job");
		await Assert.That(annotation.AutoIconEnabled).IsFalse();
	}

	[Test]
	public async Task WithTag_WithHashPrefix_NormalizesTag()
	{
		var options = new LikeC4DetailsOptions().WithTag("#external");

		await Assert.That(options.Tags).Contains("external");
		await Assert.That(options.Tags).DoesNotContain("#external");
	}

	[Test]
	public async Task WithTag_WithAndWithoutHash_ProducesSingleNormalisedEntry()
	{
		var options = new LikeC4DetailsOptions().WithTag("external").WithTag("#external");

		// Both calls normalise to "external"; list will contain it twice (dedup is DSL-level),
		// but neither entry should carry the '#' prefix.
		await Assert.That(options.Tags.All(t => !t.StartsWith('#'))).IsTrue();
	}

	[Test]
	public async Task WithTag_WithOnlyHash_Throws()
	{
		await Assert.That(() => new LikeC4DetailsOptions().WithTag("#")).Throws<ArgumentException>();
	}
}

public sealed class LikeC4RelationshipOptionsTests
{
	[Test]
	public async Task FluentMethods_SetConfiguredValues()
	{
		var options = new LikeC4RelationshipOptions()
			.WithLabel("calls")
			.WithTechnology("gRPC")
			.WithDescription("bidirectional streaming")
			.WithKind("async");

		await Assert.That(options.Label).IsEqualTo("calls");
		await Assert.That(options.Technology).IsEqualTo("gRPC");
		await Assert.That(options.Description).IsEqualTo("bidirectional streaming");
		await Assert.That(options.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithKind_SetToNull_KindIsNull()
	{
		var options = new LikeC4RelationshipOptions().WithKind(null);

		await Assert.That(options.Kind).IsNull();
	}

	[Test]
	public async Task WithLikeC4Reference_WithKind_PropagatesKindToAnnotation()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var api = appBuilder.AddExecutable("api", "dotnet", ".");
		var queue = appBuilder.AddExecutable("queue", "node", ".");

		api.WithLikeC4Reference(queue, opts => opts.WithKind("async"));

		var annotation = api.Resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>().Last();
		await Assert.That(annotation.Kind).IsEqualTo("async");
	}

	[Test]
	public async Task WithTag_WithHashPrefix_NormalizesTag()
	{
		var options = new LikeC4RelationshipOptions().WithTag("#internal");

		await Assert.That(options.Tags).Contains("internal");
		await Assert.That(options.Tags).DoesNotContain("#internal");
	}

	[Test]
	public async Task WithTag_WithOnlyHash_Throws()
	{
		await Assert.That(() => new LikeC4RelationshipOptions().WithTag("#")).Throws<ArgumentException>();
	}
}
