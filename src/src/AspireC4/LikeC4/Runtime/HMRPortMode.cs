namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

enum HMRPortMode
{
	/// <summary>
	/// Automatically detect HMR port mode based on the LikeC4 container image version.
	/// Older versions (pre-1.57) use fixed ports; newer versions support configurable ports.
	/// </summary>
	Auto,

	/// <summary>
	/// Use fixed ports for HMR. This is compatible with older LikeC4 container images (pre-1.57) that do not support configurable ports.
	/// </summary>
	FixedPort,

	/// <summary>
	/// Use configurable ports for HMR. This requires LikeC4 container images version 1.57 or later that support configurable HMR ports.
	/// </summary>
	Configurable,
}
