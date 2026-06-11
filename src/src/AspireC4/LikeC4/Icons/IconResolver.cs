namespace Aspire.Hosting.AspireC4.LikeC4.Icons;

/// <summary>
/// Provides context to a <see cref="Func{T, TResult}"/> delegate when resolving
/// the icon for a single Aspire resource.
/// </summary>
[AspireExport(ExposeProperties = true)]
public sealed class IconResolverContext
{
	/// <summary>The visible Aspire resource being rendered as a diagram element.</summary>
	public required IResource Resource { get; init; }

	/// <summary>
	/// The hidden Aspire resource that the visible resource was derived from, if any.
	/// <para>
	/// This is set when an Azure resource (e.g. <c>AzurePostgresFlexibleServerResource</c>)
	/// has been replaced by a local surrogate via <c>RunAsContainer()</c>. The surrogate is
	/// the <see cref="Resource"/>; the original Azure resource is exposed here so the resolver
	/// can access richer type information.
	/// </para>
	/// </summary>
	public IResource? HiddenOriginal { get; init; }
}
