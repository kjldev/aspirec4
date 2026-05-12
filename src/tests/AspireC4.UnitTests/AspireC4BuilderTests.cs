using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Unit tests for the local CLI command builder logic and the <see cref="LikeC4ServerResource"/> constants.
/// </summary>
public sealed class AspireC4BuilderTests
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
		await Assert.That(args).IsEquivalentTo(["--bun", "likec4", "serve", "/tmp/likec4", "--port", "5173"]);
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

	// --- BuildLikeC4CliPrefix ---

	[Test]
	public async Task BuildLikeC4CliPrefix_Npx_ReturnsNpxWithLikeC4()
	{
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Npx);

		await Assert.That(command).IsEqualTo("npx");
		await Assert.That(prefix).IsEquivalentTo(["likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Pnpm_ReturnsPnpmExecLikeC4()
	{
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Pnpm);

		await Assert.That(command).IsEqualTo("pnpm");
		await Assert.That(prefix).IsEquivalentTo(["exec", "likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Bun_ReturnsBunxWithLikeC4()
	{
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Bun);

		await Assert.That(command).IsEqualTo("bunx");
		await Assert.That(prefix).IsEquivalentTo(["likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Yarn_ReturnsYarnDlxLikeC4()
	{
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Yarn);

		await Assert.That(command).IsEqualTo("yarn");
		await Assert.That(prefix).IsEquivalentTo(["dlx", "likec4"]);
	}

	[Test]
	public async Task BuildLikeC4CliPrefix_Deno_ReturnsDenoRunWithLikeC4()
	{
		var (command, prefix) = AspireC4Builder.BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime.Deno);

		await Assert.That(command).IsEqualTo("deno");
		await Assert.That(prefix).IsEquivalentTo(["run", "--allow-all", "likec4"]);
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
	public async Task AddAspireC4_HasNoContainerMountAnnotationsAtConfigureTime()
	{
		// Bind mounts are set up lazily by the lifecycle hook in BeforeStartEvent,
		// not by the builder — so there must be zero annotations at configure-time.
		var appBuilder = DistributedApplication.CreateBuilder([]);

		var visualization = appBuilder.AddAspireC4();
		var mounts = visualization
			.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
			.ToArray();

		await Assert.That(mounts).IsEmpty();
	}

	[Test]
	public async Task WithAdditionalDSLFile_RegistersPathInDiagramOptions_NotBindMount()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		var tempDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(tempDir);
		var tempFile = Path.Combine(tempDir, "extra.c4");

		try
		{
			await File.WriteAllTextAsync(tempFile, "// extra");

			var visualization = appBuilder.AddAspireC4();
			visualization.WithAdditionalDSLFile(tempFile);

			// No ContainerMountAnnotation from the builder — the lifecycle hook handles mounts.
			var mounts = visualization
				.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
				.ToArray();
			await Assert.That(mounts).IsEmpty();

			// The file path must be registered so the lifecycle hook can copy + mount it.
			using var sp = appBuilder.Services.BuildServiceProvider();
			var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AspireC4DiagramOptions>>().Value;
			await Assert.That(opts.AdditionalDSLFiles).Contains(Path.GetFullPath(tempFile));
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, recursive: true);
		}
	}

	[Test]
	public async Task WithAdditionalDSLFolder_RegistersPathInDiagramOptions_NotBindMount()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var tempDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(tempDir);

		try
		{
			var visualization = appBuilder.AddAspireC4();
			visualization.WithAdditionalDSLFolder(tempDir);

			var mounts = visualization
				.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
				.ToArray();
			await Assert.That(mounts).IsEmpty();

			using var sp = appBuilder.Services.BuildServiceProvider();
			var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AspireC4DiagramOptions>>().Value;
			await Assert.That(opts.AdditionalDSLFolders).Contains(Path.GetFullPath(tempDir));
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, recursive: true);
		}
	}

	[Test]
	public async Task WithImageAliasFolder_RegistersAliasInDiagramOptions_NotBindMount()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var tempDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(tempDir);

		try
		{
			var visualization = appBuilder.AddAspireC4();
			visualization.WithImageAliasFolder("@icons", tempDir);

			var mounts = visualization
				.LikeC4ResourceBuilder.Resource.Annotations.OfType<ContainerMountAnnotation>()
				.ToArray();
			await Assert.That(mounts).IsEmpty();

			using var sp = appBuilder.Services.BuildServiceProvider();
			var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AspireC4DiagramOptions>>().Value;
			await Assert.That(opts.ImageAliases.ContainsKey("@icons")).IsTrue();
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, recursive: true);
		}
	}

	// --- ComputeCommonAncestor ---

	[Test]
	public async Task ComputeCommonAncestor_SinglePath_ReturnsThatPath()
	{
		var path = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
		var result = AspireC4LifecycleHook.ComputeCommonAncestor([path]);

		await Assert.That(result).IsEqualTo(Path.GetFullPath(path));
	}

	[Test]
	public async Task ComputeCommonAncestor_TwoPaths_SameDirectory_ReturnsThatDirectory()
	{
		var parent = Path.Combine(Path.GetTempPath(), "likec4-ancestor-" + Guid.NewGuid().ToString("N")[..8]);
		var child1 = Path.Combine(parent, "a");
		var child2 = Path.Combine(parent, "b");
		Directory.CreateDirectory(child1);
		Directory.CreateDirectory(child2);

		try
		{
			var result = AspireC4LifecycleHook.ComputeCommonAncestor([child1, child2]);
			await Assert.That(result).IsEqualTo(Path.GetFullPath(parent));
		}
		finally
		{
			Directory.Delete(parent, recursive: true);
		}
	}

	[Test]
	public async Task ComputeCommonAncestor_NestedPaths_ReturnsShallowerParent()
	{
		var root = Path.Combine(Path.GetTempPath(), "likec4-ancestor-" + Guid.NewGuid().ToString("N")[..8]);
		var deep = Path.Combine(root, "sub1", "sub2", "output");
		var sibling = Path.Combine(root, "assets");
		Directory.CreateDirectory(deep);
		Directory.CreateDirectory(sibling);

		try
		{
			var result = AspireC4LifecycleHook.ComputeCommonAncestor([deep, sibling]);
			await Assert.That(result).IsEqualTo(Path.GetFullPath(root));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task ComputeCommonAncestor_PathThatIsPrefixOfAnother_DoesNotFalselyMatch()
	{
		// e.g. C:\foo\bar vs C:\foo\barcode — should resolve to C:\foo
		var parent = Path.Combine(Path.GetTempPath(), "likec4-ancestor-" + Guid.NewGuid().ToString("N")[..8]);
		var bar = Path.Combine(parent, "bar");
		var barcode = Path.Combine(parent, "barcode");
		Directory.CreateDirectory(bar);
		Directory.CreateDirectory(barcode);

		try
		{
			var result = AspireC4LifecycleHook.ComputeCommonAncestor([bar, barcode]);
			await Assert.That(result).IsEqualTo(Path.GetFullPath(parent));
		}
		finally
		{
			Directory.Delete(parent, recursive: true);
		}
	}

	[Test]
	public async Task ComputeCommonAncestor_EmptyList_ThrowsArgumentException()
	{
		await Assert.That(() => AspireC4LifecycleHook.ComputeCommonAncestor([])).Throws<ArgumentException>();
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
	public async Task WithAdditionalDSLFolder_FolderDoesNotExist_ThrowsDirectoryNotFoundException()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var nonExistent = Path.Combine(Path.GetTempPath(), "likec4-nonexistent-" + Guid.NewGuid().ToString("N"));

		var visualization = appBuilder.AddAspireC4();

		await Assert
			.That(() => visualization.WithAdditionalDSLFolder(nonExistent))
			.Throws<DirectoryNotFoundException>();
	}

	[Test]
	public async Task WithImageAliasFolder_InvalidKey_ThrowsArgumentException()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var tempDir = Path.Combine(Path.GetTempPath(), "likec4-unit-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(tempDir);

		try
		{
			var visualization = appBuilder.AddAspireC4();

			await Assert.That(() => visualization.WithImageAliasFolder("icons", tempDir)).Throws<ArgumentException>();
		}
		finally
		{
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir, recursive: true);
		}
	}

	[Test]
	public async Task WithImageAliasFolder_FolderDoesNotExist_ThrowsDirectoryNotFoundException()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);
		var nonExistent = Path.Combine(Path.GetTempPath(), "likec4-nonexistent-" + Guid.NewGuid().ToString("N"));

		var visualization = appBuilder.AddAspireC4();

		await Assert
			.That(() => visualization.WithImageAliasFolder("@icons", nonExistent))
			.Throws<DirectoryNotFoundException>();
	}

	[Test]
	public async Task WithoutConfigFileGeneration_SetsFlagToFalse()
	{
		var appBuilder = DistributedApplication.CreateBuilder([]);

		appBuilder.AddAspireC4().WithoutConfigFileGeneration();

		using var sp = appBuilder.Services.BuildServiceProvider();
		var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AspireC4DiagramOptions>>().Value;

		await Assert.That(opts.GenerateConfigFile).IsFalse();
	}

	// --- NormalizeBindMountPath ---

	[Test]
	[Arguments(@"C:\Users\foo\bar", "/mnt/c/users/foo/bar")]
	[Arguments(@"P:\GitHub\myrepo\src", "/mnt/p/github/myrepo/src")]
	[Arguments(@"D:\", "/mnt/d/")]
	[Arguments(@"D:\Data\Projects\MyApp", "/mnt/d/data/projects/myapp")]
	[Arguments(@"Z:\very\deep\nested\path\here", "/mnt/z/very/deep/nested/path/here")]
	public async Task NormalizeBindMountPath_RancherDesktop_ConvertsToWsl2MntFormat(string windowsPath, string expected)
	{
		if (!OperatingSystem.IsWindows())
			return;

		await Assert
			.That(AspireC4Builder.NormalizeBindMountPath(windowsPath, ContainerRuntime.RancherDesktop))
			.IsEqualTo(expected);
	}

	[Test]
	[Arguments(@"C:\Users\foo\bar")]
	[Arguments(@"P:\GitHub\myrepo\src")]
	[Arguments(@"D:\Data\Projects")]
	public async Task NormalizeBindMountPath_RancherDesktop_PathStartsWithMntSlash(string windowsPath)
	{
		if (!OperatingSystem.IsWindows())
			return;

		var result = AspireC4Builder.NormalizeBindMountPath(windowsPath, ContainerRuntime.RancherDesktop);

		await Assert.That(result).StartsWith("/mnt/");
		await Assert.That(result).DoesNotContain(":");
		await Assert.That(result).DoesNotContain(@"\");
	}

	[Test]
	[Arguments(@"C:\Users\foo\bar")]
	[Arguments(@"P:\GitHub\myrepo\src")]
	[Arguments(@"D:\Data\Projects")]
	public async Task NormalizeBindMountPath_DockerDesktop_ReturnsWindowsPath(string windowsPath)
	{
		if (!OperatingSystem.IsWindows())
			return;

		var result = AspireC4Builder.NormalizeBindMountPath(windowsPath, ContainerRuntime.DockerDesktop);

		await Assert.That(result).StartsWith(windowsPath[0].ToString(), StringComparison.OrdinalIgnoreCase);
		await Assert.That(result).Contains(@"\");
	}

	[Test]
	[Arguments(@"C:\Users\foo\bar")]
	[Arguments(@"P:\GitHub\myrepo\src")]
	[Arguments(@"D:\Data\Projects")]
	public async Task NormalizeBindMountPath_Podman_ReturnsWindowsPathUnchanged(string windowsPath)
	{
		// Podman on Windows performs its own path translation internally (ConvertWinMountPath),
		// so AspireC4 must pass the Windows path as-is.
		if (!OperatingSystem.IsWindows())
			return;

		var result = AspireC4Builder.NormalizeBindMountPath(windowsPath, ContainerRuntime.Podman);

		await Assert.That(result).StartsWith(windowsPath[0].ToString(), StringComparison.OrdinalIgnoreCase);
		await Assert.That(result).Contains(@"\");
	}

	[Test]
	[Arguments(@"C:\Users\foo\bar")]
	[Arguments(@"P:\GitHub\myrepo\src")]
	[Arguments(@"D:\Data\Projects")]
	public async Task NormalizeBindMountPath_Podman_ProducesIdenticalResultToDockerDesktop(string windowsPath)
	{
		// Both runtimes must return the Windows path unchanged — the caller (podman.exe /
		// Docker Desktop daemon) handles any further translation.
		if (!OperatingSystem.IsWindows())
			return;

		var dockerDesktopResult = AspireC4Builder.NormalizeBindMountPath(windowsPath, ContainerRuntime.DockerDesktop);
		var podmanResult = AspireC4Builder.NormalizeBindMountPath(windowsPath, ContainerRuntime.Podman);

		await Assert.That(podmanResult).IsEqualTo(dockerDesktopResult);
	}

	[Test]
	[Arguments("/home/user/projects/myapp")]
	[Arguments("/var/data/output")]
	[Arguments("/tmp/likec4")]
	public async Task NormalizeBindMountPath_Linux_ReturnsPathUnchanged(string linuxPath)
	{
		if (OperatingSystem.IsWindows())
			return;

		await Assert
			.That(AspireC4Builder.NormalizeBindMountPath(linuxPath, ContainerRuntime.Linux))
			.IsEqualTo(linuxPath);
	}

	// --- DetectContainerRuntime ---

	[Test]
	public async Task DetectContainerRuntime_OnLinux_ReturnsLinux()
	{
		if (OperatingSystem.IsWindows())
			return;

		var runtime = AspireC4Builder.DetectContainerRuntime();

		await Assert.That(runtime).IsEqualTo(ContainerRuntime.Linux);
	}

	// --- ComputeCommonAncestor (additional cases) ---

	[Test]
	public async Task ComputeCommonAncestor_ThreePaths_FindsCommonRoot()
	{
		var root = Path.Combine(Path.GetTempPath(), "likec4-ancestor-" + Guid.NewGuid().ToString("N")[..8]);
		var a = Path.Combine(root, "src", "output");
		var b = Path.Combine(root, "assets", "images");
		var c = Path.Combine(root, "extensions");
		Directory.CreateDirectory(a);
		Directory.CreateDirectory(b);
		Directory.CreateDirectory(c);

		try
		{
			var result = AspireC4LifecycleHook.ComputeCommonAncestor([a, b, c]);
			await Assert.That(result).IsEqualTo(Path.GetFullPath(root));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task ComputeCommonAncestor_AllPathsSame_ReturnsThatPath()
	{
		var dir = Path.Combine(Path.GetTempPath(), "likec4-ancestor-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(dir);

		try
		{
			var result = AspireC4LifecycleHook.ComputeCommonAncestor([dir, dir, dir]);
			await Assert.That(result).IsEqualTo(Path.GetFullPath(dir));
		}
		finally
		{
			Directory.Delete(dir, recursive: true);
		}
	}

	[Test]
	public async Task ComputeCommonAncestor_OneIsDirectParentOfOthers_ReturnsParent()
	{
		var parent = Path.Combine(Path.GetTempPath(), "likec4-ancestor-" + Guid.NewGuid().ToString("N")[..8]);
		var child1 = Path.Combine(parent, "output");
		var child2 = Path.Combine(parent, "dsl");
		Directory.CreateDirectory(child1);
		Directory.CreateDirectory(child2);

		try
		{
			var result = AspireC4LifecycleHook.ComputeCommonAncestor([parent, child1, child2]);
			await Assert.That(result).IsEqualTo(Path.GetFullPath(parent));
		}
		finally
		{
			Directory.Delete(parent, recursive: true);
		}
	}

	[Test]
	public async Task ComputeCommonAncestor_DeepHierarchy_FindsShallowAncestor()
	{
		var root = Path.Combine(Path.GetTempPath(), "likec4-ancestor-" + Guid.NewGuid().ToString("N")[..8]);
		var deep = Path.Combine(root, "a", "b", "c", "d", "output");
		var sibling = Path.Combine(root, "a", "assets");
		Directory.CreateDirectory(deep);
		Directory.CreateDirectory(sibling);

		try
		{
			var result = AspireC4LifecycleHook.ComputeCommonAncestor([deep, sibling]);
			await Assert.That(result).IsEqualTo(Path.GetFullPath(Path.Combine(root, "a")));
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
