using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for the local CLI command builder logic and the <see cref="LikeC4ServerResource"/> constants.
/// </summary>
public sealed class LikeC4VisualizationBuilderTests
{
	// --- BuildLocalCliCommand ---

	[Test]
	public async Task BuildLocalCliCommand_Npx_UsesNpxWithLikeC4Args()
	{
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Npx, "/tmp/likec4", 5173);

		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(args).IsEquivalentTo(["likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Pnpm_UsesPnpmExec()
	{
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Pnpm, "/tmp/likec4", 5173);

		await Assert.That(command).IsEqualTo("pnpm");
		await Assert.That(args).IsEquivalentTo(["exec", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Yarn_UsesYarnDlx()
	{
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Yarn, "/tmp/likec4", 5173);

		await Assert.That(command).IsEqualTo("yarn");
		await Assert.That(args).IsEquivalentTo(["dlx", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Bun_UsesBunx()
	{
		var (command, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Bun, "/tmp/likec4", 5173);

		await Assert.That(command).IsEqualTo("bunx");
		await Assert.That(args).IsEquivalentTo(["likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Auto_Throws()
	{
		// Auto is resolved before reaching BuildLocalCliCommand; passing it directly is an error.
		await Assert
			.That(() => AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Auto, "/tmp", 5173))
			.Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task BuildLocalCliCommand_IncludesCorrectPort()
	{
		var (_, args) = AspireC4Builder.BuildLocalCliCommand(LikeC4LocalCLIRuntime.Npx, "/output", 9090);

		await Assert.That(args).Contains("9090");
	}

	[Test]
	public async Task LikeC4HmrPortCompatibility_UsesConfigurableModeForCurrentMinimumVersion()
	{
		var mode = LikeC4HmrPortCompatibility.Resolve("1.56.0");

		await Assert.That(mode).IsEqualTo(LikeC4HMRPortMode.Configurable);
	}

	[Test]
	public async Task LikeC4HmrPortCompatibility_UsesFixedPortForLegacyVersion()
	{
		var mode = LikeC4HmrPortCompatibility.Resolve("1.55.0", new Version(1, 56, 0));

		await Assert.That(mode).IsEqualTo(LikeC4HMRPortMode.FixedPort);
	}

	[Test]
	public async Task LikeC4HmrPortCompatibility_UsesConfigurableModeForSupportedVersion()
	{
		var mode = LikeC4HmrPortCompatibility.Resolve("v1.56.1-beta.2", new Version(1, 56, 0));

		await Assert.That(mode).IsEqualTo(LikeC4HMRPortMode.Configurable);
	}

	[Test]
	public async Task AddAspireC4_ExposesHttpAndHmrEndpoints()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		var visualization = appBuilder.AddAspireC4();
		var endpoints = visualization
			.LikeC4ResourceBuilder.Resource.Annotations.OfType<EndpointAnnotation>()
			.OrderBy(endpoint => endpoint.Name, StringComparer.Ordinal)
			.ToArray();

		await Assert.That(endpoints).Count().IsEqualTo(2);
		await Assert.That(endpoints[0].Name).IsEqualTo(LikeC4ServerResource.HttpEndpointName);
		await Assert.That(endpoints[0].TargetPort).IsEqualTo(LikeC4ServerResource.DefaultContainerServePort);
		await Assert.That(endpoints[1].Name).IsEqualTo(LikeC4ServerResource.HmrEndpointName);
		await Assert.That(endpoints[1].TargetPort).IsEqualTo(LikeC4ServerResource.DefaultContainerUpdatePort);
		// On Windows the relay is always used; Docker gets a dynamic host port so the relay
		// can own the well-known port (24678) without conflicting with Docker's binding.
		var expectedHmrPort = OperatingSystem.IsWindows()
			? null
			: (int?)LikeC4ServerResource.DefaultContainerUpdatePort;
		await Assert.That(endpoints[1].Port).IsEqualTo(expectedHmrPort);
	}

	[Test]
	public async Task AddAspireC4_UsesHmrRelayForFixedPortMode()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LikeC4ContainerWorkspaceOptions>>();

		await Assert.That(workspaceOptions.Value.UseHMRRelay).IsTrue();
	}

	[Test]
	public async Task AddAspireC4_UsesHmrRelayOnWindows()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		// Use Configurable-mode version; relay is still required on Windows.
		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.56.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LikeC4ContainerWorkspaceOptions>>();

		var expectedUseRelay = OperatingSystem.IsWindows();
		await Assert.That(workspaceOptions.Value.UseHMRRelay).IsEqualTo(expectedUseRelay);
	}

	[Test]
	public async Task AddAspireC4_StoresLegacyHmrCompatibilityMode()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LikeC4ContainerWorkspaceOptions>>();

		await Assert.That(workspaceOptions.Value.HMRPortMode).IsEqualTo(LikeC4HMRPortMode.FixedPort);
	}

	[Test]
	public async Task AddAspireC4_StoresConfigurableHmrCompatibilityModeForCurrentMinimumVersion()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.56.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LikeC4ContainerWorkspaceOptions>>();

		await Assert.That(workspaceOptions.Value.HMRPortMode).IsEqualTo(LikeC4HMRPortMode.Configurable);
	}

	[Test]
	public async Task AddAspireC4_UsesNamedWorkspaceVolumeForDefaultOutputDirectory()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		var visualization = appBuilder.AddAspireC4();
		var expectedSource = AspireC4DistributedApplicationBuilderExtensions.ResolveWorkspaceVolumeName(
			appBuilder.AppHostDirectory,
			AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName
		);
		var mounts = visualization
			.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
			.ToArray();

		await Assert.That(mounts).HasSingleItem();
		await Assert.That(mounts[0].Source).IsEqualTo(expectedSource);
		await Assert.That(mounts[0].Target).IsEqualTo(LikeC4ServerResource.WorkspacePath);
		await Assert.That(mounts[0].Type).IsEqualTo(ContainerMountType.Volume);
	}

	[Test]
	public async Task ResolveWorkspaceVolumeName_IsStableForTheSameAppHostDirectory()
	{
		var appHostDir = $"{Guid.NewGuid()}/subdir/{Guid.CreateVersion7()}/";

		var first = AspireC4DistributedApplicationBuilderExtensions.ResolveWorkspaceVolumeName(
			appHostDir,
			AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName
		);
		var second = AspireC4DistributedApplicationBuilderExtensions.ResolveWorkspaceVolumeName(
			appHostDir,
			AspireC4DistributedApplicationBuilderExtensions.AspireC4ResourceName
		);

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first).StartsWith("aspirec4-aspirec4-");
	}

	[Test]
	public async Task AddAspireC4_CreatesConfiguredOutputDirectory()
	{
		var outputDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);

		try
		{
			var appBuilder = DistributedApplication.CreateBuilder([]);

			appBuilder.AddAspireC4(configure: opts => opts.OutputDirectory = outputDir);

			await Assert.That(Directory.Exists(outputDir)).IsTrue();
		}
		finally
		{
			if (Directory.Exists(outputDir))
			{
				Directory.Delete(outputDir, recursive: true);
			}
		}
	}

	[Test]
	public async Task WithAdditionalDSLFile_AddsBindMountForSourceDirectory()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		// Create a temp file so Path.GetFullPath has a realistic absolute path.
		var tempDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(tempDir);
		var tempFile = Path.Combine(tempDir, "extra.c4");

		try
		{
			await File.WriteAllTextAsync(tempFile, "// extra");

			var visualization = appBuilder.AddAspireC4();
			visualization.WithAdditionalDSLFile(tempFile);

			var mounts = visualization
				.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
				.ToArray();

			// 1 named volume (workspace) + 1 bind mount (extra.c4 directory).
			await Assert.That(mounts.Length).EqualTo(2);

			var bindMount = mounts.FirstOrDefault(m => m.Type == ContainerMountType.BindMount);
			await Assert.That(bindMount).IsNotNull();
			await Assert.That(bindMount!.Source).IsEqualTo(tempDir);
			await Assert.That(bindMount.Target).StartsWith($"{LikeC4ServerResource.WorkspacePath}/ext/");
			await Assert.That(bindMount.IsReadOnly).IsTrue();
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, recursive: true);
		}
	}

	[Test]
	public async Task WithAdditionalDSLFile_SameDirectoryTwice_OnlyOneBindMount(CancellationToken cancellationToken)
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		var tempDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(tempDir);

		try
		{
			await File.WriteAllTextAsync(Path.Combine(tempDir, "a.c4"), "// a", cancellationToken);
			await File.WriteAllTextAsync(Path.Combine(tempDir, "b.c4"), "// b", cancellationToken);

			var visualization = appBuilder.AddAspireC4();
			visualization
				.WithAdditionalDSLFile(Path.Combine(tempDir, "a.c4"))
				.WithAdditionalDSLFile(Path.Combine(tempDir, "b.c4"));

			var bindMounts = visualization
				.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
				.Where(m => m.Type == ContainerMountType.BindMount)
				.ToArray();

			// Both files are in the same directory — only one bind mount should be added.
			await Assert.That(bindMounts).HasSingleItem();
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, recursive: true);
		}
	}
}
