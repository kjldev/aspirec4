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
}
