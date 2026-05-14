using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
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
			.WithStateTag(KnownResourceStates.Running, "live")
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
		await Assert.That(sut.StateTagMap[KnownResourceStates.Running]).IsEqualTo("live");
		await Assert.That(sut.IconResolvers.Count).IsEqualTo(1);
		await Assert.That(sut.ElementKindSpecs.Count).IsEqualTo(1);
	}
}
