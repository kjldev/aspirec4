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
}
