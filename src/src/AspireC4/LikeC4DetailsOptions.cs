namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Fluent options for configuring how a resource appears in the generated LikeC4 diagram.
/// </summary>
public sealed class LikeC4DetailsOptions
{
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
}
