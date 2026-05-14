namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for the local CLI command builder logic.
/// </summary>
public sealed class AspireC4BuilderTests
{
	// --- BuildLocalCLICommand ---

	[Test]
	public async Task BuildLocalCLICommand_Npx_UsesNpxWithLikeC4Args()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Npx, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(args).IsEquivalentTo(["likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCLICommand_Pnpm_UsesPnpmExec()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Pnpm, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("pnpm");
		await Assert.That(args).IsEquivalentTo(["exec", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCLICommand_Yarn_UsesYarnDlx()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Yarn, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("yarn");
		await Assert.That(args).IsEquivalentTo(["dlx", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCLICommand_Bun_UsesBunx()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Bun, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("bunx");
		await Assert.That(args).IsEquivalentTo(["--bun", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCLICommand_Auto_Throws()
	{
		// Arrange

		// Act
		static (string Command, string[] Args) Action() =>
			AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Auto, "/tmp", 5173);

		// Assert
		await Assert.That(Action).Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task BuildLocalCLICommand_IncludesCorrectPort()
	{
		// Arrange

		// Act
		var (_, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Npx, "/output", 9090);

		// Assert
		await Assert.That(args).Contains("9090");
	}

	// --- BuildLikeC4CLIPrefix ---

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
