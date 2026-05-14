using System.Net;
using System.Net.Sockets;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// Computes the single common-ancestor bind mount for the container, adds it to the
	/// <see cref="LikeC4ServerResource"/>, populates <see cref="ContainerWorkspaceOptions.ContainerServePath"/>,
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
				.FirstOrDefault(annotation => annotation.Name == LikeC4ServerResource.HMREndpointName)
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
}
