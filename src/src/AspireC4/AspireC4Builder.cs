using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.AspireC4;

sealed class AspireC4Builder(
	IDistributedApplicationBuilder applicationBuilder,
	IResourceBuilder<IResource> serverResourceBuilder,
	string outputDirectory
) : IAspireC4Builder
{
	public IDistributedApplicationBuilder ApplicationBuilder { get; } = applicationBuilder;

	public IResourceBuilder<IResource> LikeC4ResourceBuilder { get; } = serverResourceBuilder;

	internal string OutputDirectory { get; } = outputDirectory;

	public IAspireC4Builder WithLocalCLI(LikeC4LocalCLIRuntime runtime = LikeC4LocalCLIRuntime.Auto)
	{
		// Remove the existing server resource (container by default) from the app model.
		ApplicationBuilder.Resources.Remove(LikeC4ResourceBuilder.Resource);

		var resolvedRuntime = runtime == LikeC4LocalCLIRuntime.Auto ? DetectRuntime() : runtime;

		var (command, args) = BuildLocalCliCommand(
			resolvedRuntime,
			OutputDirectory,
			LikeC4LocalServerResource.DefaultPort
		);

		var localResource = new LikeC4LocalServerResource(
			LikeC4ResourceBuilder.Resource.Name,
			command,
			OutputDirectory
		);

		var localBuilder = ApplicationBuilder
			.AddResource(localResource)
			.WithArgs(args)
			.WithHttpEndpoint(
				name: LikeC4LocalServerResource.HttpEndpointName,
				targetPort: LikeC4LocalServerResource.DefaultPort
			)
			.WithExternalHttpEndpoints()
			.WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);

		// Store the resolved runtime so the lifecycle hook can use the same JS runner
		// when invoking host-side likec4 subcommands (format, validate).
		ApplicationBuilder.Services.Configure<LikeC4ContainerWorkspaceOptions>(wsOpts =>
			wsOpts.LocalCLIRuntime = resolvedRuntime
		);

		return new AspireC4Builder(ApplicationBuilder, localBuilder, OutputDirectory);
	}

	public IAspireC4Builder WithHideFromDashboard(string displayName = "Architecture Diagram")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

		ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
		{
			opts.HideFromDashboard = true;
			opts.DashboardLinkDisplayName = displayName;
		});

		return this;
	}

	public IAspireC4Builder WithAdditionalDSLFile(string sourcePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

		var absoluteSource = Path.GetFullPath(sourcePath);
		ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
			opts.AdditionalDSLFiles.Add(absoluteSource)
		);

		return this;
	}

	public IAspireC4Builder WithAdditionalDSLFolder(string folderPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		var absoluteFolder = Path.GetFullPath(folderPath);
		if (!Directory.Exists(absoluteFolder))
			throw new DirectoryNotFoundException($"The additional DSL folder does not exist: '{absoluteFolder}'");

		ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
			opts.AdditionalDSLFolders.Add(absoluteFolder)
		);

		return this;
	}

	public IAspireC4Builder WithImageAliasFolder(string aliasKey, string folderPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aliasKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

		if (!aliasKey.StartsWith('@'))
			throw new ArgumentException("Image alias keys must start with '@'.", nameof(aliasKey));

		var absoluteFolder = Path.GetFullPath(folderPath);
		if (!Directory.Exists(absoluteFolder))
			throw new DirectoryNotFoundException($"The image alias folder does not exist: '{absoluteFolder}'");

		ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts =>
			opts.ImageAliases[aliasKey] = absoluteFolder
		);

		return this;
	}

	public IAspireC4Builder WithoutConfigFileGeneration()
	{
		ApplicationBuilder.Services.Configure<AspireC4DiagramOptions>(opts => opts.GenerateConfigFile = false);

		return this;
	}

	/// <summary>
	/// Returns the correct bind-mount source path for the given host directory.
	/// </summary>
	/// <remarks>
	/// On Windows the correct format depends on the container runtime:
	/// <list type="bullet">
	///   <item><description>
	///     <b>Docker Desktop (official)</b> — natively understands Windows paths
	///     (<c>C:\…</c>); no conversion needed.
	///   </description></item>
	///   <item><description>
	///     <b>Rancher Desktop</b> — runs a Linux <c>dockerd</c> inside WSL2 that only
	///     understands Linux paths.  Windows drives are accessible at
	///     <c>/mnt/&lt;drive&gt;/…</c>, so the path is converted to that format.
	///   </description></item>
	///   <item><description>
	///     <b>Podman (Windows)</b> — <c>podman.exe</c> translates Windows paths
	///     internally via its own <c>ConvertWinMountPath</c> logic; no conversion needed.
	///   </description></item>
	/// </list>
	/// On non-Windows the path is returned unchanged.
	/// </remarks>
	internal static string NormalizeBindMountPath(string absolutePath) =>
		NormalizeBindMountPath(absolutePath, ContainerRuntime.Value);

	/// <summary>Overload with an explicit runtime — used by unit tests to avoid
	/// spawning a <c>docker</c> process.</summary>
	internal static string NormalizeBindMountPath(string absolutePath, ContainerRuntime runtime)
	{
		var fullPath = Path.GetFullPath(absolutePath);

		// Rancher Desktop exposes Windows drives under /mnt/<letter>/ inside WSL2.
		// CA1308: intentional — Linux paths require lower-case.
		if (
			runtime == AspireC4.ContainerRuntime.RancherDesktop
			&& fullPath.Length >= 2
			&& char.IsAsciiLetter(fullPath[0])
			&& fullPath[1] == ':'
		)
		{
#pragma warning disable CA1308
			return $"/mnt/{char.ToLowerInvariant(fullPath[0])}{fullPath[2..].Replace('\\', '/')}".ToLowerInvariant();
#pragma warning restore CA1308
		}

		// All other runtimes: return the path as-is.
		// - Linux/macOS: path is already a Linux path.
		// - Docker Desktop: Windows-native daemon accepts C:\… paths directly.
		// - Podman (Windows): podman.exe performs its own Windows→Linux conversion.
		return fullPath;
	}

	static readonly Lazy<ContainerRuntime> ContainerRuntime = new(
		DetectContainerRuntime,
		LazyThreadSafetyMode.ExecutionAndPublication
	);

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Runtime detection must not throw; failure falls back to Docker Desktop behavior."
	)]
	internal static ContainerRuntime DetectContainerRuntime()
	{
		if (!OperatingSystem.IsWindows())
			return AspireC4.ContainerRuntime.Linux;

		// If Podman is explicitly requested, podman.exe handles path translation itself.
		var runtimeEnv = Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME");
		if (runtimeEnv?.Equals("podman", StringComparison.OrdinalIgnoreCase) == true)
			return AspireC4.ContainerRuntime.Podman;

		// Query the Docker daemon OS string to distinguish Docker Desktop from Rancher Desktop.
		try
		{
			using var process = System.Diagnostics.Process.Start(
				new System.Diagnostics.ProcessStartInfo
				{
					FileName = "docker",
					Arguments = "info --format {{.OperatingSystem}}",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);

			process?.WaitForExit(5_000);
			var os = process?.StandardOutput.ReadToEnd().Trim() ?? "";

			if (os.Contains("Rancher Desktop", StringComparison.OrdinalIgnoreCase))
				return AspireC4.ContainerRuntime.RancherDesktop;
		}
		catch { }

		return AspireC4.ContainerRuntime.DockerDesktop;
	}

	static LikeC4LocalCLIRuntime DetectRuntime()
	{
		// Try runtimes in order of preference.
		(LikeC4LocalCLIRuntime Runtime, string Executable)[] candidates =
		[
			(LikeC4LocalCLIRuntime.Npx, "npx"),
			(LikeC4LocalCLIRuntime.Pnpm, "pnpm"),
			(LikeC4LocalCLIRuntime.Yarn, "yarn"),
			(LikeC4LocalCLIRuntime.Bun, "bun"),
			(LikeC4LocalCLIRuntime.Deno, "deno"),
		];

		foreach (var (candidate, executable) in candidates)
		{
			if (IsExecutableOnPath(executable))
				return candidate;
		}

		throw new DistributedApplicationException(
			"No supported JavaScript package manager was found on the system PATH. "
				+ "Install one of: Node.js (npx), pnpm, yarn, bun, or Deno, then retry. "
				+ "Alternatively, remove WithLocalCLI() to use the Docker container (default)."
		);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "If the exe isn't there or isn't correctly installed, it's not appropriate for use"
	)]
	static bool IsExecutableOnPath(string executable)
	{
		try
		{
			using var process = System.Diagnostics.Process.Start(
				new System.Diagnostics.ProcessStartInfo
				{
					FileName = executable,
					Arguments = "--version",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);

			process?.WaitForExit(3_000);
			return process?.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Returns the executable and the argument prefix required to invoke <c>likec4</c> via
	/// the given runtime — i.e. everything that comes BEFORE the likec4 subcommand.
	/// Internal and visible for testing.
	/// </summary>
	/// <example>
	/// Npx  → <c>("npx",  ["likec4"])</c> so the full call is <c>npx likec4 format ...</c>
	/// Pnpm → <c>("pnpm", ["exec", "likec4"])</c>
	/// Yarn → <c>("yarn", ["dlx", "likec4"])</c>
	/// Bun  → <c>("bunx", ["--bun", "likec4"])</c>
	/// Deno → <c>("deno", ["run", "--allow-all", "likec4"])</c>
	/// </example>
	internal static (string Command, string[] Prefix) BuildLikeC4CliPrefix(LikeC4LocalCLIRuntime runtime) =>
		runtime switch
		{
			LikeC4LocalCLIRuntime.Npx => ("npx", ["likec4"]),
			LikeC4LocalCLIRuntime.Pnpm => ("pnpm", ["exec", "likec4"]),
			LikeC4LocalCLIRuntime.Yarn => ("yarn", ["dlx", "likec4"]),
			LikeC4LocalCLIRuntime.Bun => ("bunx", ["likec4"]),
			LikeC4LocalCLIRuntime.Deno => ("deno", ["run", "--allow-all", "likec4"]),
			_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, $"Unsupported runtime: {runtime}"),
		};

	/// <summary>
	/// Resolves the executable command and arguments for the given local CLI runtime.
	/// Internal and visible for testing.
	/// </summary>
	internal static (string Command, string[] Args) BuildLocalCliCommand(
		LikeC4LocalCLIRuntime runtime,
		string outputDirectory,
		int port
	)
	{
		var portStr = $"{port}";
		return runtime switch
		{
			LikeC4LocalCLIRuntime.Npx => ("npx", ["likec4", "serve", outputDirectory, "--port", portStr]),
			LikeC4LocalCLIRuntime.Pnpm => ("pnpm", ["exec", "likec4", "serve", outputDirectory, "--port", portStr]),
			LikeC4LocalCLIRuntime.Yarn => ("yarn", ["dlx", "likec4", "serve", outputDirectory, "--port", portStr]),
			LikeC4LocalCLIRuntime.Bun => ("bunx", ["--bun", "likec4", "serve", outputDirectory, "--port", portStr]),
			LikeC4LocalCLIRuntime.Deno => (
				"deno",
				["run", "--allow-all", "likec4", "serve", outputDirectory, "--port", portStr]
			),
			_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, $"Unsupported runtime: {runtime}"),
		};
	}
}
