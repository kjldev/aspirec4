namespace Aspire.Hosting.AspireC4;

public sealed class AspireC4DiagramOptionsExtensionsTests
{
	AspireC4DiagramOptions _options = null!;

	[Before(Test)]
	public void SetUp() => _options = new AspireC4DiagramOptions();

	// ── Scalar setters ────────────────────────────────────────────────────────

	[Test]
	public async Task WithGeneratedViewId_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithGeneratedViewId("custom");

		await Assert.That(_options.GeneratedViewId).IsEqualTo("custom");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithGeneratedViewId_AcceptsNull()
	{
		_options.GeneratedViewId = "something";
		_options.WithGeneratedViewId(null);

		await Assert.That(_options.GeneratedViewId).IsNull();
	}

	[Test]
	public async Task WithDefaultViewId_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithDefaultViewId("home");

		await Assert.That(_options.DefaultViewId).IsEqualTo("home");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithDefaultViewId_AcceptsNull()
	{
		_options.WithDefaultViewId(null);

		await Assert.That(_options.DefaultViewId).IsNull();
	}

	[Test]
	public async Task WithTitle_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithTitle("My Architecture");

		await Assert.That(_options.Title).IsEqualTo("My Architecture");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithTitle_ThrowsOnWhitespace() =>
		await Assert.That(() => _options.WithTitle("   ")).Throws<ArgumentException>();

	[Test]
	public async Task WithOutputDirectory_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithOutputDirectory("./out");

		await Assert.That(_options.OutputDirectory).IsEqualTo("./out");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithOutputDirectory_ThrowsOnEmpty() =>
		await Assert.That(() => _options.WithOutputDirectory("")).Throws<ArgumentException>();

	[Test]
	public async Task WithFileName_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithFileName("custom.gen");

		await Assert.That(_options.FileName).IsEqualTo("custom.gen");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithHMRDisabled_SetsTrueByDefault()
	{
		var result = _options.WithHMRDisabled();

		await Assert.That(_options.DisableHMR).IsTrue();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithHMRDisabled_FalseReEnablesHMR()
	{
		_options.DisableHMR = true;
		_options.WithHMRDisabled(false);

		await Assert.That(_options.DisableHMR).IsFalse();
	}

	[Test]
	public async Task WithContainerImageTag_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithContainerImageTag("1.56");

		await Assert.That(_options.ContainerImageTag).IsEqualTo("1.56");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithContainerImageTag_AcceptsNull()
	{
		_options.ContainerImageTag = "1.0";
		_options.WithContainerImageTag(null);

		await Assert.That(_options.ContainerImageTag).IsNull();
	}

	[Test]
	public async Task WithAutoIcons_EnablesTrueByDefault()
	{
		_options.AutoIconsEnabled = false;
		var result = _options.WithAutoIcons();

		await Assert.That(_options.AutoIconsEnabled).IsTrue();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithAutoIcons_FalseDisablesAutoIcons()
	{
		_options.WithAutoIcons(false);

		await Assert.That(_options.AutoIconsEnabled).IsFalse();
	}

	[Test]
	public async Task WithHideFromDashboard_SetsHideAndDisplayName()
	{
		var result = _options.WithHideFromDashboard("My Diagram");

		await Assert.That(_options.HideFromDashboard).IsTrue();
		await Assert.That(_options.DashboardLinkDisplayName).IsEqualTo("My Diagram");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithHideFromDashboard_UsesDefaultDisplayName()
	{
		_options.WithHideFromDashboard();

		await Assert.That(_options.HideFromDashboard).IsTrue();
		await Assert.That(_options.DashboardLinkDisplayName).IsEqualTo("Architecture Diagram");
	}

	[Test]
	public async Task WithHideFromDashboard_ThrowsOnWhitespaceDisplayName() =>
		await Assert.That(() => _options.WithHideFromDashboard("  ")).Throws<ArgumentException>();

	[Test]
	public async Task WithRelationshipKindSyntax_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax.Bracket);

		await Assert.That(_options.RelationshipKindSyntax).IsEqualTo(LikeC4RelationshipKindSyntax.Bracket);
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithFormatGeneratedFile_SetsTrueByDefault()
	{
		_options.FormatGeneratedFile = false;
		var result = _options.WithFormatGeneratedFile();

		await Assert.That(_options.FormatGeneratedFile).IsTrue();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithFormatGeneratedFile_FalseDisablesFormatting()
	{
		_options.WithFormatGeneratedFile(false);

		await Assert.That(_options.FormatGeneratedFile).IsFalse();
	}

	[Test]
	public async Task WithValidateBeforeStart_SetsTrueByDefault()
	{
		var result = _options.WithValidateBeforeStart();

		await Assert.That(_options.ValidateBeforeStart).IsTrue();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithValidateBeforeStart_FalseDisablesValidation()
	{
		_options.ValidateBeforeStart = true;
		_options.WithValidateBeforeStart(false);

		await Assert.That(_options.ValidateBeforeStart).IsFalse();
	}

	[Test]
	public async Task WithAutoIncludeAspireMetadata_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithAutoIncludeAspireMetadata(AspireMetadataInclusion.None);

		await Assert.That(_options.AutoIncludeAspireMetadata).IsEqualTo(AspireMetadataInclusion.None);
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithNormaliseMetadataBehaviour_SetsProperty_AndReturnsThis()
	{
		var result = _options.WithNormaliseMetadataBehaviour(NormaliseMetadataBehaviour.Throw);

		await Assert.That(_options.NormaliseMetadataBehaviour).IsEqualTo(NormaliseMetadataBehaviour.Throw);
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithoutConfigFileGeneration_SetsGenerateConfigFileFalse()
	{
		var result = _options.WithoutConfigFileGeneration();

		await Assert.That(_options.GenerateConfigFile).IsFalse();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithAspireDashboardLinks_SetsTrueByDefault()
	{
		_options.IncludeAspireDashboardLinks = false;
		var result = _options.WithAspireDashboardLinks();

		await Assert.That(_options.IncludeAspireDashboardLinks).IsTrue();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithAspireDashboardLinks_FalseDisablesLinks()
	{
		_options.WithAspireDashboardLinks(false);

		await Assert.That(_options.IncludeAspireDashboardLinks).IsFalse();
	}

	[Test]
	public async Task WithAspireTokenInDashboardLinks_SetsTrueByDefault()
	{
		var result = _options.WithAspireTokenInDashboardLinks();

		await Assert.That(_options.IncludeAspireTokenInDashboardLinks).IsTrue();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithAspireTokenInDashboardLinks_FalseDisablesToken()
	{
		_options.IncludeAspireTokenInDashboardLinks = true;
		_options.WithAspireTokenInDashboardLinks(false);

		await Assert.That(_options.IncludeAspireTokenInDashboardLinks).IsFalse();
	}

	[Test]
	public async Task WithDefaultStateStyles_SetsTrueByDefault()
	{
		_options.IncludeDefaultStateStyles = false;
		var result = _options.WithDefaultStateStyles();

		await Assert.That(_options.IncludeDefaultStateStyles).IsTrue();
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithDefaultStateStyles_FalseDisablesStyles()
	{
		_options.WithDefaultStateStyles(false);

		await Assert.That(_options.IncludeDefaultStateStyles).IsFalse();
	}

	// ── Collection mutators ───────────────────────────────────────────────────

	[Test]
	public async Task WithElementKindSpec_AddsToCollection_AndReturnsThis()
	{
		var spec = new LikeC4ElementKindSpec("queue");
		var result = _options.WithElementKindSpec(spec);

		await Assert.That(_options.ElementKindSpecs).Contains(spec);
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithElementKindSpec_Null_Throws() =>
		await Assert.That(() => _options.WithElementKindSpec(null!)).Throws<ArgumentNullException>();

	[Test]
	public async Task WithElementKindSpec_MultipleCallsAccumulateSpecs()
	{
		var s1 = new LikeC4ElementKindSpec("queue");
		var s2 = new LikeC4ElementKindSpec("topic");

		_options.WithElementKindSpec(s1).WithElementKindSpec(s2);

		await Assert.That(_options.ElementKindSpecs.Count).IsEqualTo(2);
	}

	[Test]
	public async Task WithStateTag_UpdatesMapEntry_AndReturnsThis()
	{
		var result = _options.WithStateTag(LikeC4ResourceState.Running, "my-running");

		await Assert.That(_options.StateTagMap[LikeC4ResourceState.Running]).IsEqualTo("my-running");
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithStateTag_NullTagSuppressesTagForState()
	{
		_options.WithStateTag(LikeC4ResourceState.Running, null);

		await Assert.That(_options.StateTagMap[LikeC4ResourceState.Running]).IsNull();
	}

	[Test]
	public async Task WithIconResolver_AddsToCollection_AndReturnsThis()
	{
		static string? Resolver(LikeC4IconResolverContext _) => "tech:dotnet";
		var result = _options.WithIconResolver(Resolver);

		await Assert.That(_options.IconResolvers).Contains(Resolver);
		await Assert.That(result).IsSameReferenceAs(_options);
	}

	[Test]
	public async Task WithIconResolver_Null_Throws() =>
		await Assert.That(() => _options.WithIconResolver(null!)).Throws<ArgumentNullException>();

	[Test]
	public async Task WithIconResolver_MultipleCallsAccumulateResolvers()
	{
		_options.WithIconResolver(_ => "tech:dotnet").WithIconResolver(_ => null);

		await Assert.That(_options.IconResolvers.Count).IsEqualTo(2);
	}

	// ── Chaining ──────────────────────────────────────────────────────────────

	[Test]
	public async Task FluentChain_AllMethodsReturnSameInstance()
	{
		var result = _options
			.WithTitle("Test App")
			.WithOutputDirectory("./c4out")
			.WithFileName("arch")
			.WithGeneratedViewId("main")
			.WithDefaultViewId("main")
			.WithHMRDisabled()
			.WithContainerImageTag("1.56")
			.WithAutoIcons(false)
			.WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax.Bracket)
			.WithFormatGeneratedFile(false)
			.WithValidateBeforeStart()
			.WithAutoIncludeAspireMetadata(AspireMetadataInclusion.None)
			.WithNormaliseMetadataBehaviour(NormaliseMetadataBehaviour.Throw)
			.WithoutConfigFileGeneration()
			.WithAspireDashboardLinks(false)
			.WithAspireTokenInDashboardLinks(false)
			.WithDefaultStateStyles(false)
			.WithStateTag(LikeC4ResourceState.Running, "live")
			.WithIconResolver(_ => "tech:dotnet")
			.WithElementKindSpec(new LikeC4ElementKindSpec("cache"));

		await Assert.That(result).IsSameReferenceAs(_options);
		await Assert.That(_options.Title).IsEqualTo("Test App");
		await Assert.That(_options.OutputDirectory).IsEqualTo("./c4out");
		await Assert.That(_options.FileName).IsEqualTo("arch");
		await Assert.That(_options.GeneratedViewId).IsEqualTo("main");
		await Assert.That(_options.DefaultViewId).IsEqualTo("main");
		await Assert.That(_options.DisableHMR).IsTrue();
		await Assert.That(_options.ContainerImageTag).IsEqualTo("1.56");
		await Assert.That(_options.AutoIconsEnabled).IsFalse();
		await Assert.That(_options.RelationshipKindSyntax).IsEqualTo(LikeC4RelationshipKindSyntax.Bracket);
		await Assert.That(_options.FormatGeneratedFile).IsFalse();
		await Assert.That(_options.ValidateBeforeStart).IsTrue();
		await Assert.That(_options.AutoIncludeAspireMetadata).IsEqualTo(AspireMetadataInclusion.None);
		await Assert.That(_options.NormaliseMetadataBehaviour).IsEqualTo(NormaliseMetadataBehaviour.Throw);
		await Assert.That(_options.GenerateConfigFile).IsFalse();
		await Assert.That(_options.IncludeAspireDashboardLinks).IsFalse();
		await Assert.That(_options.IncludeAspireTokenInDashboardLinks).IsFalse();
		await Assert.That(_options.IncludeDefaultStateStyles).IsFalse();
		await Assert.That(_options.StateTagMap[LikeC4ResourceState.Running]).IsEqualTo("live");
		await Assert.That(_options.IconResolvers.Count).IsEqualTo(1);
		await Assert.That(_options.ElementKindSpecs.Count).IsEqualTo(1);
	}
}
