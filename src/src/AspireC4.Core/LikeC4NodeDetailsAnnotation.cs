using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Annotation that customises how a resource appears as a node in the generated LikeC4 diagram.
/// </summary>
public sealed class LikeC4NodeDetailsAnnotation : IResourceAnnotation
{
	public LikeC4NodeDetailsAnnotation(string label, string? technology = null, string? description = null)
		: this(
			label,
			technology,
			description,
			summary: null,
			icon: null,
			autoIconEnabled: null,
			kind: null,
			tags: [],
			links: [],
			metadata: []
		) { }

	public LikeC4NodeDetailsAnnotation(string label, string? technology, string? description, string? icon)
		: this(
			label,
			technology,
			description,
			summary: null,
			icon,
			autoIconEnabled: null,
			kind: null,
			tags: [],
			links: [],
			metadata: []
		) { }

	public LikeC4NodeDetailsAnnotation(
		string label,
		string? technology,
		string? description,
		string? icon,
		bool? autoIconEnabled
	)
		: this(
			label,
			technology,
			description,
			summary: null,
			icon,
			autoIconEnabled,
			kind: null,
			tags: [],
			links: [],
			metadata: []
		) { }

	public LikeC4NodeDetailsAnnotation(
		string label,
		string? technology,
		string? description,
		string? summary,
		string? icon,
		bool? autoIconEnabled
	)
		: this(
			label,
			technology,
			description,
			summary,
			icon,
			autoIconEnabled,
			kind: null,
			tags: [],
			links: [],
			metadata: []
		) { }

	public LikeC4NodeDetailsAnnotation(
		string label,
		string? technology,
		string? description,
		string? summary,
		string? icon,
		bool? autoIconEnabled,
		string? kind,
		IReadOnlyList<string> tags,
		IReadOnlyList<LikeC4Link> links,
		IReadOnlyList<LikeC4Metadata> metadata
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(label);

		if (icon is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(icon);
		}

		Label = label;
		Technology = technology;
		Description = description;
		Summary = summary;
		Icon = icon;
		AutoIconEnabled = autoIconEnabled;
		Kind = kind;
		Tags = tags ?? [];
		Links = links ?? [];
		Metadata = metadata ?? [];
	}

	public string Label { get; }
	public string? Technology { get; }
	public string? Description { get; }
	public string? Summary { get; }
	public string? Icon { get; }
	public bool? AutoIconEnabled { get; }

	/// <summary>Optional element kind override. When set, overrides the inferred element kind in the diagram.</summary>
	public string? Kind { get; }

	/// <summary>Tags applied to this element in the diagram.</summary>
	public IReadOnlyList<string> Tags { get; }

	/// <summary>Links attached to this element in the diagram.</summary>
	public IReadOnlyList<LikeC4Link> Links { get; }

	/// <summary>Metadata key-value pairs for this element.</summary>
	public IReadOnlyList<LikeC4Metadata> Metadata { get; }
}
