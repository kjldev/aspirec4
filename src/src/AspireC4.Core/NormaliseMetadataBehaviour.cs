namespace Aspire.Hosting.AspireC4;

/// <summary>Controls how invalid characters in LikeC4 metadata keys are handled.</summary>
public enum NormaliseMetadataBehaviour
{
	/// <summary>
	/// Replace any character that is not a letter, digit, hyphen, or underscore with <c>_</c>.
	/// A <see langword="null"/> key always throws <see cref="ArgumentNullException"/>.
	/// This is the default.
	/// <example><c>"Azure SKU"</c> → <c>"Azure_SKU"</c></example>
	/// </summary>
	Normalise,

	/// <summary>
	/// Same as <see cref="Normalise"/>, but additionally lowercases the resulting key.
	/// <example><c>"Azure SKU"</c> → <c>"azure_sku"</c></example>
	/// </summary>
	NormaliseLowercase,

	/// <summary>
	/// Throw an <see cref="ArgumentException"/> if the key contains any character that is not
	/// a letter, digit, hyphen, or underscore. A <see langword="null"/> key always throws
	/// <see cref="ArgumentNullException"/>.
	/// </summary>
	Throw,
}
