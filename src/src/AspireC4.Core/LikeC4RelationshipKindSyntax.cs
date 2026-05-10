namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Controls the DSL syntax used to emit typed relationships in the generated <c>.c4</c> file.
/// </summary>
public enum LikeC4RelationshipKindSyntax
{
	/// <summary>
	/// Emits the dot-prefix syntax: <c>SOURCE .KIND TARGET</c>. This is the preferred default.
	/// </summary>
	Dot,

	/// <summary>
	/// Emits the bracket syntax: <c>SOURCE -[KIND]-&gt; TARGET</c>.
	/// </summary>
	Bracket,
}
