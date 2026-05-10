namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Fluent options for configuring how a resource appears in the generated LikeC4 diagram.
/// </summary>
public sealed class LikeC4DetailsOptions
{
	readonly List<string> _tags = [];
	readonly List<LikeC4Link> _links = [];
	readonly List<LikeC4Metadata> _metadata = [];

	/// <summary>The resource label shown in the diagram.</summary>
	public string? Label { get; private set; }

	/// <summary>The technology text shown for the resource.</summary>
	public string? Technology { get; private set; }

	/// <summary>The longer description shown for the resource.</summary>
	public string? Description { get; private set; }

	/// <summary>The summary shown for the resource.</summary>
	public string? Summary { get; private set; }

	/// <summary>The explicit LikeC4 icon token or image reference for the resource.</summary>
	public string? Icon { get; private set; }

	/// <summary>
	/// Whether automatic icon inference is enabled for this resource.
	/// <see langword="null"/> inherits the project-level setting.
	/// </summary>
	public bool? AutoIconEnabled { get; private set; }

	/// <summary>
	/// Optional element kind override. When set, overrides the inferred element kind in the diagram.
	/// Must be a valid LikeC4 identifier (letters, digits, hyphens, underscores; cannot start with a digit).
	/// </summary>
	public string? Kind { get; private set; }

	/// <summary>Tags applied to this element in the diagram.</summary>
	public IReadOnlyList<string> Tags => _tags;

	/// <summary>Links attached to this element in the diagram.</summary>
	public IReadOnlyList<LikeC4Link> Links => _links;

	/// <summary>Metadata key-value pairs for this element.</summary>
	public IReadOnlyList<LikeC4Metadata> Metadata => _metadata;

	public LikeC4DetailsOptions WithLabel(string label)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);
		Label = label;
		return this;
	}

	public LikeC4DetailsOptions WithTechnology(string? technology)
	{
		Technology = technology;
		return this;
	}

	public LikeC4DetailsOptions WithDescription(string? description)
	{
		Description = description;
		return this;
	}

	public LikeC4DetailsOptions WithSummary(string? summary)
	{
		Summary = summary;
		return this;
	}

	public LikeC4DetailsOptions WithIcon(string? icon)
	{
		if (icon is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(icon);
		}

		Icon = icon;
		return this;
	}

	public LikeC4DetailsOptions WithAutoIcon(bool? enabled = null)
	{
		AutoIconEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets an element kind override, replacing the auto-inferred kind for this resource.
	/// Must be a valid LikeC4 identifier.
	/// </summary>
	public LikeC4DetailsOptions WithKind(string? kind)
	{
		Kind = kind;
		return this;
	}

	/// <summary>
	/// Adds a tag to this element. Tags are declared in the <c>specification</c> block.
	/// A leading <c>#</c> is accepted and stripped automatically, so <c>"#external"</c> and
	/// <c>"external"</c> refer to the same tag.
	/// </summary>
	public LikeC4DetailsOptions WithTag(string tag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tag);
		var normalized = tag.TrimStart('#');
		if (string.IsNullOrWhiteSpace(normalized))
		{
			throw new ArgumentException("Tag name must not be empty or consist only of '#' characters.", nameof(tag));
		}

		_tags.Add(normalized);
		return this;
	}

	/// <summary>
	/// Adds a hyperlink to this element.
	/// </summary>
	/// <param name="url">The URL, which may be absolute or relative to the <c>.c4</c> file.</param>
	/// <param name="title">Optional display text.</param>
	public LikeC4DetailsOptions WithLink(string url, string? title = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(url);
		_links.Add(new LikeC4Link(url, title));
		return this;
	}

	/// <summary>
	/// Adds a hyperlink to this element.
	/// </summary>
	/// <param name="uri">The URL, which may be absolute or relative to the <c>.c4</c> file.</param>
	/// <param name="title">Optional display text.</param>
	public LikeC4DetailsOptions WithLink(Uri uri, string? title = null) => WithLink(uri?.ToString()!, title);

	/// <summary>Adds a metadata key-value pair to this element.</summary>
	public LikeC4DetailsOptions WithMetadata(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		_metadata.Add(new(key, value));
		return this;
	}

	/// <summary>Adds a metadata key-value pair to this element.</summary>
	public LikeC4DetailsOptions WithMetadata(params (string key, string value)[] metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		foreach (var (key, value) in metadata)
			WithMetadata(key, value);

		return this;
	}

	/// <summary>Adds a metadata key-value pair to this element.</summary>
	public LikeC4DetailsOptions WithMetadata(IDictionary<string, string> metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		foreach (var (key, value) in metadata)
			WithMetadata(key, value);

		return this;
	}
}
