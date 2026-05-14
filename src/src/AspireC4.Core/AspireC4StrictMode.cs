namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Controls which diagram model categories are validated in strict mode.
/// When a category is enabled, any value used in that category must be pre-declared via
/// <see cref="AspireC4StrictOptions"/> or <c>WithAllowed*</c> extension methods, or a
/// <see cref="System.InvalidOperationException"/> is thrown during model building.
/// </summary>
[Flags]
public enum AspireC4StrictMode
{
	/// <summary>No strict validation is applied. This is the default.</summary>
	None = 0,

	/// <summary>
	/// Element and relationship tags must be pre-declared in <see cref="AspireC4StrictOptions.Tags"/>.
	/// Auto-generated state tags (from <see cref="AspireC4DiagramOptions.StateTagMap"/>) are exempt.
	/// </summary>
	Tags = 1,

	/// <summary>
	/// Relationship kinds must be pre-declared in <see cref="AspireC4StrictOptions.RelationshipKinds"/>.
	/// </summary>
	RelationshipKinds = 2,

	/// <summary>
	/// Element groups must be pre-declared in <see cref="AspireC4StrictOptions.Groups"/>.
	/// </summary>
	Groups = 4,

	/// <summary>
	/// User-defined metadata keys must be pre-declared in <see cref="AspireC4StrictOptions.MetadataKeys"/>.
	/// Auto-injected Aspire metadata (<c>aspire-name</c>, <c>aspire-type</c>) is exempt.
	/// </summary>
	MetadataKeys = 8,

	/// <summary>All strict validation categories are enabled.</summary>
	All = Tags | RelationshipKinds | Groups | MetadataKeys,
}
