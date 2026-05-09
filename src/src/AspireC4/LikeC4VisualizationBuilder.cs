using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

sealed class LikeC4VisualizationBuilder(
	IDistributedApplicationBuilder applicationBuilder,
	IResourceBuilder<IResource> serverResourceBuilder,
	string outputDirectory
) : ILikeC4VisualizationBuilder
{
	public IDistributedApplicationBuilder ApplicationBuilder { get; } = applicationBuilder;

	public IResourceBuilder<IResource> ServerResourceBuilder { get; } = serverResourceBuilder;

	internal string OutputDirectory { get; } = outputDirectory;

	public ILikeC4VisualizationBuilder WithLocalCli(LikeC4LocalCliRuntime runtime = LikeC4LocalCliRuntime.Auto)
	{
		// Remove the existing server resource (container by default) from the app model.
		ApplicationBuilder.Resources.Remove(ServerResourceBuilder.Resource);

		var resolvedRuntime = runtime == LikeC4LocalCliRuntime.Auto ? DetectRuntime() : runtime;

		var (command, args) = BuildLocalCliCommand(
			resolvedRuntime,
			OutputDirectory,
			LikeC4LocalServerResource.DefaultPort
		);

		var localResource = new LikeC4LocalServerResource(
			LikeC4VisualizationExtensions.ServerResourceName,
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

		return new LikeC4VisualizationBuilder(ApplicationBuilder, localBuilder, OutputDirectory);
	}

	static LikeC4LocalCliRuntime DetectRuntime()
	{
		// Try runtimes in order of preference.
		(LikeC4LocalCliRuntime Runtime, string Executable)[] candidates =
		[
			(LikeC4LocalCliRuntime.Npx, "npx"),
			(LikeC4LocalCliRuntime.Pnpm, "pnpm"),
			(LikeC4LocalCliRuntime.Yarn, "yarn"),
			(LikeC4LocalCliRuntime.Bun, "bun"),
		];

		foreach (var (candidate, executable) in candidates)
		{
			if (IsExecutableOnPath(executable))
				return candidate;
		}

		throw new DistributedApplicationException(
			"No supported JavaScript package manager was found on the system PATH. "
				+ "Install one of: Node.js (npx), pnpm, yarn, or bun, then retry. "
				+ "Alternatively, remove WithLocalCli() to use the Docker container (default)."
		);
	}

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
	/// Resolves the executable command and arguments for the given local CLI runtime.
	/// Internal and visible for testing.
	/// </summary>
	internal static (string Command, string[] Args) BuildLocalCliCommand(
		LikeC4LocalCliRuntime runtime,
		string outputDirectory,
		int port
	)
	{
		var portStr = $"{port}";
		return runtime switch
		{
			LikeC4LocalCliRuntime.Npx => ("npx", ["likec4", "serve", outputDirectory, "--port", portStr]),
			LikeC4LocalCliRuntime.Pnpm => ("pnpm", ["exec", "likec4", "serve", outputDirectory, "--port", portStr]),
			LikeC4LocalCliRuntime.Yarn => ("yarn", ["dlx", "likec4", "serve", outputDirectory, "--port", portStr]),
			LikeC4LocalCliRuntime.Bun => ("bunx", ["likec4", "serve", outputDirectory, "--port", portStr]),
			_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, $"Unsupported runtime: {runtime}"),
		};
	}
}
