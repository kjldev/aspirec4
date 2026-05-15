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
