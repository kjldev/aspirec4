using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

public sealed class AspireC4DiagramOptionsTests
{
	[Test]
	public async Task Constructor_DefaultOptions_AutoIconsEnabledIsTrue()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.AutoIconsEnabled).IsTrue();
	}

	[Test]
	public async Task Constructor_DefaultOptions_ExcludedResourceTypes_ContainsParameterResource()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.ExcludedResourceTypes).Contains(typeof(ParameterResource));
	}

	[Test]
	public async Task Constructor_DefaultOptions_ExcludedResourceTypes_HasExactlyOneEntry()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.ExcludedResourceTypes.Count).IsEqualTo(1);
	}

	[Test]
	public async Task Constructor_DefaultOptions_CheckLatestImageVersionIsTrue()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.CheckLatestImageVersion).IsTrue();
	}

	static AspireC4DiagramOptions CreateSut() => new();
}
