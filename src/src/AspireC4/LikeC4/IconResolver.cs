namespace Aspire.Hosting.AspireC4.LikeC4;

/// <summary>
/// Provides context to a <see cref="IconResolver"/> delegate when resolving
/// the icon for a single Aspire resource.
/// </summary>
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

/// <summary>
/// A delegate that resolves the LikeC4 icon string for a given Aspire resource.
/// Return a non-<see langword="null"/> icon string to use that icon; return
/// <see langword="null"/> to fall through to the next resolver or the built-in
/// auto-icon inference.
/// </summary>
/// <param name="context">The resolution context for the current resource.</param>
/// <returns>
/// A LikeC4 icon string (e.g. <c>"tech:redis"</c>, <c>"azure:azure-cache-for-redis"</c>),
/// or <see langword="null"/> to defer to the next resolver or built-in inference.
/// </returns>
public delegate string? IconResolver(IconResolverContext context);
