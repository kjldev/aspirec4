namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Holds the pre-declared allowed values for each strict-mode category.
/// Configure via <see cref="AspireC4DiagramOptionsExtensions.WithStrictMode"/> and
/// the <c>WithAllowed*</c> extension methods, or bind from the <c>AspireC4:Strict</c>
/// configuration section.
/// </summary>
/// <remarks>
/// This class is only consulted when the corresponding flag is set in
/// <see cref="AspireC4DiagramOptions.Strict"/>.
/// </remarks>
public sealed class AspireC4StrictOptions
{
	/// <summary>
	/// Controls which categories are enforced. Defaults to <see cref="AspireC4StrictMode.None"/>.
	/// </summary>
	public AspireC4StrictMode Mode { get; set; } = AspireC4StrictMode.None;

	/// <summary>
	/// The set of tag names that are permitted when <see cref="AspireC4StrictMode.Tags"/> is active.
	/// Tag names are compared case-insensitively and without a leading <c>#</c>.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0028:Simplify collection initialization",
		Justification = "Not with .NET 11/ C# next it can't..."
	)]
	public HashSet<string> Tags { get; set; } = new(StringComparer.Ordinal);

	/// <summary>
	/// The set of relationship kind identifiers that are permitted when
	/// <see cref="AspireC4StrictMode.RelationshipKinds"/> is active.
	/// Compared case-insensitively.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0028:Simplify collection initialization",
		Justification = "Not with .NET 11/ C# next it can't..."
	)]
	public HashSet<string> RelationshipKinds { get; set; } = new(StringComparer.Ordinal);

	/// <summary>
	/// The set of group names that are permitted when <see cref="AspireC4StrictMode.Groups"/> is active.
	/// Compared case-insensitively.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0028:Simplify collection initialization",
		Justification = "Not with .NET 11/ C# next it can't..."
	)]
	public HashSet<string> Groups { get; set; } = new(StringComparer.Ordinal);

	/// <summary>
	/// The set of metadata key names that are permitted when <see cref="AspireC4StrictMode.MetadataKeys"/> is active.
	/// Only user-defined metadata keys are checked; auto-injected Aspire keys (<c>aspire-name</c>,
	/// <c>aspire-type</c>) are always exempt.
	/// Compared case-insensitively.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Style",
		"IDE0028:Simplify collection initialization",
		Justification = "Not with .NET 11/ C# next it can't..."
	)]
	public HashSet<string> MetadataKeys { get; set; } = new(StringComparer.Ordinal);
}
