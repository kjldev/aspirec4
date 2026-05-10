namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Controls which Aspire runtime metadata is automatically injected into generated LikeC4 elements.
/// </summary>
[Flags]
public enum AspireMetadataInclusion
{
	/// <summary>No Aspire metadata is automatically injected.</summary>
	None = 0,

	/// <summary>
	/// Injects the Aspire resource name and resource type as element metadata entries
	/// (<c>aspire-name</c> and <c>aspire-type</c>).
	/// </summary>
	Metadata = 1,

	/// <summary>Injects publicly addressable HTTP/HTTPS endpoint URLs as element links.</summary>
	Links = 2,

	/// <summary>Injects both metadata and links. This is the default.</summary>
	All = Metadata | Links,
}
