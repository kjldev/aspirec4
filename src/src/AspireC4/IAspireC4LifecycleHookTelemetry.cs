using Purview.Telemetry;

namespace Aspire.Hosting.AspireC4;

[Logger]
interface IAspireC4LifecycleHookTelemetry
{
	[Debug]
	void GeneratingLikeC4Model(int resourceCount, string[] resourceNames);

	[Debug]
	void LikeC4ModelWritten(string outputPath);

	[Debug]
	void PublishMode();

	[Debug]
	void ResourceStateChanged(string resourceName, string newState);

	[Debug]
	void DashboardUrlDiscovered(string dashboardBaseUrl);

	[Debug]
	void RegeneratingDiagramDueToStateChange();

	[Warning]
	void HMRPortUnavailable(int port, string error);

	[Warning]
	void StateWatcherFailed(string error);

	[Info]
	void StartingLikeC4Validation();

	[Error]
	void LikeC4ValidationFailed(int filteredErrors, int totalErrors);

	[Info]
	void LikeC4ValidatedSuccessfully();

	[Debug]
	void LikeC4FormatApplied();

	[Debug]
	void AdditionalDSLFileSynced(string fileName);
}
