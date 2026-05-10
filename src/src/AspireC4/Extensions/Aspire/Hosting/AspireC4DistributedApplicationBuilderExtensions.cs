using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class AspireC4DistributedApplicationBuilderExtensions
{
	internal const string AspireC4ResourceName = "aspirec4";

	extension(IDistributedApplicationBuilder builder)
	{
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
		/// <param name="builder">The distributed application builder.</param>
		/// <param name="name">Optional name of the LikeC4 visualization resource (used for the server container and diagram file).</param>
		/// <param name="port">Optional host port to bind the LikeC4 server's HTTP endpoint to. By default, no fixed host port is used and Docker assigns a dynamic port.</param>
		/// <param name="configure">Optional callback to configure <see cref="AspireC4DiagramOptions"/>.</param>
		/// <returns>An <see cref="IAspireC4Builder"/> for further configuration.</returns>
		public IAspireC4Builder AddAspireC4(
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

			// Resolve options at build time so the bind mount and lifecycle hook use the same path.
			AspireC4DiagramOptions opts = new();
			configure?.Invoke(opts);

			var outputDir = ResolveOutputDirectory(builder.AppHostDirectory, opts.OutputDirectory);
			Directory.CreateDirectory(outputDir);
			var imageTag = opts.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;
			var imageReference = LikeC4ServerResource.GetImageReference(imageTag);
			var hmrPortMode = LikeC4HmrPortCompatibility.Resolve(imageTag);
			// Use the relay on Windows even in Configurable mode: Docker Desktop may fail to publish
			// the well-known port (24678) reliably due to Hyper-V port reservations or port-cleanup
			// races between container restarts. The relay owns port 24678 on the host side and
			// bridges incoming HMR connections to whatever dynamic port Docker happened to allocate.
			var useHmrRelay = hmrPortMode == LikeC4HMRPortMode.FixedPort || OperatingSystem.IsWindows();
			var workspaceVolumeName = ResolveWorkspaceVolumeName(builder.AppHostDirectory, name);

			builder
				.Services.AddOptions<LikeC4ContainerWorkspaceOptions>()
				.Configure(runtime =>
				{
					runtime.VolumeName = workspaceVolumeName;
					runtime.ContainerImageReference = imageReference;
					runtime.ContainerRuntimeExecutable = ResolveContainerRuntimeExecutable();
					runtime.HMRPortMode = hmrPortMode;
					runtime.UseHMRRelay = useHmrRelay;
				});

			builder.Services.AddEventingSubscriber<AspireC4LifecycleHook>();
			builder.Services.AddAspireC4LifecycleHookTelemetry();

			LikeC4ServerResource serverResource = new(name);
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
				.WithArgs("start", ".", "--port", $"{LikeC4ServerResource.DefaultContainerServePort}")
				.WithVolume(workspaceVolumeName, LikeC4ServerResource.WorkspacePath)
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
						opts.Url = "/view/index";
					}
				)
				//.WithExternalHttpEndpoints()
				// Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
				.WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);

			if (opts.DisableHMR)
			{
				serverBuilder.WithArgs("--no-react-hmr");
			}
			else
			{
				serverBuilder
					.WithHttpEndpoint(
						// When using the relay, omit a fixed host port so Docker allocates a dynamic one.
						// The relay owns port 24678 on the host and bridges connections to the dynamic port.
						// Direct fixed-port mapping is only safe on non-Windows Configurable-mode images.
						port: useHmrRelay ? null : LikeC4ServerResource.DefaultContainerUpdatePort,
						targetPort: LikeC4ServerResource.DefaultContainerUpdatePort,
						name: LikeC4ServerResource.HmrEndpointName
					)
					.WithUrlForEndpoint(
						LikeC4ServerResource.HmrEndpointName,
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

			return new AspireC4Builder(builder, serverBuilder, outputDir);
		}

		internal static string ResolveOutputDirectory(string appHostDirectory, string outputDirectory)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);
			ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

			return Path.GetFullPath(
				Path.IsPathRooted(outputDirectory) ? outputDirectory : Path.Combine(appHostDirectory, outputDirectory)
			);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
		internal static string ResolveWorkspaceVolumeName(string appHostDirectory, string resourceName)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);
			ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

			var normalizedResourceName = NormalizeContainerNameSegment(resourceName);
			var normalizedAppHostDirectory = Path.GetFullPath(appHostDirectory)
				.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedAppHostDirectory));
			var hash = Convert.ToHexString(hashBytes)[..12].ToLowerInvariant();

			return $"aspirec4-{normalizedResourceName}-{hash}";
		}

		internal static string ResolveContainerRuntimeExecutable() =>
			Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") switch
			{
				{ Length: > 0 } runtime => runtime,
				_ => "docker",
			};

		static string NormalizeContainerNameSegment(string value)
		{
			StringBuilder sb = new(value.Length);
			foreach (var ch in value)
			{
				sb.Append(char.IsAsciiLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
			}

			var normalized = sb.ToString().Trim('-');
			return normalized.Length == 0 ? "workspace" : normalized;
		}
	}
}
