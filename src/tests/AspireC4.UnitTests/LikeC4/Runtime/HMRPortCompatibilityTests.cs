namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

public sealed class HMRPortCompatibilityTests
{
	[Test]
	public async Task LikeC4HMRPortCompatibility_UsesConfigurableModeForCurrentMinimumVersion()
	{
		// Arrange
		const string version = "100.57.0";

		// Act
		var mode = HMRPortCompatibility.Resolve(version);

		// Assert
		await Assert.That(mode).IsEqualTo(HMRPortMode.Configurable);
	}

	[Test]
	public async Task LikeC4HMRPortCompatibility_UsesFixedPortForLegacyVersion()
	{
		// Arrange
		const string version = "1.55.0";
		var minimumVersion = new Version(1, 56, 0);

		// Act
		var mode = HMRPortCompatibility.Resolve(version, minimumVersion);

		// Assert
		await Assert.That(mode).IsEqualTo(HMRPortMode.FixedPort);
	}

	[Test]
	public async Task LikeC4HMRPortCompatibility_UsesConfigurableModeForSupportedVersion()
	{
		// Arrange
		const string version = "v1.56.1-beta.2";
		var minimumVersion = new Version(1, 56, 0);

		// Act
		var mode = HMRPortCompatibility.Resolve(version, minimumVersion);

		// Assert
		await Assert.That(mode).IsEqualTo(HMRPortMode.Configurable);
	}
}
