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
	[MatrixDataSource]
	public async Task LikeC4HMRPortCompatibility_UsesConfigurableModeForSupportedVersion(
		[MatrixMethod<HMRPortCompatibilityTests>(nameof(VersionPrefixes))]
		string versionPrefix,
		[MatrixMethod<HMRPortCompatibilityTests>(nameof(Majors))]
		string major,
		[MatrixMethod<HMRPortCompatibilityTests>(nameof(Minors))]
		string minor,
		[MatrixMethod<HMRPortCompatibilityTests>(nameof(Builds))]
		string build,
		[MatrixMethod<HMRPortCompatibilityTests>(nameof(PrereleaseSuffixes))]
		string prereleaseSuffix)
	{
		// Arrange
		var version = $"{versionPrefix}{major}.{minor}.{build}{prereleaseSuffix}";
		var minimumVersion = new Version(1, 57, 0);

		// Act
		var mode = HMRPortCompatibility.Resolve(version, minimumVersion);

		// Assert
		await Assert.That(mode).IsEqualTo(HMRPortMode.Configurable);

	}

	IEnumerable<string> VersionPrefixes()
	{
		yield return "v";
		yield return "";
	}

	IEnumerable<string> Majors()
	{
		yield return "1";
		yield return "2";
		yield return "100";
	}

	IEnumerable<string> Minors()
	{
		yield return "57";
		yield return "100";
		yield return "2000";
	}

	IEnumerable<string> Builds()
	{
		yield return "0";
		yield return "1";
		yield return "2";
	}

	IEnumerable<string> PrereleaseSuffixes()
	{
		yield return "";
		yield return "-beta.1";
		yield return "-prerelease.2";
	}
}
