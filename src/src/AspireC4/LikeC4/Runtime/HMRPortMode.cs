namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

enum HMRPortMode
{
	/// <summary>
	/// Automatically detect HMR port mode based on the LikeC4 container image version.
	/// Older versions (pre-1.57) use fixed ports; newer versions support configurable ports.
	/// </summary>
	Auto,
	FixedPort,
	Configurable,
}

