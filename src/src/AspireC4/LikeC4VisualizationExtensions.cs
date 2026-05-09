using System.Security.Cryptography;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Aspire.Hosting;

#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for integrating LikeC4 live architecture diagrams into an Aspire AppHost.
/// </summary>
public static class LikeC4VisualizationExtensions
{
	internal const string ServerResourceName = "likec4-visualization";

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
	/// <param name="name">The name of the LikeC4 visualization resource (used for the server container and diagram file).</param>
	/// <param name="configure">Optional callback to configure <see cref="LikeC4DiagramOptions"/>.</param>
	/// <returns>An <see cref="ILikeC4VisualizationBuilder"/> for further configuration.</returns>
	public static ILikeC4VisualizationBuilder AddLikeC4Visualization(
		this IDistributedApplicationBuilder builder,
		[ResourceName] string name = ServerResourceName,
		Action<LikeC4DiagramOptions>? configure = null
	)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(name);

		builder
			.Services.AddOptions<LikeC4DiagramOptions>()
			.Configure(opts =>
			{
				configure?.Invoke(opts);
				opts.OutputDirectory = ResolveOutputDirectory(builder.AppHostDirectory, opts.OutputDirectory);
			});

		// Resolve options at build time so the bind mount and lifecycle hook use the same path.
		var opts = new LikeC4DiagramOptions();
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
		var useHmrRelay = hmrPortMode == LikeC4HmrPortMode.FixedPort || OperatingSystem.IsWindows();
		var workspaceVolumeName = ResolveWorkspaceVolumeName(builder.AppHostDirectory, ServerResourceName);

		builder
			.Services.AddOptions<LikeC4ContainerWorkspaceOptions>()
			.Configure(runtime =>
			{
				runtime.VolumeName = workspaceVolumeName;
				runtime.ContainerImageReference = imageReference;
				runtime.ContainerRuntimeExecutable = ResolveContainerRuntimeExecutable();
				runtime.HmrPortMode = hmrPortMode;
				runtime.UseHmrRelay = useHmrRelay;
			});

		builder.Services.AddEventingSubscriber<LikeC4VisualizationLifecycleHook>();
		builder.Services.AddLikeC4VisualizationLifecycleHookTelemetry();

		var serverResource = new LikeC4ServerResource(ServerResourceName);

		var serverBuilder = builder
			.AddResource(serverResource)
			.WithImage(LikeC4ServerResource.DefaultImage, imageTag)
			.WithImageRegistry(LikeC4ServerResource.DefaultRegistry)
			// "serve",
			.WithArgs("start", ".", "--port", LikeC4ServerResource.DefaultContainerServePort)
			.WithVolume(workspaceVolumeName, LikeC4ServerResource.WorkspacePath)
			// Required on Windows/Docker Desktop: inotify events do not propagate from the host
			// filesystem into the container, so chokidar must fall back to polling to detect
			// changes to the generated .c4 file.
			.WithEnvironment("CHOKIDAR_USEPOLLING", "1")
			.WithEnvironment("CHOKIDAR_INTERVAL", "200")
			.WithHttpEndpoint(
				name: LikeC4ServerResource.HttpEndpointName,
				targetPort: LikeC4ServerResource.DefaultContainerServePort
			)
			.WithHttpEndpoint(
				targetPort: LikeC4ServerResource.DefaultContainerUpdatePort,
				// When using the relay, omit a fixed host port so Docker allocates a dynamic one.
				// The relay owns port 24678 on the host and bridges connections to the dynamic port.
				// Direct fixed-port mapping is only safe on non-Windows Configurable-mode images.
				port: useHmrRelay ? null : LikeC4ServerResource.DefaultContainerUpdatePort,
				name: LikeC4ServerResource.HmrEndpointName
			)
			.WithExternalHttpEndpoints()
			// Exclude the sidecar from the architecture diagram — it is tooling, not a system element.
			.WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);

		return new LikeC4VisualizationBuilder(builder, serverBuilder, outputDir);
	}

	internal static string ResolveOutputDirectory(string appHostDirectory, string outputDirectory)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

		return Path.GetFullPath(
			Path.IsPathRooted(outputDirectory) ? outputDirectory : Path.Combine(appHostDirectory, outputDirectory)
		);
	}

	internal static string ResolveWorkspaceVolumeName(string appHostDirectory, string resourceName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(appHostDirectory);
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		var normalizedResourceName = NormalizeContainerNameSegment(resourceName);
		var normalizedAppHostDirectory = Path.GetFullPath(appHostDirectory)
			.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedAppHostDirectory));
		var hash = Convert.ToHexString(hashBytes)[..12].ToLowerInvariant();

		return $"likec4-{normalizedResourceName}-{hash}";
	}

	internal static string ResolveContainerRuntimeExecutable() =>
		Environment.GetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME") switch
		{
			{ Length: > 0 } runtime => runtime,
			_ => "docker",
		};

	static string NormalizeContainerNameSegment(string value)
	{
		var builder = new StringBuilder(value.Length);
		foreach (var ch in value)
		{
			builder.Append(char.IsAsciiLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
		}

		var normalized = builder.ToString().Trim('-');
		return normalized.Length == 0 ? "workspace" : normalized;
	}

	/// <summary>
	/// Customises how a resource appears in the generated LikeC4 diagram.
	/// </summary>
	public static IResourceBuilder<T> WithLikeC4Details<T>(
		this IResourceBuilder<T> builder,
		string? label = null,
		string? technology = null,
		string? description = null
	)
		where T : IResource =>
		WithLikeC4DetailsCore(builder, label, technology, description, icon: null, autoIconEnabled: null);

	/// <summary>
	/// Customises how a resource appears in the generated LikeC4 diagram, including an explicit icon.
	/// </summary>
	public static IResourceBuilder<T> WithLikeC4Details<T>(
		this IResourceBuilder<T> builder,
		string? label,
		string? technology,
		string? description,
		string? icon
	)
		where T : IResource =>
		WithLikeC4DetailsCore(builder, label, technology, description, icon, autoIconEnabled: null);

	/// <summary>
	/// Customises how a resource appears in the generated LikeC4 diagram using fluent options.
	/// </summary>
	public static IResourceBuilder<T> WithLikeC4Details<T>(
		this IResourceBuilder<T> builder,
		Action<LikeC4DetailsOptions> configure
	)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new LikeC4DetailsOptions();
		configure(options);

		return WithLikeC4DetailsCore(
			builder,
			options.Label,
			options.Technology,
			options.Description,
			options.Icon,
			options.AutoIconEnabled
		);
	}

	static IResourceBuilder<T> WithLikeC4DetailsCore<T>(
		IResourceBuilder<T> builder,
		string? label,
		string? technology,
		string? description,
		string? icon,
		bool? autoIconEnabled
	)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		var effectiveLabel = label ?? builder.Resource.Name;
		return builder.WithAnnotation(
			new LikeC4NodeDetailsAnnotation(effectiveLabel, technology, description, icon, autoIconEnabled),
			ResourceAnnotationMutationBehavior.Replace
		);
	}

	/// <summary>
	/// Customises how the relationship from this resource to <paramref name="target"/> appears in the
	/// generated LikeC4 diagram.
	/// </summary>
	/// <remarks>
	/// This method only adds the LikeC4 diagram annotation — it does <em>not</em> call
	/// <c>WithReference</c>. Continue to use Aspire's <c>WithReference</c> to establish the actual
	/// runtime dependency, or use the overload that accepts <c>withAspireReference: true</c>.
	/// </remarks>
	/// <param name="builder">The source resource builder.</param>
	/// <param name="target">The target resource builder that the relationship points to.</param>
	/// <param name="configure">Action that configures the relationship appearance.</param>
	public static IResourceBuilder<T> WithLikeC4Reference<T, TRef>(
		this IResourceBuilder<T> builder,
		IResourceBuilder<TRef> target,
		Action<LikeC4RelationshipOptions> configure
	)
		where T : IResource
		where TRef : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new LikeC4RelationshipOptions();
		configure(options);

		builder.Resource.Annotations.Add(
			new LikeC4RelationshipDetailsAnnotation(
				target.Resource.Name,
				options.Label,
				options.Technology,
				options.Description
			)
		);

		return builder;
	}

	/// <summary>
	/// Customises how the relationship from this resource to <paramref name="target"/> appears in the
	/// generated LikeC4 diagram, and optionally also calls Aspire's <c>WithReference</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <paramref name="withAspireReference"/> is <c>true</c>, this method calls
	/// <c>WithReference</c> internally so you do not need to call it separately:
	/// </para>
	/// <code>
	/// .WithLikeC4Reference(redis, opts =&gt; opts
	///     .WithLabel("Caches sessions")
	///     .WithTechnology("Redis Protocol"),
	///     withAspireReference: true)
	/// .WaitFor(redis)
	/// </code>
	/// </remarks>
	/// <param name="builder">The source resource builder.</param>
	/// <param name="target">The target resource builder that the relationship points to.</param>
	/// <param name="configure">Optional action that configures the relationship appearance. Pass <c>null</c> for defaults.</param>
	/// <param name="withAspireReference">When <c>true</c>, also calls <c>WithReference</c> on the target.</param>
	public static IResourceBuilder<T> WithLikeC4Reference<T, TRef>(
		this IResourceBuilder<T> builder,
		IResourceBuilder<TRef> target,
		Action<LikeC4RelationshipOptions>? configure,
		bool withAspireReference
	)
		where T : IResourceWithEnvironment
		where TRef : IResourceWithConnectionString
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(target);

		if (withAspireReference)
			builder.WithReference((IResourceBuilder<IResourceWithConnectionString>)target);

		if (configure is not null)
			builder.WithLikeC4Reference(target, configure);

		return builder;
	}

	/// <summary>
	/// Excludes a resource from the generated LikeC4 diagram.
	/// </summary>
	public static IResourceBuilder<T> ExcludeFromLikeC4<T>(this IResourceBuilder<T> builder)
		where T : IResource
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithAnnotation(new ExcludeFromLikeC4Annotation(), ResourceAnnotationMutationBehavior.Replace);
	}
}
