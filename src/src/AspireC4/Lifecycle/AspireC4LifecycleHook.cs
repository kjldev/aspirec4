using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.Sockets;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4.Lifecycle;

/// <summary>
/// Aspire eventing subscriber that generates the LikeC4 <c>.c4</c> model file before the
/// application starts, and dynamically regenerates it whenever a resource changes state at runtime.
/// </summary>
sealed partial class AspireC4LifecycleHook(
	IOptions<AspireC4DiagramOptions> options,
	IOptions<ContainerWorkspaceOptions> workspaceOptions,
	ResourceNotificationService resourceNotificationService,
	ResourceLoggerService resourceLoggerService,
	IAspireC4LifecycleHookTelemetry telemetry,
	IConfiguration configuration
) : IDistributedApplicationEventingSubscriber, IDisposable
{
	// Well-known Aspire resource name for the dashboard process.
	const string AspireDashboardResourceName = "aspire-dashboard";

	readonly ConcurrentDictionary<string, string?> _resourceStates = new(StringComparer.OrdinalIgnoreCase);

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

	// The header-stripped body of the last .c4 file written to disk.
	// Used to skip writes when the generated content has not changed, preventing needless
	// file-system churn (and git noise) from state-change events.
	volatile string? _lastRawBody;

	// The resolved or configured exact version of the LikeC4 image (e.g. "1.57.0").
	// Set either from a pinned ContainerImageTag or by the latest-version check at startup.
	// When non-null, it is injected as a "Version" metadata property on the AspireC4Resource.
	volatile string? _resolvedLikeC4Version;

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
				var aspirec4Resource = evt.Model.Resources.OfType<AspireC4Resource>().FirstOrDefault();
				var serverResource = aspirec4Resource?.InnerResource as LikeC4ServerResource;

				if (executionContext.IsPublishMode)
				{
					await WriteC4FileAsync(evt.Model, ct);
					telemetry.PublishMode();
					return;
				}

				if (serverResource is not null)
				{
					SetupContainerBindMount(evt.Model, serverResource);

					// If the tag is pinned to a specific version, record it immediately.
					// If "latest" is used, TryUpdateHmrPortModeFromLatestVersionAsync will
					// resolve and overwrite this with the actual pulled version.
					var effectiveTag = options.Value.ContainerImageTag ?? LikeC4ServerResource.DefaultTag;
					if (
						!string.Equals(
							effectiveTag,
							LikeC4ServerResource.DefaultTag,
							StringComparison.OrdinalIgnoreCase
						)
					)
					{
						_resolvedLikeC4Version = effectiveTag;
					}

					await TryUpdateHmrPortModeFromLatestVersionAsync(ct);

					if (!options.Value.DisableHMR)
					{
						EnsureLegacyHostHmrPortAvailable();
						StartLegacyHmrRelay(evt.Model, ct);
					}
				}

				await WriteC4FileAsync(evt.Model, ct);

				// Always keep the inner server resource hidden — AspireC4Resource is the single
				// dashboard entry. Its state, URLs, and properties are forwarded from the inner.
				if (aspirec4Resource?.InnerResource is not null)
				{
					_ = KeepServerHiddenAsync(aspirec4Resource.InnerResource, ct);
				}

				if (options.Value.HideFromDashboard)
				{
					// When HideFromDashboard is set, also suppress the outer resource and
					// surface the diagram URL on all project resources instead.
					if (aspirec4Resource is not null)
					{
						_ = KeepServerHiddenAsync(aspirec4Resource, ct);
					}

					SetupDashboardIntegration(evt.Model, options.Value.DashboardLinkDisplayName, ct);
				}

				// Fire-and-forget: watch for resource state changes and regenerate the file.
				// The ct is the application lifetime token; it is cancelled on shutdown.
				_ = WatchResourceStatesAsync(
					evt.Model,
					options.Value.ExcludedResourceTypes.Count > 0 ? options.Value.ExcludedResourceTypes : null,
					ct
				);

				// Forward inner resource state, URLs, and properties to AspireC4Resource so
				// it is the single useful dashboard entry and consumers watching by the outer
				// resource name (e.g., integration tests) receive correct state updates.
				// Also forward console logs so they are visible on the outer resource's
				// Console tab in the dashboard.
				if (aspirec4Resource is not null)
				{
					_ = ForwardInnerResourceStateAsync(aspirec4Resource, ct);
					_ = ForwardInnerResourceLogsAsync(aspirec4Resource, ct);
				}

				if (options.Value.IncludeAspireDashboardLinks)
				{
					_ = WatchDashboardUrlAsync(evt.Model, ct);
				}
			}
		);

		return Task.CompletedTask;
	}
}
