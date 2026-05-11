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
	readonly Lock _debounceLock = new();
	CancellationTokenSource? _hmrRelayCts;
	TcpListener? _hmrRelayListener;
	readonly Lock _hmrRelayLock = new();

	public Task SubscribeAsync(
		IDistributedApplicationEventing eventing,
		DistributedApplicationExecutionContext executionContext,
		CancellationToken cancellationToken
	)
	{
		eventing.Subscribe<BeforeStartEvent>(
			async (evt, ct) =>
			{
				var syncContainerWorkspace = evt.Model.Resources.OfType<LikeC4ServerResource>().Any();

				if (executionContext.IsPublishMode)
				{
					await WriteC4FileAsync(evt.Model, syncContainerWorkspace, resetContainerWorkspace: false, ct);
					telemetry.PublishMode();
					return;
				}

				if (syncContainerWorkspace)
				{
					EnsureLegacyHostHmrPortAvailable();
					StartLegacyHmrRelay(evt.Model, ct);
				}

				await WriteC4FileAsync(evt.Model, syncContainerWorkspace, resetContainerWorkspace: true, ct);

				if (options.Value.HideFromDashboard)
				{
					SetupDashboardIntegration(evt.Model, options.Value.DashboardLinkDisplayName, ct);
				}

				// Fire-and-forget: watch for resource state changes and regenerate the file.
				// The ct is the application lifetime token; it is cancelled on shutdown.
				_ = WatchResourceStatesAsync(evt.Model, syncContainerWorkspace, ct);

				if (options.Value.IncludeAspireDashboardLinks)
				{
					_ = WatchDashboardUrlAsync(evt.Model, syncContainerWorkspace, ct);
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
	async Task WatchDashboardUrlAsync(
		DistributedApplicationModel appModel,
		bool syncContainerWorkspace,
		CancellationToken cancellationToken
	)
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
				ScheduleRegeneration(appModel, syncContainerWorkspace, cancellationToken);
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
	internal static string? SelectDashboardBaseUrl(IEnumerable<UrlSnapshot> urls)
	{
		var list = urls.Where(u => !u.IsInternal).ToList();

		// Primary strategy: endpoint name "https"/"http" identifies the browser frontend.
		// This matches how Aspire itself selects the public frontend URL in PopulateDashboardUrls.
		var rawUrl =
			list.FirstOrDefault(u => u.Name == "https")?.Url
			?? list.FirstOrDefault(u => u.Name == "http")?.Url
			// Fallbacks for future-proofing in case endpoint names change.
			?? list.FirstOrDefault(u =>
				u.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
			)?.Url
			?? list.FirstOrDefault(u =>
				u.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
			)?.Url;

		if (rawUrl is null || !Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed))
			return null;

		return $"{parsed.Scheme}://{parsed.Authority}";
	}

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
							? Task.FromResult(
								CommandResults.Failure("The architecture diagram is not yet available.")
							)
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
								));
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

	async Task WatchResourceStatesAsync(
		DistributedApplicationModel appModel,
		bool syncContainerWorkspace,
		CancellationToken cancellationToken
	)
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

				ScheduleRegeneration(appModel, syncContainerWorkspace, cancellationToken);
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

	void ScheduleRegeneration(
		DistributedApplicationModel appModel,
		bool syncContainerWorkspace,
		CancellationToken cancellationToken
	)
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
					await WriteC4FileAsync(
						appModel,
						syncContainerWorkspace,
						resetContainerWorkspace: false,
						cancellationToken
					);
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

	async Task WriteC4FileAsync(
		DistributedApplicationModel appModel,
		bool syncContainerWorkspace,
		bool resetContainerWorkspace,
		CancellationToken cancellationToken
	)
	{
		var opts = options.Value;

		telemetry.GeneratingLikeC4Model(appModel.Resources.Count);

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

		var model = LikeC4ModelBuilder.Build(
			[.. appModel.Resources],
			_resourceStates,
			opts.AutoIconsEnabled,
			opts.AutoIncludeAspireMetadata,
			opts.NormaliseMetadataBehaviour,
			opts.IconResolvers,
			opts.IncludeAspireDashboardLinks,
			_dashboardBaseUrl,
			configuration["AppHost:BrowserToken"],
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

		if (opts.ValidateBeforeStart)
		{
			await RunValidationAsync(outputDir, outputPath, cancellationToken);
		}

		// Sync the main model file to the container workspace.
		if (syncContainerWorkspace)
		{
			await WriteContainerWorkspaceFileAsync(opts.FileName, dsl, resetContainerWorkspace, cancellationToken);
		}

		// Copy and optionally sync additional user-provided DSL files.
		var bindMountedFiles = workspaceOptions.Value.BindMountedSourceFiles;
		foreach (var sourcePath in opts.AdditionalDslFiles)
		{
			var absoluteSource = Path.GetFullPath(sourcePath);
			if (!File.Exists(absoluteSource))
			{
				continue;
			}

			var destFileName = Path.GetFileName(absoluteSource);
			var destPath = Path.Combine(outputDir, destFileName);
			File.Copy(absoluteSource, destPath, overwrite: true);

			var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absoluteSource);
			telemetry.AdditionalDSLFileSynced(fileNameWithoutExtension);

			// Files covered by a container bind mount are already visible inside the container;
			// syncing them to the named volume would create duplicate definitions.
			if (syncContainerWorkspace && !bindMountedFiles.Contains(absoluteSource))
			{
				var additionalContent = await File.ReadAllTextAsync(destPath, cancellationToken);
				await WriteContainerWorkspaceFileAsync(
					fileNameWithoutExtension,
					additionalContent,
					resetContainerWorkspace: false,
					cancellationToken
				);
			}
		}

		telemetry.LikeC4ModelWritten(outputPath);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Validation is non-blocking; failures are logged as warnings only"
	)]
	async Task RunValidationAsync(string outputDir, string outputPath, CancellationToken cancellationToken)
	{
		try
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "npx",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			startInfo.ArgumentList.Add("likec4");
			startInfo.ArgumentList.Add("validate");
			startInfo.ArgumentList.Add("--json");
			startInfo.ArgumentList.Add("--no-layout");
			startInfo.ArgumentList.Add("--file");
			startInfo.ArgumentList.Add(outputPath);
			startInfo.ArgumentList.Add(outputDir);

			using var process = Process.Start(startInfo);
			if (process is null)
			{
				return;
			}

			var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
			await process.WaitForExitAsync(cancellationToken);

			if (string.IsNullOrWhiteSpace(stdout))
			{
				return;
			}

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
			{
				telemetry.LikeC4ValidationErrors(filteredErrors, totalErrors);
			}
			else
			{
				telemetry.LikeC4ValidationPassed();
			}
		}
		catch
		{
			// Validation is best-effort; never block startup.
		}
	}

	async Task WriteContainerWorkspaceFileAsync(
		string fileName,
		string dsl,
		bool resetContainerWorkspace,
		CancellationToken cancellationToken
	)
	{
		var workspace = workspaceOptions.Value;
		var containerFilePath = $"{LikeC4ServerResource.WorkspacePath}/{fileName}.c4";
		var syncScript = resetContainerWorkspace
			? "const fs=require('node:fs'); const path=require('node:path'); for (const entry of fs.readdirSync('/data')) { if (entry.endsWith('.c4')) fs.rmSync(path.join('/data', entry), { force: true }); } fs.writeFileSync(process.argv[1], fs.readFileSync(0));"
			: "const fs=require('node:fs');fs.writeFileSync(process.argv[1], fs.readFileSync(0));";

		var startInfo = new ProcessStartInfo
		{
			FileName = workspace.ContainerRuntimeExecutable,
			RedirectStandardError = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		startInfo.ArgumentList.Add("run");
		startInfo.ArgumentList.Add("--rm");
		startInfo.ArgumentList.Add("-i");
		startInfo.ArgumentList.Add("-v");
		startInfo.ArgumentList.Add($"{workspace.VolumeName}:{LikeC4ServerResource.WorkspacePath}");
		startInfo.ArgumentList.Add("--entrypoint");
		startInfo.ArgumentList.Add("node");
		startInfo.ArgumentList.Add(workspace.ContainerImageReference);
		startInfo.ArgumentList.Add("-e");
		startInfo.ArgumentList.Add(syncScript);
		startInfo.ArgumentList.Add(containerFilePath);

		using var process =
			Process.Start(startInfo)
			?? throw new DistributedApplicationException(
				$"Failed to start '{workspace.ContainerRuntimeExecutable}' to sync the LikeC4 workspace volume."
			);

		try
		{
			await process.StandardInput.WriteAsync(dsl.AsMemory(), cancellationToken);
			await process.StandardInput.FlushAsync(cancellationToken);
			process.StandardInput.Close();

			var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
			var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

			await process.WaitForExitAsync(cancellationToken);

			var stdout = await stdoutTask;
			var stderr = await stderrTask;

			if (process.ExitCode != 0)
			{
				var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
				throw new DistributedApplicationException(
					$"Failed to sync the LikeC4 workspace volume '{workspace.VolumeName}': {message.Trim()}"
				);
			}
		}
		catch (OperationCanceledException)
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}

			throw;
		}
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
