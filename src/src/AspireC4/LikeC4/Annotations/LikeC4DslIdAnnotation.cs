namespace Aspire.Hosting.AspireC4.LikeC4.Annotations;

/// <summary>
/// Overrides the DSL element identifier used in the generated <c>.c4</c> file.
/// When absent the resource's <c>Name</c> property is used as the identifier.
/// </summary>
/// <remarks>
/// This is applied automatically by <c>AddAspireC4</c> so that both the Docker-container
/// and the local-CLI server resources are emitted under the same logical name
/// (the base resource name, e.g. <c>aspirec4</c>) regardless of the <c>-server</c>
/// suffix carried by the Aspire resource object.
/// </remarks>
sealed class LikeC4DslIdAnnotation : IResourceAnnotation
{
	/// <summary>The DSL identifier to emit for this resource in the generated <c>.c4</c> file.</summary>
	public string DslId { get; }

	public LikeC4DslIdAnnotation(string dslId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dslId);
		DslId = dslId;
	}
}
