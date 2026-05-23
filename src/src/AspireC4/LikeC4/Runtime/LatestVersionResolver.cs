using System.Diagnostics;

namespace Aspire.Hosting.AspireC4.LikeC4.Runtime;

/// <summary>
/// Detects the actual version of the LikeC4 container image when the <c>"latest"</c> tag is in use
/// by running a throwaway container and calling <c>likec4 --version</c>.
/// </summary>
static class LatestVersionResolver
{
	/// <summary>
	/// Runs <c>&lt;containerExe&gt; run --rm &lt;imageRef&gt; likec4 --version</c> and extracts
	/// the version token from the output. Returns <see langword="null"/> if the process fails,
	/// times out, or the output cannot be parsed as a version.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1031:Do not catch general exception types",
		Justification = "Version detection is best-effort; failures are reported via telemetry and the caller falls back gracefully"
	)]
	internal static async Task<string?> TryResolveAsync(
		string containerExe,
		string imageRef,
		int timeoutSeconds,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = containerExe,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			startInfo.ArgumentList.Add("run");
			startInfo.ArgumentList.Add("--rm");
			startInfo.ArgumentList.Add(imageRef);
			startInfo.ArgumentList.Add("likec4");
			startInfo.ArgumentList.Add("--version");

			using var process = Process.Start(startInfo);
			if (process is null)
				return null;

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

			string output;
			try
			{
				output = await process.StandardOutput.ReadToEndAsync(cts.Token);
				await process.WaitForExitAsync(cts.Token);
			}
			catch (OperationCanceledException)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch
				{
					// Best-effort kill.
				}

				return null;
			}

			if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
				return null;

			return TryExtractVersion(output.Trim(), out var version) ? version : null;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Extracts the first version-like token from the output of <c>likec4 --version</c>.
	/// The typical output format is <c>@likec4/cli/1.57.0 linux-x64 node-v22.14.0</c>;
	/// this method splits on <c>'/'</c> and <c>' '</c> and returns the first token that
	/// <see cref="HMRPortCompatibility.TryParseVersion"/> accepts (e.g. <c>"1.57.0"</c>).
	/// </summary>
	internal static bool TryExtractVersion(string cliOutput, out string versionToken)
	{
		var parts = cliOutput.Split(['/', ' '], StringSplitOptions.RemoveEmptyEntries);
		foreach (var part in parts)
		{
			if (HMRPortCompatibility.TryParseVersion(part, out _))
			{
				versionToken = part;
				return true;
			}
		}

		versionToken = string.Empty;
		return false;
	}
}
