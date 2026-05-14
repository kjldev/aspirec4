namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4HMRPortCompatibilityTests
{
	[Test]
	public async Task LikeC4HMRPortCompatibility_UsesConfigurableModeForCurrentMinimumVersion()
	{
		// Arrange
		const string version = "1.56.0";

		// Act
		var mode = LikeC4HMRPortCompatibility.Resolve(version);

		// Assert
		await Assert.That(mode).IsEqualTo(LikeC4HMRPortMode.Configurable);
	}

	[Test]
	public async Task LikeC4HMRPortCompatibility_UsesFixedPortForLegacyVersion()
	{
		// Arrange
		const string version = "1.55.0";
		var minimumVersion = new Version(1, 56, 0);

		// Act
		var mode = LikeC4HMRPortCompatibility.Resolve(version, minimumVersion);

		// Assert
		await Assert.That(mode).IsEqualTo(LikeC4HMRPortMode.FixedPort);
	}

	[Test]
	public async Task LikeC4HMRPortCompatibility_UsesConfigurableModeForSupportedVersion()
	{
		// Arrange
		const string version = "v1.56.1-beta.2";
		var minimumVersion = new Version(1, 56, 0);

		// Act
		var mode = LikeC4HMRPortCompatibility.Resolve(version, minimumVersion);

		// Assert
		await Assert.That(mode).IsEqualTo(LikeC4HMRPortMode.Configurable);
	}
}
