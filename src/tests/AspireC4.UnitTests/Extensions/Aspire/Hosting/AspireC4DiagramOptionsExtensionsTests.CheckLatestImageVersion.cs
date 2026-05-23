namespace Aspire.Hosting;

public sealed partial class AspireC4DiagramOptionsExtensionsTests
{
	[Test]
	public async Task WithCheckLatestImageVersion_WhenCalledWithFalse_SetsFalse_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var result = sut.WithCheckLatestImageVersion(false);

		// Assert
		await Assert.That(sut.CheckLatestImageVersion).IsFalse();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithCheckLatestImageVersion_WhenCalledWithTrue_SetsTrue_AndReturnsThis()
	{
		// Arrange
		var sut = CreateSut();
		sut.CheckLatestImageVersion = false;

		// Act
		var result = sut.WithCheckLatestImageVersion(true);

		// Assert
		await Assert.That(sut.CheckLatestImageVersion).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}

	[Test]
	public async Task WithCheckLatestImageVersion_DefaultParameter_SetsTrue()
	{
		// Arrange
		var sut = CreateSut();
		sut.CheckLatestImageVersion = false;

		// Act
		var result = sut.WithCheckLatestImageVersion();

		// Assert
		await Assert.That(sut.CheckLatestImageVersion).IsTrue();
		await Assert.That(result).IsSameReferenceAs(sut);
	}
}
