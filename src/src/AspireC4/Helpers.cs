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
}
