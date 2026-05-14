namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for the local CLI command builder logic.
/// </summary>
public sealed partial class AspireC4BuilderTests
{
	[Test]
	public async Task BuildLikeC4CLIPrefix_Npx_ReturnsNpxWithLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CLIPrefix(LocalCLIRuntime.Npx);

		// Assert
		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(prefix).IsEquivalentTo(["likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CLIPrefix_Pnpm_ReturnsPnpmExecLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CLIPrefix(LocalCLIRuntime.Pnpm);

		// Assert
		await Assert.That(command).IsEqualTo("pnpm");
		await Assert.That(prefix).IsEquivalentTo(["exec", "likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CLIPrefix_Bun_ReturnsBunxWithLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CLIPrefix(LocalCLIRuntime.Bun);

		// Assert
		await Assert.That(command).IsEqualTo("bunx");
		await Assert.That(prefix).IsEquivalentTo(["likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CLIPrefix_Yarn_ReturnsYarnDlxLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CLIPrefix(LocalCLIRuntime.Yarn);

		// Assert
		await Assert.That(command).IsEqualTo("yarn");
		await Assert.That(prefix).IsEquivalentTo(["dlx", "likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CLIPrefix_Deno_ReturnsDenoRunWithLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CLIPrefix(LocalCLIRuntime.Deno);

		// Assert
		await Assert.That(command).IsEqualTo("deno");
		await Assert.That(prefix).IsEquivalentTo(["run", "--allow-all", "likec4"]);
	}
}
