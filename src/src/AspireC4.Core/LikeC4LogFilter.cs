namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Content-based log-line filter used to distinguish real error-severity messages from
/// informational output written to stderr.
/// </summary>
/// <remarks>
/// Some services (e.g. PostgreSQL, Redis) write all log levels — including <c>LOG</c>,
/// <c>INFO</c>, and <c>NOTICE</c> — to <c>stderr</c>.  Aspire marks any stderr line as
/// <see cref="Aspire.Hosting.ApplicationModel.ResourceLogLine.IsErrorMessage"/><c> = true</c>.
/// This class applies a secondary content-based check so that normal startup messages on
/// stderr are not treated as errors in the LikeC4 diagram.
/// </remarks>
public static class LikeC4LogFilter
{
	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="content"/> appears to be an
	/// actual error-severity log line, based on the presence of recognised keywords.
	/// </summary>
	/// <remarks>
	/// Checks case-insensitively for: <c>error</c>, <c>fatal</c>, <c>panic</c>,
	/// <c>exception</c>.  A line that is marked <c>IsErrorMessage</c> by Aspire but contains
	/// none of these keywords (e.g. a PostgreSQL <c>LOG:  listening on IPv4 address…</c>
	/// startup message) will return <see langword="false"/>.
	/// </remarks>
	public static bool IsActualError(string content)
	{
		ArgumentNullException.ThrowIfNull(content);

		return content.Contains("error", StringComparison.OrdinalIgnoreCase)
			|| content.Contains("fatal", StringComparison.OrdinalIgnoreCase)
			|| content.Contains("panic", StringComparison.OrdinalIgnoreCase)
			|| content.Contains("exception", StringComparison.OrdinalIgnoreCase);
	}
}
