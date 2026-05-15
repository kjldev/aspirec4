using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;

namespace Aspire.Hosting.AspireC4;

public sealed partial class ModelBuilderTests
{
	[Test]
	public async Task Build_NormaliseMetadata_Default_ReplacesSpaceWithUnderscore()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("Azure SKU", "Standard"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "Azure_SKU" && m.Value == "Standard")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_DuplicateKeysAreNormalisedAndOutput()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(
			new LikeC4NodeDetailsAnnotation("API")
				.WithMetadata("Azure SKU", "Entry 1")
				.WithMetadata("Azure SKU", "Entry 2")
		);

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements.Count).IsEqualTo(1);

		var meta = model.Elements[0].Metadata;
		await Assert.That(meta.Count(m => m.Key == "Azure_SKU")).IsEqualTo(2);
	}

	[Test]
	public async Task Build_NormaliseMetadata_Default_PreservesValidChars()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("valid-key_123", "value"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "valid-key_123")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseLowercase_LowercasesKey()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("Azure SKU", "Standard"));

		// Act
		var model = ModelBuilder.Build(
			[resource],
			normaliseMetadataBehaviour: NormaliseMetadataBehaviour.NormaliseLowercase
		);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "azure_sku" && m.Value == "Standard")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Throw_ThrowsOnInvalidKey()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("Azure SKU", "Standard"));

		// Act
		// Assert
		await Assert
			.That(() => ModelBuilder.Build([resource], normaliseMetadataBehaviour: NormaliseMetadataBehaviour.Throw))
			.Throws<ArgumentException>();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Throw_AcceptsValidKey()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("valid-key_123", "value"));

		// Act
		var model = ModelBuilder.Build([resource], normaliseMetadataBehaviour: NormaliseMetadataBehaviour.Throw);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "valid-key_123")).IsTrue();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Normalise_ThrowsOnNullKey()
	{
		// Arrange
		// Act
		// Assert
		await Assert
			.That(() => new LikeC4NodeDetailsAnnotation("API").WithMetadata(null!, "value"))
			.Throws<ArgumentNullException>();
	}

	[Test]
	public async Task Build_NormaliseMetadata_Default_ReplacesMultipleInvalidChars()
	{
		// Arrange
		var resource = CreateProjectResource("api");
		resource.Annotations.Add(new LikeC4NodeDetailsAnnotation("API").WithMetadata("My Key (v2)!", "value"));

		// Act
		var model = ModelBuilder.Build([resource]);

		var meta = model.Elements[0].Metadata;
		// Assert
		await Assert.That(meta.Any(m => m.Key == "My_Key__v2__")).IsTrue();
	}
}
