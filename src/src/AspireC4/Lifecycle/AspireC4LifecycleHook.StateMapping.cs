using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Aspire.Hosting.AspireC4.LikeC4.Generators;
using Aspire.Hosting.AspireC4.LikeC4.Runtime;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	static string? MapAspireState(CustomResourceSnapshot snapshot)
	{
		var style = snapshot.State?.Style;
		var text = snapshot.State?.Text;

		// Style takes semantic precedence (Aspire sets it based on exit code / health).
		if (string.Equals(style, "error", StringComparison.OrdinalIgnoreCase))
			return KnownResourceStates.FailedToStart;

		if (string.Equals(style, "warn", StringComparison.OrdinalIgnoreCase))
			return KnownResourceStates.RuntimeUnhealthy;

		// Use string literals — KnownResourceStates members are static readonly, not const.
		return text switch
		{
			"Running" => KnownResourceStates.Running,
			"Starting" => KnownResourceStates.Starting,
			"Waiting" => KnownResourceStates.Waiting,
			"Stopping" => KnownResourceStates.Stopping,
			"FailedToStart" => KnownResourceStates.FailedToStart,
			"RuntimeUnhealthy" => KnownResourceStates.RuntimeUnhealthy,
			"Exited" => KnownResourceStates.Exited,
			"Finished" => KnownResourceStates.Finished,
			_ => null,
		};
	}
}
