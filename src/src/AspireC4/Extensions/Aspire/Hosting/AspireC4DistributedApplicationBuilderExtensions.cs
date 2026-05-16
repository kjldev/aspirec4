using System.ComponentModel;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.Lifecycle;
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

		var diagramOpts = new AspireC4DiagramOptions();
		configure?.Invoke(diagramOpts);

		var outputDir = ResolveOutputDirectory(builder.AppHostDirectory, diagramOpts.OutputDirectory);
		Directory.CreateDirectory(outputDir);
		var imageTag = diagramOpts.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;
		var hmrPortMode = HMRPortCompatibility.Resolve(imageTag);
		// Use the relay on Windows even in Configurable mode: Docker Desktop may fail to publish
		// the well-known port (24678) reliably due to Hyper-V port reservations or port-cleanup
		// races between container restarts. The relay owns port 24678 on the host side and
		// bridges incoming HMR connections to whatever dynamic port Docker happened to allocate.
		var useHmrRelay = hmrPortMode == HMRPortMode.FixedPort || OperatingSystem.IsWindows();
		var defaultViewId = string.IsNullOrWhiteSpace(diagramOpts.DefaultViewId) ? null : diagramOpts.DefaultViewId;

		builder
			.Services.AddOptions<ContainerWorkspaceOptions>()
			.Configure(runtime =>
			{
				runtime.HMRPortMode = hmrPortMode;
				runtime.UseHMRRelay = useHmrRelay;
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

				context.Args.Add("start");
				context.Args.Add(wsOpts.Value.ContainerServePath);

				if (!string.IsNullOrWhiteSpace(diagramOpts.Title))
				{
					context.Args.Add("--title");
					context.Args.Add($"\"{diagramOpts.Title}\"");
				}

				var dot = await Helpers.IsDotAvailableAsync(context.CancellationToken);
				if (dot)
					context.Args.Add("--use-dot");

				context.Args.Add("--port");
				context.Args.Add($"{LikeC4ServerResource.DefaultContainerServePort}");
				if (diagOpts.Value.DisableHMR)
					context.Args.Add("--no-react-hmr");
			})
			//.WithExternalHttpEndpoints()
			// Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
			.ExcludeFromLikeC4();

		if (!diagramOpts.DisableHMR)
		{
			serverBuilder
				.WithHttpEndpoint(
					// When using the relay, omit a fixed host port so Docker allocates a dynamic one.
					// The relay owns port 24678 on the host and bridges connections to the dynamic port.
					// Direct fixed-port mapping is only safe on non-Windows Configurable-mode images.
					port: useHmrRelay ? null : LikeC4ServerResource.DefaultContainerUpdatePort,
					targetPort: LikeC4ServerResource.DefaultContainerUpdatePort,
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

		AspireC4Resource aspirec4Resource = new(name, outputDir) { InnerResource = serverResource };

		return builder.AddResource(aspirec4Resource);
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
