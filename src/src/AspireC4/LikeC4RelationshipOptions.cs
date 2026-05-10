namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Fluent options for customising how a relationship appears in the generated LikeC4 diagram.
/// </summary>
/// <seealso cref="AspireC4ResourceBuilderExtensions.WithLikeC4Reference{T,TRef}"/>
public sealed class LikeC4RelationshipOptions
{
	readonly List<string> _tags = [];
	readonly List<LikeC4Link> _links = [];
	readonly List<LikeC4Metadata> _metadata = [];

	/// <summary>Short label shown on the relationship arrow.</summary>
	public string? Label { get; private set; }

	/// <summary>Technology or protocol used by the relationship (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public string? Technology { get; private set; }

	/// <summary>Longer description of the relationship.</summary>
	public string? Description { get; private set; }

	/// <summary>
	/// Optional LikeC4 relationship kind identifier (e.g. "async", "sync", "grpc"). When set, the kind
	/// is declared in the <c>specification</c> block and the typed <c>-[kind]-&gt;</c> syntax is used.
	/// Must be a valid LikeC4 identifier (letters, digits, hyphens, underscores; cannot start with a digit).
	/// </summary>
	public string? Kind { get; private set; }

	/// <summary>Tags applied to this relationship in the diagram.</summary>
	public IReadOnlyList<string> Tags => _tags;

	/// <summary>Links attached to this relationship in the diagram.</summary>
	public IReadOnlyList<LikeC4Link> Links => _links;

	/// <summary>Metadata key-value pairs for this relationship.</summary>
	public IReadOnlyList<LikeC4Metadata> Metadata => _metadata;

	/// <summary>
	/// Optional ID of a LikeC4 view to navigate to when the relationship is clicked.
	/// The view must be a dynamic view defined in the LikeC4 DSL.
	/// </summary>
	public string? NavigateTo { get; private set; }

	/// <summary>Sets the short label shown on the relationship arrow.</summary>
	public LikeC4RelationshipOptions WithLabel(string label)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);
		Label = label;
		return this;
	}

	/// <summary>Sets the technology or protocol (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public LikeC4RelationshipOptions WithTechnology(string? technology)
	{
		Technology = technology;
		return this;
	}

	/// <summary>Sets the longer description of the relationship.</summary>
	public LikeC4RelationshipOptions WithDescription(string? description)
	{
		Description = description;
		return this;
	}

	/// <summary>
	/// Sets the LikeC4 relationship kind (e.g. "async", "sync", "grpc"). The kind is declared in the
	/// <c>specification</c> block and the typed <c>-[kind]-&gt;</c> syntax is used in the model.
	/// </summary>
	public LikeC4RelationshipOptions WithKind(string? kind)
	{
		Kind = kind;
		return this;
	}

	/// <summary>Adds a tag to this relationship. Tags are declared in the <c>specification</c> block.</summary>
	public LikeC4RelationshipOptions WithTag(string tag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tag);
		_tags.Add(tag);
		return this;
	}

	/// <summary>Adds a hyperlink to this relationship.</summary>
	/// <param name="url">The URL, which may be absolute or relative to the <c>.c4</c> file.</param>
	/// <param name="title">Optional display text.</param>
	public LikeC4RelationshipOptions WithLink(string url, string? title = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(url);
		_links.Add(new LikeC4Link(url, title));
		return this;
	}

	/// <summary>Adds a hyperlink to this relationship.</summary>
	/// <param name="uri">The URI, which may be absolute or relative to the <c>.c4</c> file.</param>
	/// <param name="title">Optional display text.</param>
	public LikeC4RelationshipOptions WithLink(Uri uri, string? title = null) => WithLink(uri?.ToString()!, title);

	/// <summary>Adds a metadata key-value pair to this relationship.</summary>
	public LikeC4RelationshipOptions WithMetadata(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		_metadata.Add(new LikeC4Metadata(key, value));
		return this;
	}

	/// <summary>Adds a metadata key-value pair to this element.</summary>
	public LikeC4RelationshipOptions WithMetadata(params (string key, string value)[] metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		foreach (var (key, value) in metadata)
			WithMetadata(key, value);

		return this;
	}

	/// <summary>Adds a metadata key-value pair to this element.</summary>
	public LikeC4RelationshipOptions WithMetadata(IDictionary<string, string> metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		foreach (var (key, value) in metadata)
			WithMetadata(key, value);

		return this;
	}

	/// <summary>
	/// Sets the ID of a LikeC4 dynamic view to navigate to when the relationship is clicked.
	/// </summary>
	/// <param name="viewId">The identifier of the target dynamic view.</param>
	public LikeC4RelationshipOptions WithNavigateTo(string viewId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(viewId);
		NavigateTo = viewId;
		return this;
	}
}
