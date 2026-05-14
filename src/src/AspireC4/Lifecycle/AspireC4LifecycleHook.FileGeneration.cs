using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Generators;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	async Task WriteC4FileAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		var opts = options.Value;

		telemetry.GeneratingLikeC4Model(appModel.Resources.Count, [.. appModel.Resources.Select(m => m.Name)]);

		// Build a snapshot of external endpoint URLs (resource name → [(url, name)]) so that
		// the model builder uses the correct public-port URLs from resource snapshots.
		IReadOnlyDictionary<string, IReadOnlyList<(string Url, string Name)>>? resourceSnapshotUrls = null;
		if (!_resourceExternalUrls.IsEmpty)
		{
			resourceSnapshotUrls = _resourceExternalUrls.ToDictionary(
				kvp => kvp.Key,
				kvp => (IReadOnlyList<(string Url, string Name)>)[.. kvp.Value],
				StringComparer.OrdinalIgnoreCase
			);
		}

		var dashboardBrowserToken = ResolveAspireBrowserToken(configuration, options.Value);
		var model = ModelBuilder.Build(
			[.. appModel.Resources],
			_resourceStates,
			opts.AutoIconsEnabled,
			opts.AutoIncludeAspireMetadata,
			opts.NormaliseMetadataBehaviour,
			opts.IconResolvers,
			opts.IncludeAspireDashboardLinks,
			_dashboardBaseUrl,
			dashboardBrowserToken,
			opts.StateTagMap,
			resourceSnapshotUrls,
			opts.Strict
		);
		var dsl = LikeC4DSLGenerator.Generate(model, opts);

		var outputDir = Path.GetFullPath(opts.OutputDirectory);
		Directory.CreateDirectory(outputDir);

		var filename = opts.FileName;
		if (!filename.EndsWith(".c4", StringComparison.OrdinalIgnoreCase))
			filename += ".c4";

		var outputPath = Path.Combine(outputDir, filename);
		await File.WriteAllTextAsync(outputPath, dsl, cancellationToken);

		// Format the generated file in-place (best-effort) so the on-disk file is human-readable.
		if (opts.FormatGeneratedFile)
		{
			await RunFormatAsync(outputPath, outputDir, cancellationToken);
		}

		// Copy additional user-provided DSL files into the output directory so LikeC4
		// picks them up as part of the workspace.
		var additionalDestPaths = new List<string>();
		foreach (var sourcePath in opts.AdditionalDSLFiles)
		{
			var absoluteSource = Path.GetFullPath(sourcePath);
			if (!File.Exists(absoluteSource))
			{
				continue;
			}

			var destFileName = Path.GetFileName(absoluteSource);
			var destPath = Path.Combine(outputDir, destFileName);
			File.Copy(absoluteSource, destPath, overwrite: true);
			additionalDestPaths.Add(destPath);

			telemetry.AdditionalDSLFileSynced(Path.GetFileNameWithoutExtension(absoluteSource));
		}

		// Generate likec4.config.json when opted in (default).
		if (opts.GenerateConfigFile)
		{
			await WriteConfigFileAsync(opts, outputDir, cancellationToken);
		}

		// Validate after ALL files (generated model + additional DSL) are in the output directory.
		if (opts.ValidateBeforeStart)
		{
			await RunValidationAsync(outputDir, outputPath, additionalDestPaths, cancellationToken);
		}

		telemetry.LikeC4ModelWritten(outputPath);
	}

	/// <summary>
	/// Returns the executable and argument prefix for invoking <c>likec4</c> via the configured
	/// runtime. In Docker mode (<see cref="ContainerWorkspaceOptions.LocalCLIRuntime"/> is
	/// <see langword="null"/>), falls back to <c>npx</c> since the host still needs a JS runner
	/// for host-side operations such as format.
	/// </summary>
	(string Command, string[] Prefix) BuildCliPrefix() =>
		workspaceOptions.Value.LocalCLIRuntime is { } runtime
			? AspireC4Builder.BuildLikeC4CLIPrefix(runtime)
			: ("npx", ["likec4"]);

	/// <summary>
	/// Returns the container runtime executable name (<c>docker</c> or <c>podman</c>) by
	/// reading the <c>ASPIRE_CONTAINER_RUNTIME</c> environment variable.
	/// </summary>
	static string GetContainerRuntimeExecutable() =>
		Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") is { } r
		&& r.Equals("podman", StringComparison.OrdinalIgnoreCase)
			? "podman"
			: "docker";

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Validation is non-blocking; failures are logged only"
	)]
	async Task RunValidationAsync(
		string outputDir,
		string outputPath,
		IReadOnlyList<string> additionalFilePaths,
		CancellationToken cancellationToken
	)
	{
		telemetry.StartingLikeC4Validation();
		try
		{
			ProcessStartInfo startInfo;

			var bindMountSource = workspaceOptions.Value.ContainerBindMountSource;
			if (workspaceOptions.Value.LocalCLIRuntime is null && bindMountSource is not null)
			{
				// Docker / Podman mode: run validation inside a throwaway container using the
				// same image and bind mount as the main LikeC4 server.
				var imageRef = LikeC4ServerResource.GetImageReference(
					options.Value.ContainerImageTag ?? LikeC4ServerResource.DefaultTag
				);
				var containerPath = workspaceOptions.Value.ContainerServePath;
				var containerExe = GetContainerRuntimeExecutable();

				startInfo = new ProcessStartInfo
				{
					FileName = containerExe,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				};

				startInfo.ArgumentList.Add("run");
				startInfo.ArgumentList.Add("--rm");
				startInfo.ArgumentList.Add("-v");
				startInfo.ArgumentList.Add($"{bindMountSource}:{LikeC4ServerResource.WorkspacePath}");
				startInfo.ArgumentList.Add(imageRef);
				startInfo.ArgumentList.Add("validate");
				startInfo.ArgumentList.Add("--json");
				startInfo.ArgumentList.Add("--no-layout");
				startInfo.ArgumentList.Add(containerPath);
			}
			else
			{
				// Local CLI mode: invoke via the configured JS runtime (npx / pnpm / etc.).
				var (command, prefix) = BuildCliPrefix();
				startInfo = new ProcessStartInfo
				{
					FileName = command,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				};

				foreach (var arg in prefix)
					startInfo.ArgumentList.Add(arg);

				startInfo.ArgumentList.Add("validate");
				startInfo.ArgumentList.Add("--json");
				startInfo.ArgumentList.Add("--no-layout");

				if (await Helpers.IsDotAvailableAsync(cancellationToken))
					startInfo.ArgumentList.Add("--use-dot");

				startInfo.ArgumentList.Add("--file");
				startInfo.ArgumentList.Add(outputPath);

				foreach (var additionalPath in additionalFilePaths)
				{
					startInfo.ArgumentList.Add("--file");
					startInfo.ArgumentList.Add(additionalPath);
				}

				startInfo.ArgumentList.Add(outputDir);
			}

			using var process = Process.Start(startInfo);
			if (process is null)
				return;

			var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
			await process.WaitForExitAsync(cancellationToken);

			if (string.IsNullOrWhiteSpace(stdout))
				return;

			using var doc = JsonDocument.Parse(stdout);
			var root = doc.RootElement;

			var filteredErrors =
				root.TryGetProperty("stats", out var stats) && stats.TryGetProperty("filteredErrors", out var fe)
					? fe.GetInt32()
					: 0;

			var totalErrors =
				root.TryGetProperty("stats", out var statsTotal) && statsTotal.TryGetProperty("totalErrors", out var te)
					? te.GetInt32()
					: 0;

			if (filteredErrors > 0)
				telemetry.LikeC4ValidationFailed(filteredErrors, totalErrors);
			else
				telemetry.LikeC4ValidatedSuccessfully();
		}
		catch
		{
			// Validation is best-effort; never block startup.
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Formatting is non-blocking; failures are silently ignored"
	)]
	async Task RunFormatAsync(string outputPath, string outputDir, CancellationToken cancellationToken)
	{
		try
		{
			var (command, prefix) = BuildCliPrefix();
			var startInfo = new ProcessStartInfo
			{
				FileName = command,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = outputDir,
			};

			foreach (var arg in prefix)
				startInfo.ArgumentList.Add(arg);

			startInfo.ArgumentList.Add("format");
			startInfo.ArgumentList.Add("--files");
			startInfo.ArgumentList.Add(outputPath);
			startInfo.ArgumentList.Add(outputDir);

			using var process = Process.Start(startInfo);
			if (process is null)
				return;

			await process.WaitForExitAsync(cancellationToken);

			if (process.ExitCode == 0)
				telemetry.LikeC4FormatApplied();
		}
		catch (Exception ex)
		{
			// Formatting is best-effort; never block startup or regeneration.
			telemetry.FailedToRunFormatter(ex);
		}
	}

	static async Task WriteConfigFileAsync(
		AspireC4DiagramOptions opts,
		string outputDir,
		CancellationToken cancellationToken
	)
	{
		// Paths in the config are relative to the output directory. Because all referenced folders
		// (DSL folders, image-alias folders) are accessible via the single bind mount, these same
		// relative paths work correctly inside the container without any translation.
		var includePaths = opts
			.AdditionalDSLFolders.Select(absoluteFolder =>
				Path.GetRelativePath(outputDir, absoluteFolder).Replace('\\', '/')
			)
			.ToList();

		var aliases = opts.ImageAliases.ToDictionary(
			kvp => kvp.Key,
			kvp => Path.GetRelativePath(outputDir, kvp.Value).Replace('\\', '/'),
			StringComparer.OrdinalIgnoreCase
		);

		var config = LikeC4ConfigGenerator.Generate(
			"aspirec4",
			opts.Title,
			includePaths,
			aliases,
			opts.ConfigFileMetadata
		);
		var configPath = Path.Combine(outputDir, "likec4.config.json");
		await File.WriteAllTextAsync(configPath, config, cancellationToken);
	}
}
