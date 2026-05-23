using System.ComponentModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.Lifecycle;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for <see cref="IDistributedApplicationBuilder"/> to add LikeC4 live architecture diagram capabilities to an Aspire application.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4DistributedApplicationBuilderExtensions
{
	internal const string AspireC4ResourceName = "aspirec4";
	internal const string AspireC4ServerResourceSuffix = "-server";

	/// <summary>
	/// Adds a LikeC4 live architecture diagram to the Aspire application.
	/// </summary>
	/// <remarks>
	/// This registers a lifecycle hook that generates a <c>.c4</c> model file from the Aspire
	/// resource graph, and starts the official <c>ghcr.io/likec4/likec4</c> container as a
	/// sidecar that renders an interactive, hot-reloading diagram in the browser.
	/// <para>
	/// <b>Prerequisite:</b> Docker must be available (standard Aspire requirement). To use a
	/// local Node.js CLI instead, call <c>.WithLocalCli()</c> on the returned builder.
	/// </para>
	/// </remarks>
	/// <param name="builder">The Aspire distributed application builder.</param>
	/// <param name="name">Optional name of the LikeC4 visualization resource (used for the server container and diagram file).</param>
	/// <param name="port">Optional host port to bind the LikeC4 server's HTTP endpoint to. By default, no fixed host port is used and Docker assigns a dynamic port.</param>
	/// <param name="configure">Optional callback to configure <see cref="AspireC4DiagramOptions"/>.</param>
	/// <returns>An <see cref="IResourceBuilder{AspireC4Resource}"/> for further configuration.</returns>
	[AspireExport(
		Description = "Adds a LikeC4 live architecture diagram to the Aspire application.",
		RunSyncOnBackgroundThread = true
	)]
	public static IResourceBuilder<AspireC4Resource> AddAspireC4(
		this IDistributedApplicationBuilder builder,
		[ResourceName] string? name = null,
		int? port = null,
		Action<AspireC4DiagramOptions>? configure = null
	)
	{
		if (string.IsNullOrWhiteSpace(name))
			name = AspireC4ResourceName;

		ArgumentNullException.ThrowIfNull(builder);

		builder
			.Services.AddOptions<AspireC4DiagramOptions>()
			.BindConfiguration(AspireC4DiagramOptions.SectionName)
			.Configure(opts =>
			{
				configure?.Invoke(opts);
				opts.OutputDirectory = ResolveOutputDirectory(builder.AppHostDirectory, opts.OutputDirectory);
			});

		AspireC4DiagramOptions diagramOpts = new();
		configure?.Invoke(diagramOpts);

		var outputDir = ResolveOutputDirectory(builder.AppHostDirectory, diagramOpts.OutputDirectory);
		Directory.CreateDirectory(outputDir);
		var imageTag = diagramOpts.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;
		var hmrPortMode = HMRPortCompatibility.Resolve(imageTag);
		var resolvedHmrPort = diagramOpts.HMRPort ?? LikeC4ServerResource.DefaultContainerHMRPort;
		var defaultViewId = string.IsNullOrWhiteSpace(diagramOpts.DefaultViewId) ? null : diagramOpts.DefaultViewId;

		// Only create a version probe when using "latest" with version checking enabled.
		// A pinned tag always has a known HMR mode; "latest" requires a probe to discover it.
		var needsVersionProbe =
			string.Equals(imageTag, LikeC4ServerResource.DefaultTag, StringComparison.OrdinalIgnoreCase)
			&& diagramOpts.CheckLatestImageVersion;

		// Pre-complete the TCS when no probe is needed so WithArgs can proceed without waiting.
		var hmrPortModeTcs = new TaskCompletionSource<HMRPortMode>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (!needsVersionProbe)
			hmrPortModeTcs.TrySetResult(hmrPortMode);

		builder.Services.AddSingleton(hmrPortModeTcs);

		// Always use the same port on both the host and inside the container for HMR.
		// In LikeC4 v1.57+, --hmr-port sets server.hmr.port — the port Vite BINDS to inside
		// the container. Vite also advertises this same port to browsers as the HMR WebSocket
		// target (no separate clientPort option exists). Docker must therefore map the SAME port
		// on the host so the browser's connection to host:PORT reaches container:PORT correctly.
		// Dynamic (null) host ports cannot work here: if Docker maps host:DYNAMIC → container:24678
		// but Vite is told --hmr-port DYNAMIC it binds to container:DYNAMIC, which Docker doesn't
		// forward, breaking the HMR WebSocket connection entirely.
		int? hmrHostPort = diagramOpts.HMRPort ?? resolvedHmrPort;

		builder
			.Services.AddOptions<ContainerWorkspaceOptions>()
			.Configure(runtime =>
			{
				runtime.HMRPortMode = hmrPortMode;
				runtime.ResolvedHMRPort = resolvedHmrPort;
			});

		builder.Services.AddEventingSubscriber<AspireC4LifecycleHook>();
		builder.Services.AddAspireC4LifecycleHookTelemetry();

		LikeC4ServerResource serverResource = new(name + AspireC4ServerResourceSuffix);
		builder.Eventing.Subscribe<BeforeStartEvent>(
			(_, _) =>
			{
				var otlpExporterAnnotations = serverResource.Annotations.OfType<OtlpExporterAnnotation>().ToArray();
				foreach (var annotation in otlpExporterAnnotations)
					serverResource.Annotations.Remove(annotation);

				return Task.CompletedTask;
			}
		);

		var serverBuilder = builder
			.AddResource(serverResource)
			.WithImage(LikeC4ServerResource.DefaultImage)
			.WithImageTag(imageTag)
			.WithImageRegistry(LikeC4ServerResource.DefaultRegistry)
			.WithImagePullPolicy(ImagePullPolicy.Always)
			.WithHttpEndpoint(
				port: port,
				targetPort: LikeC4ServerResource.DefaultContainerServePort,
				name: LikeC4ServerResource.HttpEndpointName
			)
			.WithUrlForEndpoint(
				LikeC4ServerResource.HttpEndpointName,
				opts =>
				{
					opts.DisplayText = "View LikeC4 Diagram";
					opts.DisplayOrder = 0;
					opts.DisplayLocation = UrlDisplayLocation.SummaryAndDetails;
					opts.Url = defaultViewId != null ? $"/view/{defaultViewId}" : "/";
				}
			)
			.WithHttpHealthCheck("/", statusCode: 200, endpointName: LikeC4LocalServerResource.HttpEndpointName)
			// Register container args as a callback so they are evaluated at container-start
			// time (after BeforeStartEvent has set ContainerServePath). DisableHMR is read
			// from AspireC4DiagramOptions so it respects configuration overrides at runtime.
			.WithArgs(async context =>
			{
				var wsOpts = context.ExecutionContext.ServiceProvider.GetRequiredService<
					IOptions<ContainerWorkspaceOptions>
				>();
				var diagOpts = context.ExecutionContext.ServiceProvider.GetRequiredService<
					IOptions<AspireC4DiagramOptions>
				>();
				var hmrTcs = context.ExecutionContext.ServiceProvider.GetRequiredService<
					TaskCompletionSource<HMRPortMode>
				>();

				context.Args.Add("start");
				context.Args.Add(wsOpts.Value.ContainerServePath);

				if (!string.IsNullOrWhiteSpace(diagramOpts.Title))
				{
					context.Args.Add("--title");
					context.Args.Add($"\"{diagramOpts.Title}\"");
				}

				var useDot =
					diagOpts.Value.UseDotIfAvailable && await Helpers.IsDotAvailableAsync(context.CancellationToken);
				if (useDot)
					context.Args.Add("--use-dot");

				context.Args.Add("--port");
				context.Args.Add(LikeC4ServerResource.DefaultContainerServePort);

				var hmrMode = await hmrTcs.Task.WaitAsync(context.CancellationToken);
				if (!diagOpts.Value.DisableHMR && hmrMode == HMRPortMode.Configurable)
				{
					// Pass the container-internal HMR port. Because host and container use the
					// same port (symmetric mapping), this value is also what the browser connects to.
					context.Args.Add("--hmr-port");
					context.Args.Add(wsOpts.Value.ResolvedHMRPort);
				}

				if (diagOpts.Value.DisableHMR)
					context.Args.Add("--no-react-hmr");
			})
			// Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
			// Set a stable DSL identifier equal to the base name so that the element, when explicitly
			// included by a consumer (e.g. via ConfigureTestHost), is always emitted as "aspirec4"
			// regardless of the "-server" suffix on the Aspire resource name.
			.ExcludeFromLikeC4()
			.WithAnnotation(new LikeC4DslIdAnnotation(name))
			.ExcludeFromManifest();

		if (!diagramOpts.DisableHMR)
		{
			serverBuilder
				.WithHttpEndpoint(
					port: hmrHostPort,
					targetPort: resolvedHmrPort,
					name: LikeC4ServerResource.HMREndpointName
				)
				.WithUrlForEndpoint(
					LikeC4ServerResource.HMREndpointName,
					opts =>
					{
						opts.DisplayText = "LikeC4 HMR Endpoint";
						opts.DisplayOrder = 1;
						opts.DisplayLocation = UrlDisplayLocation.DetailsOnly;
					}
				);

			if (OperatingSystem.IsWindows())
			{
				serverBuilder
					// Required on Windows/Docker Desktop: inotify events do not propagate from the host
					// filesystem into the container, so chokidar must fall back to polling to detect
					// changes to the generated .c4 file.
					.WithEnvironment("CHOKIDAR_USEPOLLING", "1")
					.WithEnvironment("CHOKIDAR_INTERVAL", "200");
			}
		}

		AspireC4Resource aspirec4Resource = new(name, outputDir)
		{
			InnerResource = serverResource,
			HMRPortModeTcs = hmrPortModeTcs,
		};

		if (needsVersionProbe)
		{
			LikeC4VersionProbeResource probeResource = new(name + "-version-probe");
			var probeBuilder = builder
				.AddResource(probeResource)
				.WithImage(LikeC4ServerResource.DefaultImage)
				.WithImageTag(imageTag)
				.WithImageRegistry(LikeC4ServerResource.DefaultRegistry)
				.WithImagePullPolicy(ImagePullPolicy.Always)
				.WithArgs("--version")
				.ExcludeFromLikeC4()
				.ExcludeFromManifest()
				.WithInitialState(
					new CustomResourceSnapshot
					{
						ResourceType = "Container",
						IsHidden = true,
						Properties = [],
					}
				);

			serverBuilder.WaitForCompletion(probeBuilder);
			aspirec4Resource.VersionProbeResource = probeResource;
		}

		return builder
			.AddResource(aspirec4Resource)
			.ExcludeFromLikeC4()
			.ExcludeFromManifest()
			.WithInitialState(
				new CustomResourceSnapshot
				{
					// Shown as a container type since it IS backed by a container (or local CLI).
					// URLs, state, and properties are forwarded from the inner resource at runtime
					// by ForwardInnerResourceStateAsync so this entry stays accurate.
					ResourceType = "Container",
					IsHidden = false,
					Properties = [],
				}
			);
	}

	static string ResolveOutputDirectory(string appHostDirectory, string outputDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

		return Path.GetFullPath(
			Path.IsPathRooted(outputDirectory) ? outputDirectory : Path.Combine(appHostDirectory, outputDirectory)
		);
	}
}
