using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithExcludedResourceType_AddsTypeToSet_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		sut.ExcludedResourceTypes.Clear();

		// Act
		var result = sut.WithExcludedResourceType<ContainerResource>();

		// Assert
		await Assert.That(sut.ExcludedResourceTypes).Contains(typeof(ContainerResource));
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithExcludedResourceType_WhenTypeAlreadyPresent_IsIdempotent()
	{
		// Arrange
		var sut = CreateSut();
		sut.ExcludedResourceTypes.Clear();
		sut.ExcludedResourceTypes.Add(typeof(ContainerResource));

		// Act
		sut.WithExcludedResourceType<ContainerResource>();

		// Assert
		await Assert.That(sut.ExcludedResourceTypes.Count).IsEqualTo(1);
	}

	[Test]
	public async Task WithExcludedResourceType_MultipleCallsAccumulateTypes()
	{
		// Arrange
		var sut = CreateSut();
		sut.ExcludedResourceTypes.Clear();

		// Act
		sut.WithExcludedResourceType<ContainerResource>().WithExcludedResourceType<ProjectResource>();

		// Assert
		await Assert.That(sut.ExcludedResourceTypes.Count).IsEqualTo(2);
	}

	[Test]
	public async Task WithoutExcludedResourceType_RemovesTypeFromSet_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithoutExcludedResourceType<ParameterResource>();

		// Assert
		await Assert.That(sut.ExcludedResourceTypes).DoesNotContain(typeof(ParameterResource));
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithoutExcludedResourceType_WhenTypeNotPresent_IsNoOp()
	{
		// Arrange
		var sut = CreateSut();
		sut.ExcludedResourceTypes.Clear();

		// Act
		sut.WithoutExcludedResourceType<ContainerResource>();

		// Assert
		await Assert.That(sut.ExcludedResourceTypes).IsEmpty();
	}
}
