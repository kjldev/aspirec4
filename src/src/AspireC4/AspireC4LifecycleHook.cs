using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Aspire eventing subscriber that generates the LikeC4 <c>.c4</c> model file before the
/// application starts, and dynamically regenerates it whenever a resource changes state at runtime.
/// </summary>
sealed class AspireC4LifecycleHook(
	IOptions<AspireC4DiagramOptions> options,
	IOptions<LikeC4ContainerWorkspaceOptions> workspaceOptions,
	ResourceNotificationService resourceNotificationService,
	IAspireC4LifecycleHookTelemetry telemetry,
	IConfiguration configuration
) : IDistributedApplicationEventingSubscriber, IDisposable
{
	// Well-known Aspire resource name for the dashboard process.
	const string AspireDashboardResourceName = "aspire-dashboard";

	readonly ConcurrentDictionary<string, LikeC4ResourceState> _resourceStates = new(StringComparer.OrdinalIgnoreCase);

	// Maps resource name → externally-accessible endpoint URLs (from resource snapshots).
	// Populated by WatchResourceStatesAsync; used by WriteC4FileAsync to pass the correct
	// public-port URLs to LikeC4ModelBuilder.Build() instead of reading AllocatedEndpoint.
	readonly ConcurrentDictionary<string, ImmutableArray<(string Url, string Name)>> _resourceExternalUrls = new(
		StringComparer.OrdinalIgnoreCase
	);

	// Discovered at runtime once the aspire-dashboard resource starts.
	volatile string? _dashboardBaseUrl;

	// Discovered at runtime once the LikeC4 server is Running.
	// Contains the full public URL (scheme + host + port + path) from the server's snapshot,
	// e.g. "http://localhost:51234/view/index". Used by the dashboard command handler.
	volatile string? _diagramUrl;

	// Debounce: cancels any pending delayed write when a new state change arrives.
	CancellationTokenSource? _debounceCts;
	CancellationTokenSource? _hmrRelayCts;
	TcpListener? _hmrRelayListener;

#if NET9_0_OR_GREATER
	readonly Lock _debounceLock = new();
	readonly Lock _hmrRelayLock = new();
#else
	readonly object _debounceLock = new();
	readonly object _hmrRelayLock = new();
#endif

	public Task SubscribeAsync(
		IDistributedApplicationEventing eventing,
		DistributedApplicationExecutionContext executionContext,
		CancellationToken cancellationToken
	)
	{
		eventing.Subscribe<BeforeStartEvent>(
			async (evt, ct) =>
			{
				var serverResource = evt.Model.Resources.OfType<LikeC4ServerResource>().FirstOrDefault();

				if (executionContext.IsPublishMode)
				{
					await WriteC4FileAsync(evt.Model, ct);
					telemetry.PublishMode();
					return;
				}

				if (serverResource is not null)
				{
					SetupContainerBindMount(evt.Model, serverResource);

					if (!options.Value.DisableHMR)
					{
						EnsureLegacyHostHmrPortAvailable();
						StartLegacyHmrRelay(evt.Model, ct);
					}
				}

				await WriteC4FileAsync(evt.Model, ct);

				if (options.Value.HideFromDashboard)
				{
					SetupDashboardIntegration(evt.Model, options.Value.DashboardLinkDisplayName, ct);
				}

				// Fire-and-forget: watch for resource state changes and regenerate the file.
				// The ct is the application lifetime token; it is cancelled on shutdown.
				_ = WatchResourceStatesAsync(evt.Model, ct);

				if (options.Value.IncludeAspireDashboardLinks)
				{
					_ = WatchDashboardUrlAsync(evt.Model, ct);
				}
			}
		);

		return Task.CompletedTask;
	}

	// ── Background watcher ────────────────────────────────────────────────────

	/// <summary>
	/// Watches for the Aspire dashboard resource to start, captures its base URL, and triggers
	/// a diagram regeneration so that dashboard deep-links are injected into the generated file.
	/// </summary>
	async Task WatchDashboardUrlAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				if (notification.Resource.Name != AspireDashboardResourceName)
					continue;
				if (notification.Snapshot.State?.Text != KnownResourceStates.Running)
					continue;

				var baseUrl = SelectDashboardBaseUrl(notification.Snapshot.Urls);
				if (baseUrl is null)
					continue;

				// Only regenerate if the URL is genuinely new.
				if (_dashboardBaseUrl == baseUrl)
					break;

				_dashboardBaseUrl = baseUrl;
				telemetry.DashboardUrlDiscovered(baseUrl);
				ScheduleRegeneration(appModel, cancellationToken);
				break;
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// Selects the browser-facing Aspire dashboard base URL (<c>scheme://host:port</c>) from
	/// a resource snapshot URL list, mirroring Aspire's own <c>PopulateDashboardUrls</c> logic.
	/// </summary>
	/// <remarks>
	/// The <c>aspire-dashboard</c> resource exposes multiple non-internal HTTPS endpoints when
	/// TLS is configured — most notably the browser frontend (<c>Name = "https"</c>) and the
	/// OTLP HTTP endpoint (<c>Name = "otlp-http"</c>). Filtering by endpoint name, not by
	/// URL scheme alone, ensures the correct browser-facing URL is always selected first.
	/// </remarks>
	public static string? SelectDashboardBaseUrl(IEnumerable<UrlSnapshot> urls)
	{
		var list = urls.Where(u => !u.IsInternal).ToList();

		// Primary strategy: endpoint name "https"/"http" identifies the browser frontend.
		// This matches how Aspire itself selects the public frontend URL in PopulateDashboardUrls.
		var rawUrl =
			list.FirstOrDefault(u => u.Name == "https")?.Url
			?? list.FirstOrDefault(u => u.Name == "http")?.Url
			// Fallbacks for future-proofing in case endpoint names change.
			?? list.FirstOrDefault(u => u.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))?.Url
			?? list.FirstOrDefault(u => u.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))?.Url;

		return rawUrl is null || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed)
			? null
			: $"{parsed.Scheme}://{parsed.Authority}";
	}

	/// <summary>
	/// Returns the Aspire browser token from configuration when
	/// <see cref="AspireC4DiagramOptions.IncludeAspireTokenInDashboardLinks"/> is <see langword="true"/>;
	/// otherwise <see langword="null"/> (token is suppressed by default).
	/// </summary>
	internal static string? ResolveAspireBrowserToken(IConfiguration configuration, AspireC4DiagramOptions options) =>
		options.IncludeAspireTokenInDashboardLinks ? configuration["AppHost:BrowserToken"] : null;

	// ── Dashboard integration (hide & surface URL/command on project resources) ──

	void SetupDashboardIntegration(
		DistributedApplicationModel appModel,
		string displayName,
		CancellationToken cancellationToken
	)
	{
		var serverResource = appModel.Resources.FirstOrDefault(r =>
			r is LikeC4ServerResource or LikeC4LocalServerResource
		);

		if (serverResource is null)
		{
			return;
		}

		// Add a command annotation to every project resource so the diagram link appears
		// in the Aspire dashboard on those rows. The URL is populated once the server starts;
		// until then, the command is disabled. URL injection into snapshots is handled by
		// InjectDiagramUrlWhenLikeC4RunsAsync, which uses the correct public URL from the server's snapshot.
		foreach (var projectResource in appModel.Resources.OfType<ProjectResource>())
		{
			// ResourceCommandAnnotation: re-evaluated on every state update via UpdateCommands.
			// The executeCommand callback returns a Markdown link that the dashboard renders
			// as a clickable dialog (displayImmediately: true).
			projectResource.Annotations.Add(
				new ResourceCommandAnnotation(
					name: "likec4-architecture-diagram",
					displayName: displayName,
					updateState: _ =>
						_diagramUrl is not null ? ResourceCommandState.Enabled : ResourceCommandState.Disabled,
					executeCommand: _ =>
					{
						var url = _diagramUrl;
						return url is null
							? Task.FromResult(CommandResults.Failure("The architecture diagram is not yet available."))
							: Task.FromResult(
								CommandResults.Success(
									$"[Open {displayName}]({url})",
									new CommandResultData
									{
										Value = $"[Open {displayName}]({url})",
										Format = CommandResultFormat.Markdown,
										DisplayImmediately = true,
									}
								)
							);
					},
					displayDescription: "View the live LikeC4 architecture diagram",
					parameter: null,
					confirmationMessage: null,
					iconName: "PlugConnected",
					iconVariant: IconVariant.Filled,
					isHighlighted: false
				)
			);
		}

		// Fire-and-forget background tasks.
		_ = KeepServerHiddenAsync(serverResource, cancellationToken);
		_ = InjectDiagramUrlWhenLikeC4RunsAsync(appModel, serverResource, displayName, cancellationToken);
	}

	/// <summary>
	/// Watches for the LikeC4 server resource to become Running, then directly injects
	/// its URL into each project resource's snapshot so the link appears immediately —
	/// even when the server starts after the project resource's endpoints are already allocated.
	/// Also caches the URL into <see cref="_diagramUrl"/> so the dashboard command handler
	/// can use the correct public URL (with scheme, public port, and path).
	/// </summary>
	async Task InjectDiagramUrlWhenLikeC4RunsAsync(
		DistributedApplicationModel appModel,
		IResource serverResource,
		string displayName,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				if (notification.Resource.Name != serverResource.Name)
					continue;
				if (notification.Snapshot.State?.Text != KnownResourceStates.Running)
					continue;

				var url = notification
					.Snapshot.Urls.FirstOrDefault(u => u.Name == LikeC4ServerResource.HttpEndpointName && !u.IsInternal)
					?.Url;

				if (url is null)
					continue;

				// Cache the URL so the command handler can use the correct public URL.
				_diagramUrl = url;

				var capturedUrl = url;
				foreach (var resource in appModel.Resources.OfType<ProjectResource>())
				{
					await resourceNotificationService.PublishUpdateAsync(
						resource,
						s =>
							// Avoid duplicates on repeated Running notifications.
							s.Urls.Any(u => u.Name == "architecture-diagram")
								? s
								: (
									s with
									{
										Urls = s.Urls.Add(
											new UrlSnapshot(
												Name: "architecture-diagram",
												Url: capturedUrl,
												IsInternal: false
											)
											{
												DisplayProperties = new UrlDisplayPropertiesSnapshot(displayName),
											}
										),
									}
								)
					);
				}

				break; // Only inject once — the URL persists in the snapshot.
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// Continuously re-publishes <c>IsHidden = true</c> on the LikeC4 server resource
	/// whenever DCP resets it to visible, ensuring it stays off the dashboard resource list.
	/// </summary>
	async Task KeepServerHiddenAsync(IResource serverResource, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				if (notification.Resource.Name != serverResource.Name)
					continue;
				if (notification.Snapshot.IsHidden)
					continue;

				await resourceNotificationService.PublishUpdateAsync(serverResource, s => s with { IsHidden = true });
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	async Task WatchResourceStatesAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		try
		{
			var visibleNames = LikeC4ModelBuilder.GetVisibleResourceNames([.. appModel.Resources]);

			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				// Ignore resources that aren't shown in the diagram.
				if (!visibleNames.Contains(notification.Resource.Name))
				{
					continue;
				}

				// Always capture external URLs from the snapshot (updates on every notification
				// so that once endpoints are allocated the correct public-port URLs are stored).
				var externalUrls = notification
					.Snapshot.Urls.Where(u =>
						!u.IsInternal
						&& (
							u.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
							|| u.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
						)
					)
					.Select(u => (u.Url, Name: u.Name ?? "endpoint"))
					.ToImmutableArray();

				if (!externalUrls.IsEmpty)
				{
					_resourceExternalUrls[notification.Resource.Name] = externalUrls;
				}

				var newState = MapAspireState(notification.Snapshot);

				if (_resourceStates.TryGetValue(notification.Resource.Name, out var current) && current == newState)
				{
					continue;
				}

				_resourceStates[notification.Resource.Name] = newState;
				telemetry.ResourceStateChanged(notification.Resource.Name, newState.ToString());

				ScheduleRegeneration(appModel, cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on application shutdown.
		}
#pragma warning disable CA1031 // intentional: watcher must not crash the host process
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	void ScheduleRegeneration(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		CancellationTokenSource newCts;
		lock (_debounceLock)
		{
			_debounceCts?.Cancel();
			_debounceCts?.Dispose();
			newCts = _debounceCts = new CancellationTokenSource();
		}

		_ = Task.Run(
			async () =>
			{
				try
				{
					await Task.Delay(TimeSpan.FromMilliseconds(300), newCts.Token);
					telemetry.RegeneratingDiagramDueToStateChange();
					await WriteC4FileAsync(appModel, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					// Debounced — a newer state change superseded this one.
				}
			},
			CancellationToken.None
		);
	}

	// ── File generation ───────────────────────────────────────────────────────

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
		var model = LikeC4ModelBuilder.Build(
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
			resourceSnapshotUrls
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
	/// runtime. In Docker mode (<see cref="LikeC4ContainerWorkspaceOptions.LocalCLIRuntime"/> is
	/// <see langword="null"/>), falls back to <c>npx</c> since the host still needs a JS runner
	/// for host-side operations such as format.
	/// </summary>
	(string Command, string[] Prefix) BuildCliPrefix() =>
		workspaceOptions.Value.LocalCLIRuntime is { } runtime
			? AspireC4Builder.BuildLikeC4CliPrefix(runtime)
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

	// ── Container bind-mount setup ────────────────────────────────────────────

	/// <summary>
	/// Computes the single common-ancestor bind mount for the container, adds it to the
	/// <see cref="LikeC4ServerResource"/>, populates <see cref="LikeC4ContainerWorkspaceOptions.ContainerServePath"/>,
	/// and appends the <c>likec4 start</c> command-line arguments.
	/// </summary>
	void SetupContainerBindMount(DistributedApplicationModel _, LikeC4ServerResource serverResource)
	{
		var opts = options.Value;
		var outputDir = Path.GetFullPath(opts.OutputDirectory);

		// Collect all host-side directory paths that must be visible inside the container.
		var allPaths = new List<string> { outputDir };
		allPaths.AddRange(opts.AdditionalDSLFolders.Select(Path.GetFullPath));
		allPaths.AddRange(opts.ImageAliases.Values.Select(Path.GetFullPath));

		var commonAncestor = ComputeCommonAncestor(allPaths);
		var normalizedSource = AspireC4Builder.NormalizeBindMountPath(commonAncestor);

		serverResource.Annotations.Add(
			new ContainerMountAnnotation(
				normalizedSource,
				LikeC4ServerResource.WorkspacePath,
				ContainerMountType.BindMount,
				isReadOnly: true
			)
		);

		// Compute the container path for the output directory: /data/{rel-from-ancestor-to-outputDir}
		var relOutputDir = Path.GetRelativePath(commonAncestor, outputDir).Replace('\\', '/');
		var servePath = $"{LikeC4ServerResource.WorkspacePath}/{relOutputDir}";
		// ContainerServePath is read by the WithArgs callback registered at configure time in AddAspireC4.
		workspaceOptions.Value.ContainerServePath = servePath;
		// ContainerBindMountSource is used by RunValidationAsync when running docker-mode validation.
		workspaceOptions.Value.ContainerBindMountSource = normalizedSource;
	}

	/// <summary>
	/// Returns the common ancestor directory of all provided absolute paths.
	/// All paths must reside on the same drive (Windows) or under the same root (Unix).
	/// </summary>
	internal static string ComputeCommonAncestor(IReadOnlyList<string> absolutePaths)
	{
		if (absolutePaths.Count == 0)
			throw new ArgumentException("At least one path is required.", nameof(absolutePaths));

		var sep = Path.DirectorySeparatorChar;

		// Normalize each path: absolute + trailing separator for reliable prefix matching.
		static string WithTrailingSep(string p) =>
			Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;

		var first = WithTrailingSep(absolutePaths[0]);
		var commonPrefix = first;
		var pathRoot = Path.GetPathRoot(first)!;

		foreach (var path in absolutePaths.Skip(1))
		{
			var normalized = WithTrailingSep(path);

			while (!normalized.StartsWith(commonPrefix, StringComparison.OrdinalIgnoreCase))
			{
				if (commonPrefix == pathRoot)
					throw new InvalidOperationException(
						"The provided paths share no common ancestor directory. "
							+ "Ensure all paths used by AddAspireC4 reside on the same drive."
					);

				// Back up one directory level.
				var trimmed = commonPrefix[..^1]; // Remove trailing separator
				var idx = trimmed.LastIndexOf(sep);
				commonPrefix = idx >= 0 ? trimmed[..(idx + 1)] : pathRoot;
			}
		}

		// Return without trailing separator, unless that would yield an invalid rooted path.
		var result = commonPrefix.TrimEnd(sep);
		return Path.IsPathRooted(result) ? result : commonPrefix;
	}

	void EnsureLegacyHostHmrPortAvailable()
	{
		if (!workspaceOptions.Value.UseHMRRelay)
		{
			return;
		}

		for (var attempt = 0; attempt < 15; attempt++)
		{
			Socket? socket = null;

			try
			{
				socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				socket.Bind(new IPEndPoint(IPAddress.Loopback, LikeC4ServerResource.DefaultContainerUpdatePort));
				return;
			}
			catch (SocketException) when (attempt < 14)
			{
				Thread.Sleep(TimeSpan.FromSeconds(1));
			}
			catch (SocketException ex)
			{
				telemetry.HMRPortUnavailable(LikeC4ServerResource.DefaultContainerUpdatePort, ex.Message);
				throw new DistributedApplicationException(
					$"LikeC4 live updates require host port {LikeC4ServerResource.DefaultContainerUpdatePort} to be free so the Vite HMR endpoint can be published. Stop the process using that port, or remove the LikeC4 visualization sidecar before starting the app."
				);
			}
			finally
			{
				socket?.Dispose();
			}
		}
	}

	void StartLegacyHmrRelay(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		if (!workspaceOptions.Value.UseHMRRelay)
		{
			return;
		}

		lock (_hmrRelayLock)
		{
			if (_hmrRelayListener is not null)
			{
				return;
			}

			_hmrRelayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			// On Windows, bind to all IPv4 interfaces (0.0.0.0) so the relay accepts
			// connections regardless of how the OS resolves "localhost" in the browser.
			var listenerAddress = OperatingSystem.IsWindows() ? IPAddress.Any : IPAddress.Loopback;
			_hmrRelayListener = new TcpListener(listenerAddress, LikeC4ServerResource.DefaultContainerUpdatePort);
			_hmrRelayListener.Start();

			_ = RunHmrRelayAsync(appModel, _hmrRelayListener, _hmrRelayCts.Token);
		}
	}

	static async Task RunHmrRelayAsync(
		DistributedApplicationModel appModel,
		TcpListener listener,
		CancellationToken cancellationToken
	)
	{
		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				TcpClient inbound;
				try
				{
					inbound = await listener.AcceptTcpClientAsync(cancellationToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				_ = RelayHmrConnectionAsync(appModel, inbound, cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on application shutdown.
		}
	}

	static async Task RelayHmrConnectionAsync(
		DistributedApplicationModel appModel,
		TcpClient inbound,
		CancellationToken cancellationToken
	)
	{
		using var inboundConnection = inbound;
		var (address, port) = await WaitForAllocatedHmrEndpointAsync(appModel, cancellationToken);

		using var outboundConnection = new TcpClient();
		await outboundConnection.ConnectAsync(address, port, cancellationToken);

		await using var inboundStream = inboundConnection.GetStream();
		await using var outboundStream = outboundConnection.GetStream();

#pragma warning disable CA2025 // Both relay tasks are awaited before the streams are disposed.
		Task PumpToTargetAsync() => PumpAsync(inboundStream, outboundStream, cancellationToken);
		Task PumpFromTargetAsync() => PumpAsync(outboundStream, inboundStream, cancellationToken);
#pragma warning restore CA2025

		var toTarget = PumpToTargetAsync();
		var fromTarget = PumpFromTargetAsync();

		try
		{
			await Task.WhenAny(toTarget, fromTarget);
			inboundConnection.Close();
			outboundConnection.Close();
			await Task.WhenAll(toTarget, fromTarget);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Expected when either side closes the socket.
		}
		catch (IOException)
		{
			// Expected when the browser or the upstream HMR endpoint disconnects.
		}
		catch (SocketException)
		{
			// Expected when the browser or the upstream HMR endpoint disconnects.
		}
		catch (ObjectDisposedException)
		{
			// Expected during shutdown or connection teardown.
		}
	}

	static async Task PumpAsync(Stream source, Stream destination, CancellationToken cancellationToken)
	{
		await source.CopyToAsync(destination, cancellationToken);
		await destination.FlushAsync(cancellationToken);
	}

	static async Task<(string Address, int Port)> WaitForAllocatedHmrEndpointAsync(
		DistributedApplicationModel appModel,
		CancellationToken cancellationToken
	)
	{
		for (var attempt = 0; attempt < 300; attempt++)
		{
			var endpoint = appModel
				.Resources.OfType<LikeC4ServerResource>()
				.SelectMany(resource => resource.Annotations.OfType<EndpointAnnotation>())
				.FirstOrDefault(annotation => annotation.Name == LikeC4ServerResource.HmrEndpointName)
				?.AllocatedEndpoint;

			if (endpoint is { Address.Length: > 0, Port: > 0 })
			{
				var address = endpoint.Address switch
				{
					"0.0.0.0" => IPAddress.Loopback.ToString(),
					_ => endpoint.Address,
				};

				if (
					!(
						address == IPAddress.Loopback.ToString()
						&& endpoint.Port == LikeC4ServerResource.DefaultContainerUpdatePort
					)
				)
				{
					return (address, endpoint.Port);
				}
			}

			await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
		}

		throw new DistributedApplicationException(
			"The LikeC4 HMR endpoint was not allocated before the browser connected."
		);
	}

	// ── IDisposable ───────────────────────────────────────────────────────────

	public void Dispose()
	{
		lock (_hmrRelayLock)
		{
			_hmrRelayListener?.Dispose();
			_hmrRelayCts?.Dispose();
			_hmrRelayCts = null;
			_hmrRelayListener = null;
		}

		lock (_debounceLock)
		{
			_debounceCts?.Cancel();
			_debounceCts?.Dispose();
			_debounceCts = null;
		}
	}

	// ── State mapping ─────────────────────────────────────────────────────────

	static LikeC4ResourceState MapAspireState(CustomResourceSnapshot snapshot)
	{
		var style = snapshot.State?.Style;
		var text = snapshot.State?.Text;

		// Style takes semantic precedence (Aspire sets it based on exit code / health).
		if (string.Equals(style, "error", StringComparison.OrdinalIgnoreCase))
		{
			return LikeC4ResourceState.Error;
		}

		if (string.Equals(style, "warn", StringComparison.OrdinalIgnoreCase))
		{
			return LikeC4ResourceState.Failed;
		}

		// Use string literals — KnownResourceStates members are static readonly, not const.
		return text switch
		{
			"Running" => LikeC4ResourceState.Running,
			"Starting" or "Waiting" => LikeC4ResourceState.Starting,
			"Stopping" => LikeC4ResourceState.Stopping,
			"FailedToStart" or "RuntimeUnhealthy" => LikeC4ResourceState.Error,
			// Use ExitCode to distinguish a clean stop (0 / unknown) from a crash (non-zero).
			// This handles cases where Aspire does not set the "success" style reliably.
			"Exited" => snapshot.ExitCode is null or 0 ? LikeC4ResourceState.Exited : LikeC4ResourceState.Failed,
			"Finished" => LikeC4ResourceState.Exited,
			_ => LikeC4ResourceState.Unknown,
		};
	}
}
