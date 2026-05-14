using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithStrictMode_SetsMode_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithStrictMode(AspireC4StrictMode.Tags);

		// Assert
		await Assert.That(sut.Strict.Mode).IsEqualTo(AspireC4StrictMode.Tags);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAllowedTag_AddsBareTag_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAllowedTag("external");

		// Assert
		await Assert.That(sut.Strict.Tags).Contains("external");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAllowedTag_NormalizesHashPrefix()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithAllowedTag("#external");

		// Assert — stored without the # prefix
		await Assert.That(sut.Strict.Tags).Contains("external");
		await Assert.That(sut.Strict.Tags).DoesNotContain("#external");
	}

	[Test]
	public async Task WithAllowedRelationshipKind_AddsKind_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAllowedRelationshipKind("async");

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).Contains("async");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAllowedGroup_AddsGroup_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAllowedGroup("Frontend");

		// Assert
		await Assert.That(sut.Strict.Groups).Contains("Frontend");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAllowedMetadataKey_AddsKey_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithAllowedMetadataKey("custom-key");

		// Assert
		await Assert.That(sut.Strict.MetadataKeys).Contains("custom-key");
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithAllowedTag_CanAddMultipleTags()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithAllowedTag("external").WithAllowedTag("internal").WithAllowedTag("deprecated");

		// Assert
		await Assert.That(sut.Strict.Tags.Count).IsEqualTo(3);
	}
}
