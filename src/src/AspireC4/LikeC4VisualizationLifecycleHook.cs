using System.Collections.Concurrent;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Aspire eventing subscriber that generates the LikeC4 <c>.c4</c> model file before the
/// application starts, and dynamically regenerates it whenever a resource changes state at runtime.
/// </summary>
sealed class LikeC4VisualizationLifecycleHook(
	IOptions<LikeC4DiagramOptions> options,
	ResourceNotificationService resourceNotificationService,
	ILikeC4VisualizationLifecycleHookTelemetry telemetry) : IDistributedApplicationEventingSubscriber, IDisposable
{
	readonly ConcurrentDictionary<string, LikeC4ResourceState> _resourceStates =
		new(StringComparer.OrdinalIgnoreCase);

	// Debounce: cancels any pending delayed write when a new state change arrives.
	CancellationTokenSource? _debounceCts;
	readonly Lock _debounceLock = new();

	public Task SubscribeAsync(
		IDistributedApplicationEventing eventing,
		DistributedApplicationExecutionContext executionContext,
		CancellationToken cancellationToken)
	{
		eventing.Subscribe<BeforeStartEvent>(async (evt, ct) =>
		{
			await WriteC4FileAsync(evt.Model, ct);

			if (executionContext.IsPublishMode)
			{
				telemetry.PublishMode();
				return;
			}

			// Fire-and-forget: watch for resource state changes and regenerate the file.
			// The ct is the application lifetime token; it is cancelled on shutdown.
			_ = WatchResourceStatesAsync(evt.Model, ct);
		});

		return Task.CompletedTask;
	}

	// ── Background watcher ────────────────────────────────────────────────────

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

				var newState = MapAspireState(notification.Snapshot);

				if (_resourceStates.TryGetValue(notification.Resource.Name, out var current)
					&& current == newState)
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

		_ = Task.Run(async () =>
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
		}, CancellationToken.None);
	}

	// ── File generation ───────────────────────────────────────────────────────

	async Task WriteC4FileAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		var opts = options.Value;

		telemetry.GeneratingLikeC4Model(appModel.Resources.Count);

		var model = LikeC4ModelBuilder.Build([.. appModel.Resources], _resourceStates);
		var dsl = LikeC4DslGenerator.Generate(model, opts);

		var outputDir = Path.GetFullPath(opts.OutputDirectory);
		Directory.CreateDirectory(outputDir);

		var outputPath = Path.Combine(outputDir, opts.FileName + ".c4");
		await File.WriteAllTextAsync(outputPath, dsl, cancellationToken);

		telemetry.LikeC4ModelWritten(outputPath);
	}

	// ── IDisposable ───────────────────────────────────────────────────────────

	public void Dispose()
	{
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
			"Exited" => string.Equals(style, "success", StringComparison.OrdinalIgnoreCase)
				? LikeC4ResourceState.Exited
				: LikeC4ResourceState.Failed,
			"Finished" => LikeC4ResourceState.Exited,
			_ => LikeC4ResourceState.Unknown,
		};
	}
}
