using Purview.Telemetry;

namespace Aspire.Hosting.AspireC4;

[Logger]
interface IAspireC4LifecycleHookTelemetry
{
	[Info]
	void GeneratingLikeC4Model(int resourceCount);

	[Info]
	void LikeC4ModelWritten(string outputPath);

	[Info]
	void PublishMode();

	[Info]
	void ResourceStateChanged(string resourceName, string newState);

	[Info]
	void ResourceErrorLogDetected(string resourceName);

	[Info]
	void RegeneratingDiagramDueToStateChange();

	[Warning]
	void HMRPortUnavailable(int port, string error);

	[Warning]
	void StateWatcherFailed(string error);

	[Warning]
	void LikeC4ValidationErrors(int filteredErrors, int totalErrors);

	[Info]
	void LikeC4ValidationPassed();

	[Info]
	void AdditionalDSLFileSynced(string fileName);
}
