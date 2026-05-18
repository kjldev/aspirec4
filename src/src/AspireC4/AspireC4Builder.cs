using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Internal utility methods shared between <see cref="AspireC4ResourceExtensions"/> and the lifecycle hook.
/// </summary>
static class AspireC4Builder
{
	static readonly Lazy<ContainerRuntime> ContainerRuntime = new(
		DetectContainerRuntime,
		LazyThreadSafetyMode.ExecutionAndPublication
	);

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
	public static string NormalizeBindMountPath(string absolutePath) =>
		NormalizeBindMountPath(absolutePath, ContainerRuntime.Value);

	/// <summary>Overload with an explicit runtime — used by unit tests to avoid
	/// spawning a <c>docker</c> process.</summary>
	public static string NormalizeBindMountPath(string absolutePath, ContainerRuntime runtime)
	{
		var fullPath = Path.GetFullPath(absolutePath);

		// Rancher Desktop exposes Windows drives under /mnt/<letter>/ inside WSL2.
		// CA1308: intentional — Linux paths require lower-case.
		if (
			runtime == LikeC4.Runtime.ContainerRuntime.RancherDesktop
			&& fullPath.Length >= 2
			&& char.IsAsciiLetter(fullPath[0])
			&& fullPath[1] == ':'
		)
		{
			return $"/mnt/{char.ToLowerInvariant(fullPath[0])}{fullPath[2..].Replace('\\', '/')}".ToLowerInvariantSafe();
		}

		// All other runtimes: return the path as-is.
		// - Linux/macOS: path is already a Linux path.
		// - Docker Desktop: Windows-native daemon accepts C:\… paths directly.
		// - Podman (Windows): podman.exe performs its own Windows→Linux conversion.
		return fullPath;
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Runtime detection must not throw; failure falls back to Docker Desktop behavior."
	)]
	public static ContainerRuntime DetectContainerRuntime()
	{
		if (!OperatingSystem.IsWindows())
			return LikeC4.Runtime.ContainerRuntime.Linux;

		// If Podman is explicitly requested, podman.exe handles path translation itself.
		var runtimeEnv = Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME");
		if (runtimeEnv?.Equals("podman", StringComparison.OrdinalIgnoreCase) == true)
			return LikeC4.Runtime.ContainerRuntime.Podman;

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
			if (process is not null && !process.HasExited)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch { }

				return LikeC4.Runtime.ContainerRuntime.DockerDesktop;
			}

			var os = process?.StandardOutput.ReadToEnd().Trim() ?? "";

			if (os.Contains("Rancher Desktop", StringComparison.OrdinalIgnoreCase))
				return LikeC4.Runtime.ContainerRuntime.RancherDesktop;
		}
		catch { }

		return LikeC4.Runtime.ContainerRuntime.DockerDesktop;
	}

	public static LocalCLIRuntime DetectRuntime()
	{
		// Try runtimes in order of preference.
		(LocalCLIRuntime Runtime, string Executable)[] candidates =
		[
			(LocalCLIRuntime.Npx, "npx"),
			(LocalCLIRuntime.Pnpm, "pnpm"),
			(LocalCLIRuntime.Yarn, "yarn"),
			(LocalCLIRuntime.Bun, "bun"),
			(LocalCLIRuntime.Deno, "deno"),
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
	public static bool IsExecutableOnPath(string executable)
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
	public static (string Command, string[] Prefix) BuildLikeC4CLIPrefix(LocalCLIRuntime runtime) =>
		runtime switch
		{
			LocalCLIRuntime.Npx => ("npx", ["likec4"]),
			LocalCLIRuntime.Pnpm => ("pnpm", ["exec", "likec4"]),
			LocalCLIRuntime.Yarn => ("yarn", ["dlx", "likec4"]),
			LocalCLIRuntime.Bun => ("bunx", ["likec4"]),
			LocalCLIRuntime.Deno => ("deno", ["run", "--allow-all", "likec4"]),
			_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, $"Unsupported runtime: {runtime}"),
		};

	/// <summary>
	/// Resolves the executable command and arguments for the given local CLI runtime.
	/// Internal and visible for testing.
	/// </summary>
	public static (string Command, string[] Args) BuildLocalCLICommand(
		LocalCLIRuntime runtime,
		string outputDirectory,
		int port
	)
	{
		var portStr = $"{port}";
		return runtime switch
		{
			LocalCLIRuntime.Npx => ("npx", ["likec4", "serve", outputDirectory, "--port", portStr]),
			LocalCLIRuntime.Pnpm => ("pnpm", ["exec", "likec4", "serve", outputDirectory, "--port", portStr]),
			LocalCLIRuntime.Yarn => ("yarn", ["dlx", "likec4", "serve", outputDirectory, "--port", portStr]),
			LocalCLIRuntime.Bun => ("bunx", ["--bun", "likec4", "serve", outputDirectory, "--port", portStr]),
			LocalCLIRuntime.Deno => (
				"deno",
				["run", "--allow-all", "likec4", "serve", outputDirectory, "--port", portStr]
			),
			_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, $"Unsupported runtime: {runtime}"),
		};
	}
}
