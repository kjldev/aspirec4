namespace Aspire.Hosting.AspireC4;

public sealed class AspireC4DiagramOptionsExtensionsTests
{
	// ── Scalar setters ────────────────────────────────────────────────────────

	[Test]
	public async Task WithGeneratedViewId_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithGeneratedViewId("custom");

		// Assert
		await Assert.That(sut.GeneratedViewId).IsEqualTo("custom");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithGeneratedViewId_AcceptsNull()
	{
		// Arrange
		var sut = CreateSut();
		sut.GeneratedViewId = "something";

		// Act
		sut.WithGeneratedViewId(null);

		// Assert
		await Assert.That(sut.GeneratedViewId).IsNull();
	}

	[Test]
	public async Task WithDefaultViewId_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithDefaultViewId("home");

		// Assert
		await Assert.That(sut.DefaultViewId).IsEqualTo("home");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithDefaultViewId_AcceptsNull()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithDefaultViewId(null);

		// Assert
		await Assert.That(sut.DefaultViewId).IsNull();
	}

	[Test]
	public async Task WithTitle_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithTitle("My Architecture");

		// Assert
		await Assert.That(sut.Title).IsEqualTo("My Architecture");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithTitle_ThrowsOnWhitespace()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithTitle("   ")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithOutputDirectory_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithOutputDirectory("./out");

		// Assert
		await Assert.That(sut.OutputDirectory).IsEqualTo("./out");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithOutputDirectory_ThrowsOnEmpty()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithOutputDirectory("")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithFileName_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithFileName("custom.gen");

		// Assert
		await Assert.That(sut.FileName).IsEqualTo("custom.gen");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithHMRDisabled_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithHMRDisabled();

		// Assert
		await Assert.That(sut.DisableHMR).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithHMRDisabled_FalseReEnablesHMR()
	{
		// Arrange
		var sut = CreateSut();
		sut.DisableHMR = true;

		// Act
		sut.WithHMRDisabled(false);

		// Assert
		await Assert.That(sut.DisableHMR).IsFalse();
	}

	[Test]
	public async Task WithContainerImageTag_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithContainerImageTag("1.56");

		// Assert
		await Assert.That(sut.ContainerImageTag).IsEqualTo("1.56");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithContainerImageTag_AcceptsNull()
	{
		// Arrange
		var sut = CreateSut();
		sut.ContainerImageTag = "1.0";

		// Act
		sut.WithContainerImageTag(null);

		// Assert
		await Assert.That(sut.ContainerImageTag).IsNull();
	}

	[Test]
	public async Task WithAutoIcons_EnablesTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.AutoIconsEnabled = false;

		// Act
		var result = sut.WithAutoIcons();

		// Assert
		await Assert.That(sut.AutoIconsEnabled).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAutoIcons_FalseDisablesAutoIcons()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithAutoIcons(false);

		// Assert
		await Assert.That(sut.AutoIconsEnabled).IsFalse();
	}

	[Test]
	public async Task WithHideFromDashboard_SetsHideAndDisplayName()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithHideFromDashboard("My Diagram");

		// Assert
		await Assert.That(sut.HideFromDashboard).IsTrue();
		await Assert.That(sut.DashboardLinkDisplayName).IsEqualTo("My Diagram");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithHideFromDashboard_UsesDefaultDisplayName()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithHideFromDashboard();

		// Assert
		await Assert.That(sut.HideFromDashboard).IsTrue();
		await Assert.That(sut.DashboardLinkDisplayName).IsEqualTo("Architecture Diagram");
	}

	[Test]
	public async Task WithHideFromDashboard_ThrowsOnWhitespaceDisplayName()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithHideFromDashboard("  ")).Throws<ArgumentException>();
	}

	[Test]
	public async Task WithRelationshipKindSyntax_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithRelationshipKindSyntax(LikeC4RelationshipKindSyntax.Bracket);

		// Assert
		await Assert.That(sut.RelationshipKindSyntax).IsEqualTo(LikeC4RelationshipKindSyntax.Bracket);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithFormatGeneratedFile_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.FormatGeneratedFile = false;

		// Act
		var result = sut.WithFormatGeneratedFile();

		// Assert
		await Assert.That(sut.FormatGeneratedFile).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithFormatGeneratedFile_FalseDisablesFormatting()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithFormatGeneratedFile(false);

		// Assert
		await Assert.That(sut.FormatGeneratedFile).IsFalse();
	}

	[Test]
	public async Task WithValidateBeforeStart_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithValidateBeforeStart();

		// Assert
		await Assert.That(sut.ValidateBeforeStart).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithValidateBeforeStart_FalseDisablesValidation()
	{
		// Arrange
		var sut = CreateSut();
		sut.ValidateBeforeStart = true;

		// Act
		sut.WithValidateBeforeStart(false);

		// Assert
		await Assert.That(sut.ValidateBeforeStart).IsFalse();
	}

	[Test]
	public async Task WithAutoIncludeAspireMetadata_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAutoIncludeAspireMetadata(AspireMetadataInclusion.None);

		// Assert
		await Assert.That(sut.AutoIncludeAspireMetadata).IsEqualTo(AspireMetadataInclusion.None);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithNormaliseMetadataBehaviour_SetsProperty_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithNormaliseMetadataBehaviour(NormaliseMetadataBehaviour.Throw);

		// Assert
		await Assert.That(sut.NormaliseMetadataBehaviour).IsEqualTo(NormaliseMetadataBehaviour.Throw);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithoutConfigFileGeneration_SetsGenerateConfigFileFalse()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithoutConfigFileGeneration();

		// Assert
		await Assert.That(sut.GenerateConfigFile).IsFalse();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAspireDashboardLinks_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.IncludeAspireDashboardLinks = false;

		// Act
		var result = sut.WithAspireDashboardLinks();

		// Assert
		await Assert.That(sut.IncludeAspireDashboardLinks).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAspireDashboardLinks_FalseDisablesLinks()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithAspireDashboardLinks(false);

		// Assert
		await Assert.That(sut.IncludeAspireDashboardLinks).IsFalse();
	}

	[Test]
	public async Task WithAspireTokenInDashboardLinks_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAspireTokenInDashboardLinks();

		// Assert
		await Assert.That(sut.IncludeAspireTokenInDashboardLinks).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAspireTokenInDashboardLinks_FalseDisablesToken()
	{
		// Arrange
		var sut = CreateSut();
		sut.IncludeAspireTokenInDashboardLinks = true;

		// Act
		sut.WithAspireTokenInDashboardLinks(false);

		// Assert
		await Assert.That(sut.IncludeAspireTokenInDashboardLinks).IsFalse();
	}

	[Test]
	public async Task WithDefaultStateStyles_SetsTrueByDefault()
	{
		// Arrange
		var sut = CreateSut();
		sut.IncludeDefaultStateStyles = false;

		// Act
		var result = sut.WithDefaultStateStyles();

		// Assert
		await Assert.That(sut.IncludeDefaultStateStyles).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithDefaultStateStyles_FalseDisablesStyles()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithDefaultStateStyles(false);

		// Assert
		await Assert.That(sut.IncludeDefaultStateStyles).IsFalse();
	}

	// ── Collection mutators ───────────────────────────────────────────────────

	[Test]
	public async Task WithElementKindSpec_AddsToCollection_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		var spec = new LikeC4ElementKindSpec("queue");

		// Act
		var result = sut.WithElementKindSpec(spec);

		// Assert
		await Assert.That(sut.ElementKindSpecs).Contains(spec);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithElementKindSpec_Null_Throws()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithElementKindSpec(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task WithElementKindSpec_MultipleCallsAccumulateSpecs()
	{
		// Arrange
		var sut = CreateSut();
		var s1 = new LikeC4ElementKindSpec("queue");
		var s2 = new LikeC4ElementKindSpec("topic");

		// Act
		sut.WithElementKindSpec(s1).WithElementKindSpec(s2);

		// Assert
		await Assert.That(sut.ElementKindSpecs.Count).IsEqualTo(2);
	}

	[Test]
	public async Task WithStateTag_UpdatesMapEntry_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithStateTag(LikeC4ResourceState.Running, "my-running");

		// Assert
		await Assert.That(sut.StateTagMap[LikeC4ResourceState.Running]).IsEqualTo("my-running");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithStateTag_NullTagSuppressesTagForState()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithStateTag(LikeC4ResourceState.Running, null);

		// Assert
		await Assert.That(sut.StateTagMap[LikeC4ResourceState.Running]).IsNull();
	}

	[Test]
	public async Task WithIconResolver_AddsToCollection_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		static string? Resolver(LikeC4IconResolverContext _) => "tech:dotnet";

		// Act
		var result = sut.WithIconResolver(Resolver);

		// Assert
		await Assert.That(sut.IconResolvers).Contains(Resolver);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithIconResolver_Null_Throws()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert.That(() => sut.WithIconResolver(null!)).Throws<ArgumentNullException>();
	}

	[Test]
	public async Task WithIconResolver_MultipleCallsAccumulateResolvers()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithIconResolver(_ => "tech:dotnet").WithIconResolver(_ => null);

		// Assert
		await Assert.That(sut.IconResolvers.Count).IsEqualTo(2);
	}

	// ── Chaining ──────────────────────────────────────────────────────────────

	[Test]
	public async Task FluentChain_WhenAllMethodsCalled_ReturnsSameInstance()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithTitle("Test App")
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

		// Assert
		await Assert.That(result).IsSameReferenceAs(sut);
		await Assert.That(sut.Title).IsEqualTo("Test App");
		await Assert.That(sut.OutputDirectory).IsEqualTo("./c4out");
		await Assert.That(sut.FileName).IsEqualTo("arch");
		await Assert.That(sut.GeneratedViewId).IsEqualTo("main");
		await Assert.That(sut.DefaultViewId).IsEqualTo("main");
		await Assert.That(sut.DisableHMR).IsTrue();
		await Assert.That(sut.ContainerImageTag).IsEqualTo("1.56");
		await Assert.That(sut.AutoIconsEnabled).IsFalse();
		await Assert.That(sut.RelationshipKindSyntax).IsEqualTo(LikeC4RelationshipKindSyntax.Bracket);
		await Assert.That(sut.FormatGeneratedFile).IsFalse();
		await Assert.That(sut.ValidateBeforeStart).IsTrue();
		await Assert.That(sut.AutoIncludeAspireMetadata).IsEqualTo(AspireMetadataInclusion.None);
		await Assert.That(sut.NormaliseMetadataBehaviour).IsEqualTo(NormaliseMetadataBehaviour.Throw);
		await Assert.That(sut.GenerateConfigFile).IsFalse();
		await Assert.That(sut.IncludeAspireDashboardLinks).IsFalse();
		await Assert.That(sut.IncludeAspireTokenInDashboardLinks).IsFalse();
		await Assert.That(sut.IncludeDefaultStateStyles).IsFalse();
		await Assert.That(sut.StateTagMap[LikeC4ResourceState.Running]).IsEqualTo("live");
		await Assert.That(sut.IconResolvers.Count).IsEqualTo(1);
		await Assert.That(sut.ElementKindSpecs.Count).IsEqualTo(1);
	}

	private static AspireC4DiagramOptions CreateSut() => new();
}
