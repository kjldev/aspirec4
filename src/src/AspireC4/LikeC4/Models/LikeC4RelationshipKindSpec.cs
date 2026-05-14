namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>
/// Defines a custom relationship kind specification that is declared in the LikeC4 <c>specification</c> block
/// with an optional technology label.
/// </summary>
public sealed class LikeC4RelationshipKindSpec
{
	/// <param name="name">The kind identifier, e.g. <c>"async"</c> or <c>"grpc"</c>.</param>
	/// <param name="technology">Optional default technology label for all relationships of this kind (e.g. <c>"AMQP"</c>, <c>"gRPC"</c>).</param>
	public LikeC4RelationshipKindSpec(string name, string? technology = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		Name = name;
		Technology = technology;
	}

	/// <summary>The kind identifier used in the DSL.</summary>
	public string Name { get; }

	/// <summary>Optional default technology label for all relationships of this kind (e.g. <c>"AMQP"</c>, <c>"gRPC"</c>).</summary>
	public string? Technology { get; set; }

	/// <inheritdoc cref="Technology"/>
	public LikeC4RelationshipKindSpec WithTechnology(string technology)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(technology);
		Technology = technology;
		return this;
	}
}
