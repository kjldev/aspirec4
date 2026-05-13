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

	private static AspireC4DiagramOptions CreateSut() => new();
}
