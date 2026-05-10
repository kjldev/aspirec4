namespace Aspire.Hosting.AspireC4;

public sealed class LikeC4LogFilterTests
{
	// ── Postgres log levels that are NOT errors ────────────────────────────────

	[Test]
	[Arguments("2026-05-10 21:24:43.757 UTC [1] LOG:  listening on IPv4 address \"0.0.0.0\", port 5432")]
	[Arguments("2026-05-10 21:24:43.758 UTC [1] LOG:  listening on IPv6 address \"::\"")]
	[Arguments("2026-05-10 21:24:43.760 UTC [1] LOG:  listening on Unix socket \"/var/run/postgresql/.s.PGSQL.5432\"")]
	[Arguments("2026-05-10 21:24:43.763 UTC [1] LOG:  database system was shut down at 2026-05-10 21:24:43 UTC")]
	[Arguments("2026-05-10 21:24:43.765 UTC [1] LOG:  database system is ready to accept connections")]
	[Arguments("NOTICE:  table \"foo\" does not exist, skipping")]
	public async Task IsActualError_PostgresInfoLine_ReturnsFalse(string line)
	{
		await Assert.That(LikeC4LogFilter.IsActualError(line)).IsFalse();
	}

	// ── Postgres log levels that ARE errors ───────────────────────────────────

	[Test]
	[Arguments("2026-05-10 21:24:43.800 UTC [1] ERROR:  invalid input syntax for type integer: \"abc\"")]
	[Arguments("2026-05-10 21:24:43.801 UTC [1] FATAL:  role \"nobody\" does not exist")]
	[Arguments("2026-05-10 21:24:43.802 UTC [1] PANIC:  could not locate a valid checkpoint record")]
	public async Task IsActualError_PostgresErrorLine_ReturnsTrue(string line)
	{
		await Assert.That(LikeC4LogFilter.IsActualError(line)).IsTrue();
	}

	// ── Generic application messages ──────────────────────────────────────────

	[Test]
	[Arguments("Server started successfully")]
	[Arguments("Listening on port 8080")]
	[Arguments("Connected to database")]
	[Arguments("Initialising configuration")]
	public async Task IsActualError_NormalStartupMessage_ReturnsFalse(string line)
	{
		await Assert.That(LikeC4LogFilter.IsActualError(line)).IsFalse();
	}

	[Test]
	[Arguments("error: connection refused")]
	[Arguments("[ERROR] Failed to connect")]
	[Arguments("System.Exception: something went wrong")]
	[Arguments("An unhandled exception occurred")]
	[Arguments("fatal: unable to read configuration")]
	[Arguments("panic: runtime error: index out of range")]
	public async Task IsActualError_CommonErrorPatterns_ReturnsTrue(string line)
	{
		await Assert.That(LikeC4LogFilter.IsActualError(line)).IsTrue();
	}

	// ── Case-insensitivity ────────────────────────────────────────────────────

	[Test]
	[Arguments("Error: disk full")]
	[Arguments("ERROR: disk full")]
	[Arguments("error: disk full")]
	public async Task IsActualError_ErrorKeyword_CaseInsensitive(string line)
	{
		await Assert.That(LikeC4LogFilter.IsActualError(line)).IsTrue();
	}

	[Test]
	public async Task IsActualError_EmptyString_ReturnsFalse()
	{
		await Assert.That(LikeC4LogFilter.IsActualError(string.Empty)).IsFalse();
	}
}
