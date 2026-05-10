using System.Text;

namespace Aspire.Hosting.AspireC4;

/// <summary>
/// Generates a LikeC4 DSL (<c>.c4</c>) string from a <see cref="LikeC4Model"/>.
/// </summary>
public static class LikeC4DSLGenerator
{
	/// <summary>Generates a LikeC4 DSL string from the given model and options.</summary>
	public static string Generate(LikeC4Model model, AspireC4DiagramOptions options)
	{
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(options);

		StringBuilder sb = new(512);

		WriteSpecification(sb, model, options);
		sb.AppendLine();
		WriteModel(sb, model, options);
		sb.AppendLine();
		WriteViews(sb, model, options);

		return sb.ToString();
	}

	static void WriteSpecification(StringBuilder sb, LikeC4Model model, AspireC4DiagramOptions options)
	{
		var elementKindsInModel = model.Elements.Select(e => e.Kind).ToHashSet();
		var allElementKinds = elementKindsInModel.Union(options.ElementKindSpecs.Select(s => s.Name)).OrderBy(k => k);

		var relationshipKinds = model
			.Relationships.Where(r => !string.IsNullOrWhiteSpace(r.Kind))
			.Select(r => r.Kind!)
			.Distinct()
			.OrderBy(k => k);

		var allTags = model
			.Elements.SelectMany(e => e.Tags)
			.Concat(model.Relationships.SelectMany(r => r.Tags))
			.Distinct()
			.OrderBy(t => t);

		var specsByName = options.ElementKindSpecs.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

		sb.AppendLine("specification {");

		foreach (var kind in allElementKinds)
		{
			if (specsByName.TryGetValue(kind, out var spec))
			{
				WriteElementKindSpec(sb, spec);
			}
			else
			{
				sb.Append("  element ").AppendLine(kind);
			}
		}

		foreach (var kind in relationshipKinds)
		{
			sb.Append("  relationship ").AppendLine(kind);
		}

		foreach (var tag in allTags)
		{
			sb.Append("  tag ").AppendLine(tag);
		}

		sb.AppendLine("}");
	}

	static void WriteElementKindSpec(StringBuilder sb, LikeC4ElementKindSpec spec)
	{
		var hasNotation = !string.IsNullOrWhiteSpace(spec.Notation);
		var hasTechnology = !string.IsNullOrWhiteSpace(spec.Technology);
		var style = spec.Style;
		var hasStyle =
			style is not null
			&& (
				!string.IsNullOrWhiteSpace(style.Shape)
				|| !string.IsNullOrWhiteSpace(style.Color)
				|| !string.IsNullOrWhiteSpace(style.Icon)
				|| !string.IsNullOrWhiteSpace(style.Border)
				|| style.Opacity.HasValue
			);

		if (!hasNotation && !hasTechnology && !hasStyle)
		{
			sb.Append("  element ").AppendLine(spec.Name);
			return;
		}

		sb.Append("  element ").AppendLine(spec.Name + " {");

		if (hasNotation)
		{
			sb.Append("    notation '").Append(EscapeQuote(spec.Notation!)).AppendLine("'");
		}

		if (hasTechnology)
		{
			sb.Append("    technology '").Append(EscapeQuote(spec.Technology!)).AppendLine("'");
		}

		if (hasStyle)
		{
			sb.AppendLine("    style {");
			if (!string.IsNullOrWhiteSpace(style!.Shape))
			{
				sb.Append("      shape ").AppendLine(style.Shape);
			}

			if (!string.IsNullOrWhiteSpace(style.Color))
			{
				sb.Append("      color ").AppendLine(style.Color);
			}

			if (!string.IsNullOrWhiteSpace(style.Icon))
			{
				sb.Append("      icon ").AppendLine(style.Icon);
			}

			if (!string.IsNullOrWhiteSpace(style.Border))
			{
				sb.Append("      border ").AppendLine(style.Border);
			}

			if (style.Opacity.HasValue)
			{
				sb.Append("      opacity ").Append(style.Opacity.Value).AppendLine("%");
			}

			sb.AppendLine("    }");
		}

		sb.AppendLine("  }");
	}

	static void WriteModel(StringBuilder sb, LikeC4Model model, AspireC4DiagramOptions options)
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
			var hasKind = !string.IsNullOrWhiteSpace(rel.Kind);

			sb.Append("  ").Append(Sanitize(rel.SourceName));

			if (hasKind)
			{
				if (options.RelationshipKindSyntax == LikeC4RelationshipKindSyntax.Bracket)
				{
					sb.Append(" -[").Append(rel.Kind).Append("]-> ");
				}
				else
				{
					sb.Append(" .").Append(rel.Kind).Append(' ');
				}
			}
			else
			{
				sb.Append(" -> ");
			}

			sb.Append(Sanitize(rel.TargetName));

			if (!string.IsNullOrWhiteSpace(rel.Label))
			{
				sb.Append(" '").Append(EscapeQuote(rel.Label)).Append('\'');
			}

			var hasTags = rel.Tags.Count > 0;
			var hasTechnology = !string.IsNullOrWhiteSpace(rel.Technology);
			var hasDescription = !string.IsNullOrWhiteSpace(rel.Description);
			var hasLinks = rel.Links.Count > 0;
			var hasMetadata = rel.Metadata.Count > 0;

			if (hasTags || hasTechnology || hasDescription || hasLinks || hasMetadata)
			{
				sb.AppendLine(" {");

				if (hasTags)
				{
					sb.Append("    ");
					foreach (var tag in rel.Tags)
					{
						sb.Append('#').Append(tag).Append(' ');
					}

					sb.AppendLine();
				}

				if (hasTechnology)
				{
					sb.Append("    technology '").Append(EscapeQuote(rel.Technology!)).AppendLine("'");
				}

				if (hasDescription)
				{
					sb.Append("    description '''").AppendLine();
					sb.Append(EscapeQuote(rel.Description!)).AppendLine();
					sb.AppendLine("    '''");
				}

				foreach (var link in rel.Links)
				{
					sb.Append("    link ").Append(link.Uri);
					if (!string.IsNullOrWhiteSpace(link.Title))
					{
						sb.Append(" '").Append(EscapeQuote(link.Title)).Append('\'');
					}

					sb.AppendLine();
				}

				if (hasMetadata)
				{
					sb.AppendLine("    metadata {");
					foreach (var (key, value) in rel.Metadata)
					{
						sb.Append("      ").Append(key).Append(" '").Append(EscapeQuote(value)).AppendLine("'");
					}

					sb.AppendLine("    }");
				}

				sb.AppendLine("  }");
			}
			else
			{
				sb.AppendLine();
			}
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
		var hasTags = element.Tags.Count > 0;
		var hasTechnology = !string.IsNullOrWhiteSpace(element.Technology);
		var hasDescription = !string.IsNullOrWhiteSpace(element.Description);
		var hasSummary = !string.IsNullOrWhiteSpace(element.Summary);
		var hasIcon = !string.IsNullOrWhiteSpace(element.Icon);
		var hasLinks = element.Links.Count > 0;
		var hasMetadata = element.Metadata.Count > 0;
		var hasChildren = children?.Count > 0;

		if (
			!hasTags
			&& !hasTechnology
			&& !hasDescription
			&& !hasSummary
			&& !hasIcon
			&& !hasLinks
			&& !hasMetadata
			&& !hasChildren
		)
		{
			sb.AppendLine();
			return;
		}

		sb.AppendLine(" {");

		if (hasTags)
		{
			sb.Append(indent).Append("  ");
			foreach (var tag in element.Tags)
			{
				sb.Append('#').Append(tag).Append(' ');
			}

			sb.AppendLine();
		}

		if (hasTechnology)
		{
			sb.Append(indent).Append("  technology '").Append(EscapeQuote(element.Technology!)).AppendLine("'");
		}

		if (hasSummary)
		{
			sb.Append(indent).Append("  summary '").Append(EscapeQuote(element.Summary!)).AppendLine("'");
		}

		if (hasDescription)
		{
			sb.Append(indent)
				.Append("  description '''")
				.AppendLine()
				.Append(EscapeQuote(element.Description!))
				.AppendLine()
				.AppendLine("  '''");
		}

		if (hasIcon)
		{
			sb.Append(indent).Append("  icon ").AppendLine(element.Icon!);
		}

		foreach (var link in element.Links)
		{
			sb.Append(indent).Append("  link ").Append(link.Uri);
			if (!string.IsNullOrWhiteSpace(link.Title))
			{
				sb.Append(" '").Append(EscapeQuote(link.Title)).Append('\'');
			}

			sb.AppendLine();
		}

		if (hasMetadata)
		{
			sb.Append(indent).AppendLine("  metadata {");
			foreach (var (key, value) in element.Metadata)
			{
				sb.Append(indent).Append("    ").Append(key).Append(" '").Append(EscapeQuote(value)).AppendLine("'");
			}

			sb.Append(indent).AppendLine("  }");
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

	static void WriteViews(StringBuilder sb, LikeC4Model model, AspireC4DiagramOptions options)
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

			// Emit view-level group blocks.
			var groups = model.Elements.Where(e => e.Group is not null).GroupBy(e => e.Group!).OrderBy(g => g.Key);

			foreach (var group in groups)
			{
				var members = string.Join(", ", group.Select(e => Sanitize(e.Name)));
				sb.Append("    group '").Append(EscapeQuote(group.Key)).AppendLine("' {");
				sb.Append("      include ").AppendLine(members);
				sb.AppendLine("    }");
			}

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
