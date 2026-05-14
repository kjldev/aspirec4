namespace System;

static class StringExtensions
{
	extension(string str)
	{
		[Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase")]
		public string ToLowerInvariantSafe() => str.ToLowerInvariant();
	}
}
