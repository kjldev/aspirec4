namespace Aspire.Hosting.AspireC4.LikeC4.Models;

/// <summary>
/// Defines a custom element kind specification that is declared in the LikeC4 <c>specification</c> block
/// with optional style, notation, and technology defaults.
/// </summary>
public sealed class LikeC4ElementKindSpec
{
	/// <param name="name">The kind identifier, e.g. <c>"database"</c> or <c>"queue"</c>.</param>
	public LikeC4ElementKindSpec(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		Name = name;
	}

	/// <summary>The kind identifier used in the DSL.</summary>
	public string Name { get; }

	/// <summary>Optional notation string shown in the diagram for all elements of this kind.</summary>
	public string? Notation { get; set; }

	/// <summary>Optional default technology label for all elements of this kind.</summary>
	public string? Technology { get; set; }

	/// <summary>Optional style overrides applied to all elements of this kind in the specification block.</summary>
	public LikeC4ElementKindStyle? Style { get; set; }

	/// <summary>
	/// Fluent helper to configure the style.
	/// Returns <see langword="this"/> for chaining.
	/// </summary>
	public LikeC4ElementKindSpec WithNotation(string notation)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(notation);
		Notation = notation;
		return this;
	}

	/// <inheritdoc cref="Technology"/>
	public LikeC4ElementKindSpec WithTechnology(string technology)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(technology);
		Technology = technology;
		return this;
	}

	/// <inheritdoc cref="Style"/>
	public LikeC4ElementKindSpec WithStyle(LikeC4ElementKindStyle style)
	{
		ArgumentNullException.ThrowIfNull(style);
		Style = style;
		return this;
	}
}
