namespace Aspire.Hosting.AspireC4;

static class LikeC4TagHelper
{
	/// <summary>
	/// Normalises a tag name by stripping the leading <c>#</c> character if present.
	/// <c>"#external"</c> and <c>"external"</c> both normalise to <c>"external"</c>,
	/// preventing duplicates in the LikeC4 <c>specification</c> block and body tags.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tag"/> is <see langword="null"/>, empty, whitespace, or consists
	/// only of <c>#</c> characters (leaving an empty name after stripping).
	/// </exception>
	public static string Normalize(string tag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tag);

		var normalized = tag.TrimStart('#').TrimEnd();

		return string.IsNullOrWhiteSpace(normalized)
			? throw new ArgumentException("Tag name must not be empty or consist only of '#' characters.", nameof(tag))
			: normalized;
	}
}
