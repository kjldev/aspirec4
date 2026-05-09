using System.Text;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Generates a LikeC4 DSL (<c>.c4</c>) string from a <see cref="LikeC4Model"/>.
/// </summary>
public static class LikeC4DslGenerator
{
	/// <summary>Generates a LikeC4 DSL string from the given model and options.</summary>
	public static string Generate(LikeC4Model model, LikeC4DiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		var sb = new StringBuilder(512);

		WriteSpecification(sb, model);
		sb.AppendLine();
		WriteModel(sb, model);
		sb.AppendLine();
		WriteViews(sb, model, options);

		return sb.ToString();
	}

	static void WriteSpecification(StringBuilder sb, LikeC4Model model)
	{
		var kinds = model.Elements.Select(e => e.Kind).Distinct().OrderBy(k => k);

		sb.AppendLine("specification {");
		foreach (var kind in kinds)
		{
			sb.Append("  element ").AppendLine(kind);
		}

		sb.AppendLine("}");
	}

	static void WriteModel(StringBuilder sb, LikeC4Model model)
	{
		sb.AppendLine("model {");

		// Top-level elements first, then nested.
		var topLevel = model.Elements.Where(e => e.ParentName is null).ToList();
		var nested = model
			.Elements.Where(e => e.ParentName is not null)
			.GroupBy(e => e.ParentName!)
			.ToDictionary(g => g.Key, g => g.ToList());

		foreach (var element in topLevel)
		{
			WriteElement(sb, element, "  ", nested);
		}

		sb.AppendLine();

		foreach (var rel in model.Relationships)
		{
			sb.Append("  ").Append(Sanitize(rel.SourceName)).Append(" -> ").Append(Sanitize(rel.TargetName));

			if (!string.IsNullOrWhiteSpace(rel.Label))
			{
				sb.Append(" '").Append(EscapeQuote(rel.Label)).Append('\'');
			}

			sb.AppendLine();
		}

		sb.AppendLine("}");
	}

	static void WriteElement(
		StringBuilder sb,
		LikeC4Element element,
		string indent,
		Dictionary<string, List<LikeC4Element>> nested
	)
	{
		sb.Append(indent)
			.Append(Sanitize(element.Name))
			.Append(" = ")
			.Append(element.Kind)
			.Append(" '")
			.Append(EscapeQuote(element.Label))
			.Append('\'');

		var children = nested.GetValueOrDefault(element.Name);
		var hasTechnology = !string.IsNullOrWhiteSpace(element.Technology);
		var hasDescription = !string.IsNullOrWhiteSpace(element.Description);
		var hasIcon = !string.IsNullOrWhiteSpace(element.Icon);
		var hasChildren = children?.Count > 0;

		if (!hasTechnology && !hasDescription && !hasIcon && !hasChildren)
		{
			sb.AppendLine();
			return;
		}

		sb.AppendLine(" {");

		if (hasTechnology)
		{
			sb.Append(indent).Append("  technology '").Append(EscapeQuote(element.Technology!)).AppendLine("'");
		}

		if (hasDescription)
		{
			sb.Append(indent).Append("  description '").Append(EscapeQuote(element.Description!)).AppendLine("'");
		}

		if (hasIcon)
		{
			sb.Append(indent).Append("  icon ").AppendLine(element.Icon!);
		}

		if (hasChildren)
		{
			foreach (var child in children!)
			{
				WriteElement(sb, child, indent + "  ", nested);
			}
		}

		sb.Append(indent).AppendLine("}");
	}

	/// <summary>
	/// Style override (color and/or opacity) for a view-level style rule.
	/// A <see langword="null"/> value means "use the default".
	/// </summary>
	readonly record struct LikeC4ElementStyleOverride(string? Color, int? Opacity);

	/// <summary>
	/// Maps a <see cref="LikeC4ResourceState"/> to a view-level style override.
	/// <para>
	/// Opacity is used to distinguish transitional from terminal states:
	/// <list type="bullet">
	///   <item><description><see cref="LikeC4ResourceState.Stopping"/> — 60 % opacity: visible but clearly winding down.</description></item>
	///   <item><description><see cref="LikeC4ResourceState.Exited"/> — 30 % opacity: very faded, fully inactive.</description></item>
	/// </list>
	/// </para>
	/// </summary>
	static LikeC4ElementStyleOverride GetStateStyle(LikeC4ResourceState state) =>
		state switch
		{
			LikeC4ResourceState.Starting => new("sky", null),
			LikeC4ResourceState.Running => new("green", null),
			LikeC4ResourceState.Stopping => new("slate", 60),
			LikeC4ResourceState.Exited => new("muted", 30),
			LikeC4ResourceState.Failed => new("amber", null),
			LikeC4ResourceState.Error => new("red", null),
			_ => new(null, null),
		};

	static void WriteViews(StringBuilder sb, LikeC4Model model, LikeC4DiagramOptions options)
	{
		sb.AppendLine("views {");
		sb.AppendLine("  view index {");
		sb.Append("    title '").Append(EscapeQuote(options.Title)).AppendLine("'");

		if (model.Elements.Count == 0)
		{
			sb.AppendLine("    // No resources found");
		}
		else
		{
			sb.AppendLine("    include *");

			// Emit style overrides grouped by (color, opacity) for elements with a non-default state.
			// In LikeC4, element colors must be set via view-level style rules, not in the
			// model block.
			var byStyle = model
				.Elements.Select(e => (Element: e, Style: GetStateStyle(e.State)))
				.Where(t => t.Style.Color is not null || t.Style.Opacity is not null)
				.GroupBy(t => t.Style, t => t.Element);

			foreach (var group in byStyle)
			{
				var names = string.Join(", ", group.Select(e => Sanitize(e.Name)));
				sb.Append("    style ").Append(names).AppendLine(" {");
				if (group.Key.Color is not null)
				{
					sb.Append("      color ").AppendLine(group.Key.Color);
				}

				if (group.Key.Opacity is not null)
				{
					sb.Append("      opacity ").Append(group.Key.Opacity).AppendLine("%");
				}

				sb.AppendLine("    }");
			}
		}

		sb.AppendLine("  }");
		sb.AppendLine("}");
	}

	/// <summary>Replaces characters invalid in LikeC4 identifiers with underscores.</summary>
	static string Sanitize(string name) =>
		string.Create(
			name.Length,
			name,
			static (span, src) =>
			{
				for (var i = 0; i < src.Length; i++)
				{
					span[i] = char.IsLetterOrDigit(src[i]) || src[i] == '_' ? src[i] : '_';
				}
			}
		);

	/// <summary>Escapes single quotes in string values used inside LikeC4 quoted strings.</summary>
	static string EscapeQuote(string value) => value.Replace("'", "\\'", StringComparison.Ordinal);
}
