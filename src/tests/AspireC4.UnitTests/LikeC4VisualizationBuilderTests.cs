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
		var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
			LikeC4LocalCliRuntime.Npx,
			"/tmp/likec4",
			5173
		);

		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(args).IsEquivalentTo(["likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Pnpm_UsesPnpmExec()
	{
		var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
			LikeC4LocalCliRuntime.Pnpm,
			"/tmp/likec4",
			5173
		);

		await Assert.That(command).IsEqualTo("pnpm");
		await Assert.That(args).IsEquivalentTo(["exec", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Yarn_UsesYarnDlx()
	{
		var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
			LikeC4LocalCliRuntime.Yarn,
			"/tmp/likec4",
			5173
		);

		await Assert.That(command).IsEqualTo("yarn");
		await Assert.That(args).IsEquivalentTo(["dlx", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Bun_UsesBunx()
	{
		var (command, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(
			LikeC4LocalCliRuntime.Bun,
			"/tmp/likec4",
			5173
		);

		await Assert.That(command).IsEqualTo("bunx");
		await Assert.That(args).IsEquivalentTo(["likec4", "serve", "/tmp/likec4", "--port", "5173"]);
	}

	[Test]
	public async Task BuildLocalCliCommand_Auto_Throws()
	{
		// Auto is resolved before reaching BuildLocalCliCommand; passing it directly is an error.
		await Assert
			.That(() => LikeC4VisualizationBuilder.BuildLocalCliCommand(LikeC4LocalCliRuntime.Auto, "/tmp", 5173))
			.Throws<ArgumentOutOfRangeException>();
	}

	[Test]
	public async Task BuildLocalCliCommand_IncludesCorrectPort()
	{
		var (_, args) = LikeC4VisualizationBuilder.BuildLocalCliCommand(LikeC4LocalCliRuntime.Npx, "/output", 9090);

		await Assert.That(args).Contains("9090");
	}

	[Test]
	public async Task LikeC4HmrPortCompatibility_UsesConfigurableModeForCurrentMinimumVersion()
	{
		var mode = LikeC4HmrPortCompatibility.Resolve("1.56.0");

		await Assert.That(mode).IsEqualTo(LikeC4HmrPortMode.Configurable);
	}

	[Test]
	public async Task LikeC4HmrPortCompatibility_UsesFixedPortForLegacyVersion()
	{
		var mode = LikeC4HmrPortCompatibility.Resolve("1.55.0", new Version(1, 56, 0));

		await Assert.That(mode).IsEqualTo(LikeC4HmrPortMode.FixedPort);
	}

	[Test]
	public async Task LikeC4HmrPortCompatibility_UsesConfigurableModeForSupportedVersion()
	{
		var mode = LikeC4HmrPortCompatibility.Resolve("v1.56.1-beta.2", new Version(1, 56, 0));

		await Assert.That(mode).IsEqualTo(LikeC4HmrPortMode.Configurable);
	}

	[Test]
	public async Task AddAspireC4_ExposesHttpAndHmrEndpoints()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		var visualization = appBuilder.AddAspireC4();
		var endpoints = visualization
			.ServerResourceBuilder.Resource.Annotations.OfType<EndpointAnnotation>()
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

		await Assert.That(workspaceOptions.Value.UseHmrRelay).IsTrue();
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
		await Assert.That(workspaceOptions.Value.UseHmrRelay).IsEqualTo(expectedUseRelay);
	}

	[Test]
	public async Task AddAspireC4_StoresLegacyHmrCompatibilityMode()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.55.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LikeC4ContainerWorkspaceOptions>>();

		await Assert.That(workspaceOptions.Value.HmrPortMode).IsEqualTo(LikeC4HmrPortMode.FixedPort);
	}

	[Test]
	public async Task AddAspireC4_StoresConfigurableHmrCompatibilityModeForCurrentMinimumVersion()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		appBuilder.AddAspireC4(configure: opts => opts.ContainerImageTag = "1.56.0");
		using var provider = appBuilder.Services.BuildServiceProvider();
		var workspaceOptions =
			provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LikeC4ContainerWorkspaceOptions>>();

		await Assert.That(workspaceOptions.Value.HmrPortMode).IsEqualTo(LikeC4HmrPortMode.Configurable);
	}

	[Test]
	public async Task AddAspireC4_UsesNamedWorkspaceVolumeForDefaultOutputDirectory()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		var visualization = appBuilder.AddAspireC4();
		var expectedSource = AspireC4DistributedApplicationBuilderExtensions.ResolveWorkspaceVolumeName(
			appBuilder.AppHostDirectory,
			AspireC4DistributedApplicationBuilderExtensions.ServerResourceName
		);
		var mounts = visualization
			.ServerResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
			.ToArray();

		await Assert.That(mounts).HasSingleItem();
		await Assert.That(mounts[0].Source).IsEqualTo(expectedSource);
		await Assert.That(mounts[0].Target).IsEqualTo(LikeC4ServerResource.WorkspacePath);
		await Assert.That(mounts[0].Type).IsEqualTo(ContainerMountType.Volume);
	}

	[Test]
	public async Task ResolveWorkspaceVolumeName_IsStableForTheSameAppHostDirectory()
	{
		var first = AspireC4DistributedApplicationBuilderExtensions.ResolveWorkspaceVolumeName(
			@"P:\GitHub\kjldev\aspirec4\src\tests\AspireC4.TestAppHost",
			AspireC4DistributedApplicationBuilderExtensions.ServerResourceName
		);
		var second = AspireC4DistributedApplicationBuilderExtensions.ResolveWorkspaceVolumeName(
			@"P:\GitHub\kjldev\aspirec4\src\tests\AspireC4.TestAppHost\",
			AspireC4DistributedApplicationBuilderExtensions.ServerResourceName
		);

		await Assert.That(first).IsEqualTo(second);
		await Assert.That(first).StartsWith("likec4-likec4-visualization-");
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
}
