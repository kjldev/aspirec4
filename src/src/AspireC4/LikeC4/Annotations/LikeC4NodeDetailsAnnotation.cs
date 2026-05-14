using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4.LikeC4.Annotations;

/// <summary>
/// Annotation that customises how a resource appears as a node in the generated LikeC4 diagram.
/// Configure using <c>WithLikeC4Details(a =&gt; a.WithIcon(...).WithTag(...))</c>.
/// </summary>
public sealed class LikeC4NodeDetailsAnnotation : IResourceAnnotation
{
	readonly List<string> _tags = [];
	readonly List<LikeC4Link> _links = [];
	readonly List<LikeC4Metadata> _metadata = [];

	public LikeC4NodeDetailsAnnotation(string label)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);
		Label = label;
	}

	public string Label { get; private set; }

	public string? Technology { get; private set; }

	public string? Description { get; private set; }

	public string? Summary { get; private set; }

	public string? Icon { get; private set; }

	public bool? AutoIconEnabled { get; private set; }

	/// <summary>Optional element kind override. When set, overrides the inferred element kind in the diagram.</summary>
	public string? Kind { get; private set; }

	/// <summary>Tags applied to this element in the diagram.</summary>
	public IReadOnlyList<string> Tags => _tags;

	/// <summary>Links attached to this element in the diagram.</summary>
	public IReadOnlyList<LikeC4Link> Links => _links;

	/// <summary>Metadata key-value pairs for this element.</summary>
	public IReadOnlyList<LikeC4Metadata> Metadata => _metadata;

	public LikeC4NodeDetailsAnnotation WithLabel(string label)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);
		Label = label;
		return this;
	}

	public LikeC4NodeDetailsAnnotation WithTechnology(string? technology)
	{
		Technology = technology;
		return this;
	}

	public LikeC4NodeDetailsAnnotation WithDescription(string? description)
	{
		Description = description;
		return this;
	}

	public LikeC4NodeDetailsAnnotation WithSummary(string? summary)
	{
		Summary = summary;
		return this;
	}

	public LikeC4NodeDetailsAnnotation WithIcon(string? icon)
	{
		if (icon is not null)
			ArgumentException.ThrowIfNullOrWhiteSpace(icon);
		Icon = icon;
		return this;
	}

	/// <summary>
	/// Controls automatic icon inference for this element.
	/// <see langword="null"/> inherits the project-level <see cref="AspireC4DiagramOptions.AutoIconsEnabled"/> setting.
	/// </summary>
	public LikeC4NodeDetailsAnnotation WithAutoIcon(bool? enabled = null)
	{
		AutoIconEnabled = enabled;
		return this;
	}

	/// <summary>
	/// Sets an element kind override, replacing the auto-inferred kind for this resource.
	/// Must be a valid LikeC4 identifier (letters, digits, hyphens, underscores; cannot start with a digit).
	/// </summary>
	public LikeC4NodeDetailsAnnotation WithKind(string? kind)
	{
		Kind = kind;
		return this;
	}

	/// <summary>
	/// Adds a tag. A leading <c>#</c> is accepted and stripped automatically, so <c>"#external"</c>
	/// and <c>"external"</c> refer to the same tag.
	/// </summary>
	public LikeC4NodeDetailsAnnotation WithTag(string tag)
	{
		_tags.Add(Helpers.NormaliseTag(tag));
		return this;
	}

	/// <summary>Adds a hyperlink to this element.</summary>
	/// <param name="url">The URL, which may be absolute or relative to the <c>.c4</c> file.</param>
	/// <param name="title">Optional display text.</param>
	public LikeC4NodeDetailsAnnotation WithLink(string url, string? title = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(url);
		_links.Add(new LikeC4Link(url, title));
		return this;
	}

	/// <inheritdoc cref="WithLink(string, string?)"/>
	public LikeC4NodeDetailsAnnotation WithLink(Uri uri, string? title = null) => WithLink(uri?.ToString()!, title);

	/// <summary>Adds a metadata key-value pair to this element.</summary>
	public LikeC4NodeDetailsAnnotation WithMetadata(string key, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		_metadata.Add(new(key, value));

		return this;
	}

	/// <summary>Adds multiple metadata key-value pairs to this element.</summary>
	public LikeC4NodeDetailsAnnotation WithMetadata(params (string key, string value)[] metadata)
	{
		ArgumentNullException.ThrowIfNull(metadata);

		foreach (var (key, value) in metadata)
			WithMetadata(key, value);

		return this;
	}
}
