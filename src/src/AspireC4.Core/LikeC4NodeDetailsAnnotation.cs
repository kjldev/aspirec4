using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Annotation that customises how a resource appears as a node in the generated LikeC4 diagram.
/// </summary>
public sealed class LikeC4NodeDetailsAnnotation : IResourceAnnotation
{
	public LikeC4NodeDetailsAnnotation(string label, string? technology = null, string? description = null)
		: this(label, technology, description, icon: null, autoIconEnabled: null) { }

	public LikeC4NodeDetailsAnnotation(string label, string? technology, string? description, string? icon)
		: this(label, technology, description, icon, autoIconEnabled: null) { }

	public LikeC4NodeDetailsAnnotation(
		string label,
		string? technology,
		string? description,
		string? icon,
		bool? autoIconEnabled
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
		Icon = icon;
		AutoIconEnabled = autoIconEnabled;
	}

	public string Label { get; }
	public string? Technology { get; }
	public string? Description { get; }
	public string? Icon { get; }
	public bool? AutoIconEnabled { get; }
}
