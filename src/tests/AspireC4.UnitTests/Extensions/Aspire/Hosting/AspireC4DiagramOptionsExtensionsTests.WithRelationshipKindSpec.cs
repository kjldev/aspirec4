using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_AddsSpec_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithRelationshipKindSpec("async");

		// Assert
		await Assert.That(sut.RelationshipKindSpecs.Any(s => s.Name == "async")).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_WithTechnology_SetsSpecTechnology()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		sut.WithRelationshipKindSpec("grpc", technology: "gRPC");

		// Assert
		await Assert.That(sut.RelationshipKindSpecs.Single(s => s.Name == "grpc").Technology).IsEqualTo("gRPC");
	}

	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_StrictNull_ModeNotSet_DoesNotAddToAllowedList()
	{
		// Arrange
		var sut = CreateSut();

		// Act — strict=null (default), RelationshipKinds mode is NOT enabled
		sut.WithRelationshipKindSpec("async");

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).DoesNotContain("async");
	}

	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_StrictNull_ModeSet_AddsToAllowedList()
	{
		// Arrange
		var sut = CreateSut();
		sut.WithStrictMode(AspireC4StrictMode.RelationshipKinds);

		// Act — strict=null (default), RelationshipKinds mode IS enabled
		sut.WithRelationshipKindSpec("async");

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).Contains("async");
	}

	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_StrictTrue_ModeNotSet_AddsToAllowedList()
	{
		// Arrange
		var sut = CreateSut();

		// Act — strict=true forces addition regardless of current mode
		sut.WithRelationshipKindSpec("async", strict: true);

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).Contains("async");
	}

	[Test]
	public async Task WithRelationshipKindSpec_StringOverload_StrictFalse_ModeSet_DoesNotAddToAllowedList()
	{
		// Arrange
		var sut = CreateSut();
		sut.WithStrictMode(AspireC4StrictMode.RelationshipKinds);

		// Act — strict=false suppresses addition even though mode is enabled
		sut.WithRelationshipKindSpec("async", strict: false);

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).DoesNotContain("async");
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_AddsSpec_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		var spec = new LikeC4RelationshipKindSpec("async");

		// Act
		var result = sut.WithRelationshipKindSpec(spec);

		// Assert
		await Assert.That(sut.RelationshipKindSpecs).Contains(spec);
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_NullSpec_Throws()
	{
		// Arrange
		var sut = CreateSut();

		// Act / Assert
		await Assert
			.That(() => sut.WithRelationshipKindSpec((LikeC4RelationshipKindSpec)null!))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_StrictNull_ModeNotSet_DoesNotAddToAllowedList()
	{
		// Arrange
		var sut = CreateSut();
		var spec = new LikeC4RelationshipKindSpec("async");

		// Act — strict=null (default), RelationshipKinds mode is NOT enabled
		sut.WithRelationshipKindSpec(spec);

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).DoesNotContain("async");
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_StrictNull_ModeSet_AddsToAllowedList()
	{
		// Arrange
		var sut = CreateSut();
		sut.WithStrictMode(AspireC4StrictMode.RelationshipKinds);
		var spec = new LikeC4RelationshipKindSpec("async");

		// Act — strict=null (default), RelationshipKinds mode IS enabled
		sut.WithRelationshipKindSpec(spec);

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).Contains("async");
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_StrictTrue_ModeNotSet_AddsToAllowedList()
	{
		// Arrange
		var sut = CreateSut();
		var spec = new LikeC4RelationshipKindSpec("async");

		// Act — strict=true forces addition regardless of current mode
		sut.WithRelationshipKindSpec(spec, strict: true);

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).Contains("async");
	}

	[Test]
	public async Task WithRelationshipKindSpec_SpecOverload_StrictFalse_ModeSet_DoesNotAddToAllowedList()
	{
		// Arrange
		var sut = CreateSut();
		sut.WithStrictMode(AspireC4StrictMode.RelationshipKinds);
		var spec = new LikeC4RelationshipKindSpec("async");

		// Act — strict=false suppresses addition even though mode is enabled
		sut.WithRelationshipKindSpec(spec, strict: false);

		// Assert
		await Assert.That(sut.Strict.RelationshipKinds).DoesNotContain("async");
	}
}
