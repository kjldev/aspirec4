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
	public async Task Constructor_DefaultOptions_StrictModeIsNone()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.Strict.Mode).IsEqualTo(AspireC4StrictMode.None);
	}

	[Test]
	public async Task Constructor_DefaultOptions_StrictAllowedListsAreEmpty()
	{
		// Arrange

		// Act
		var options = CreateSut();

		// Assert
		await Assert.That(options.Strict.Tags).IsEmpty();
		await Assert.That(options.Strict.RelationshipKinds).IsEmpty();
		await Assert.That(options.Strict.Groups).IsEmpty();
		await Assert.That(options.Strict.MetadataKeys).IsEmpty();
	}

	static AspireC4DiagramOptions CreateSut() => new();
}
