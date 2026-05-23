namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for the local CLI command builder logic.
/// </summary>
public sealed partial class AspireC4BuilderTests
{
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
	public async Task BuildLocalCLICommand_Pnpm_UsesPnpmDlx()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Pnpm, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("pnpm");
		await Assert
			.That(args)
			.IsEquivalentTo(["dlx", "--ignore-workspace", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCLICommand_Yarn_UsesYarnDlx()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Yarn, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("yarn");
		await Assert
			.That(args)
			.IsEquivalentTo([
				"dlx",
				"--package",
				"likec4",
				"--package",
				"react",
				"--package",
				"react-dom",
				"likec4",
				"serve",
				"/tmp/likec4",
				"--port",
				"5173",
			]);
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
	public async Task BuildLocalCLICommand_Deno_UsesDenoRunWithNodeModulesDirNone()
	{
		// Arrange

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Deno, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(command).IsEqualTo("deno");
		await Assert
			.That(args)
			.IsEquivalentTo([
				"run",
				"--allow-all",
				"--node-modules-dir=none",
				"npm:likec4",
				"serve",
				"/tmp/likec4",
				"--port",
				"5173",
			]);
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

	[Test]
	[MatrixDataSource]
	public async Task BuildLocalCLICommand_WithHmrPort_IncludesHmrPortArg(
		[MatrixMethod<AspireC4BuilderTests>(nameof(AllRuntimes))] LocalCLIRuntime runtime
	)
	{
		// Arrange
		const int hmrPort = 12345;

		// Act
		var (_, args) = AspireC4Builder.BuildLocalCLICommand(runtime, "/tmp/likec4", 5173, hmrPort);

		// Assert
		var argList = args.ToList();
		var hmrPortIdx = argList.IndexOf("--hmr-port");
		await Assert.That(hmrPortIdx).IsGreaterThan(-1);
		await Assert.That(args[hmrPortIdx + 1]).IsEqualTo("12345");
	}

	[Test]
	[MatrixDataSource]
	public async Task BuildLocalCLICommand_WithoutHmrPort_ExcludesHmrPortArg(
		[MatrixMethod<AspireC4BuilderTests>(nameof(AllRuntimes))] LocalCLIRuntime runtime
	)
	{
		// Arrange

		// Act
		var (_, args) = AspireC4Builder.BuildLocalCLICommand(runtime, "/tmp/likec4", 5173);

		// Assert
		await Assert.That(args).DoesNotContain("--hmr-port");
	}

	[Test]
	public async Task BuildLocalCLICommand_Npx_WithHmrPort_IncludesAllExpectedArgs()
	{
		// Arrange
		const int hmrPort = 24678;

		// Act
		var (command, args) = AspireC4Builder.BuildLocalCLICommand(LocalCLIRuntime.Npx, "/tmp/likec4", 5173, hmrPort);

		// Assert
		await Assert.That(command).IsEqualTo("npx");
		await Assert
			.That(args)
			.IsEquivalentTo(["likec4", "serve", "/tmp/likec4", "--port", "5173", "--hmr-port", "24678"]);
	}

	IEnumerable<LocalCLIRuntime> AllRuntimes()
	{
		yield return LocalCLIRuntime.Npx;
		yield return LocalCLIRuntime.Pnpm;
		yield return LocalCLIRuntime.Yarn;
		yield return LocalCLIRuntime.Bun;
		yield return LocalCLIRuntime.Deno;
	}
}
