namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for the local CLI command builder logic.
/// </summary>
public sealed class AspireC4BuilderTests
{
	// --- BuildLocalCliCommand ---

	[Test]
	public async Task BuildLocalCliCommand_Npx_UsesNpxWithLikeC4Args()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Npx, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(args).IsEquivalentTo(["likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Pnpm_UsesPnpmExec()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Pnpm, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("pnpm");
		await Assert.That(args).IsEquivalentTo(["exec", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Yarn_UsesYarnDlx()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Yarn, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("yarn");
		await Assert.That(args).IsEquivalentTo(["dlx", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Bun_UsesBunx()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Bun, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("bunx");
		await Assert.That(args).IsEquivalentTo(["--bun", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Auto_Throws()
	{
		// Arrange

		// Act
		static (string Command, string[] Args) Action() =>
			AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Auto, "/tmp", 5173);

		// Assert
		await Assert.That(Action).Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task BuildLocalCliCommand_IncludesCorrectPort()
	{
		// Arrange

		// Act
		var (_, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Npx, "/output", 9090);

		// Assert
		await Assert.That(args).Contains("9090");
	}

	// --- BuildLikeC4CliPrefix ---

	[Test]
	public async Task BuildLikeC4CliPrefix_Npx_ReturnsNpxWithLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Npx);

		// Assert
		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(prefix).IsEquivalentTo(["likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Pnpm_ReturnsPnpmExecLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Pnpm);

		// Assert
		await Assert.That(command).IsEqualTo("pnpm");
		await Assert.That(prefix).IsEquivalentTo(["exec", "likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Bun_ReturnsBunxWithLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Bun);

		// Assert
		await Assert.That(command).IsEqualTo("bunx");
		await Assert.That(prefix).IsEquivalentTo(["likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Yarn_ReturnsYarnDlxLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Yarn);

		// Assert
		await Assert.That(command).IsEqualTo("yarn");
		await Assert.That(prefix).IsEquivalentTo(["dlx", "likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Deno_ReturnsDenoRunWithLikeC4()
	{
		// Arrange

		// Act
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Deno);

		// Assert
		await Assert.That(command).IsEqualTo("deno");
		await Assert.That(prefix).IsEquivalentTo(["run", "--allow-all", "likec4"]);
	}
}
