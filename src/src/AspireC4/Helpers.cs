using System.Diagnostics;

namespace Aspire.Hosting.AspireC4;

static class Helpers
{
	// Lazily checks once whether Graphviz `dot` is on PATH so we can pass --use-dot to likec4 validate.
	static readonly AsyncLazy<bool> DotAvailable = new(static async cancellationToken =>
	{
		try
		{
			using var proc = Process.Start(
				new ProcessStartInfo
				{
					FileName = "dot",
					Arguments = "-V",
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				}
			);
			if (proc is null)
				return false;

			await proc.WaitForExitAsync(cancellationToken);
			return proc.ExitCode == 0;
		}
		catch
		{
			return false;
		}
	});

	public static async Task<bool> IsDotAvailableAsync(CancellationToken cancellationToken = default) =>
		await DotAvailable.GetValueAsync(cancellationToken);

	/// <summary>
	/// Normalises a tag name by stripping the leading <c>#</c> character if present.
	/// <c>"#external"</c> and <c>"external"</c> both normalise to <c>"external"</c>,
	/// preventing duplicates in the LikeC4 <c>specification</c> block and body tags.
	/// </summary>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tag"/> is <see langword="null"/>, empty, whitespace, or consists
	/// only of <c>#</c> characters (leaving an empty name after stripping).
	/// </exception>
	public static string NormaliseTag(string tag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tag);

		var normalized = tag.TrimStart('#').TrimEnd();

		return string.IsNullOrWhiteSpace(normalized)
			? throw new ArgumentException("Tag name must not be empty or consist only of '#' characters.", nameof(tag))
			: normalized;
	}
}
