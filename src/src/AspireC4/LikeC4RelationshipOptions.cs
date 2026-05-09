namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Fluent options for customising how a relationship appears in the generated LikeC4 diagram.
/// </summary>
/// <seealso cref="LikeC4VisualizationExtensions.WithLikeC4Reference{T,TRef}"/>
public sealed class LikeC4RelationshipOptions
{
	/// <summary>Short label shown on the relationship arrow.</summary>
	public string? Label { get; private set; }

	/// <summary>Technology or protocol used by the relationship (e.g., "HTTP/2", "gRPC", "AMQP").</summary>
	public string? Technology { get; private set; }

	/// <summary>Longer description of the relationship.</summary>
	public string? Description { get; private set; }

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
}
