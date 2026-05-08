using System.Text;

namespace Aspire.Hosting.LikeC4;

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

    private static void WriteSpecification(StringBuilder sb, LikeC4Model model)
    {
        var kinds = model.Elements
            .Select(e => e.Kind)
            .Distinct()
            .OrderBy(k => k);

        sb.AppendLine("specification {");
        foreach (var kind in kinds)
        {
            sb.Append("  element ").AppendLine(kind);
        }

        sb.AppendLine("}");
    }

    private static void WriteModel(StringBuilder sb, LikeC4Model model)
    {
        sb.AppendLine("model {");

        // Top-level elements first, then nested.
        var topLevel = model.Elements.Where(e => e.ParentName is null).ToList();
        var nested = model.Elements
            .Where(e => e.ParentName is not null)
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

    private static void WriteElement(
        StringBuilder sb,
        LikeC4Element element,
        string indent,
        Dictionary<string, List<LikeC4Element>> nested)
    {
        sb.Append(indent).Append(Sanitize(element.Name)).Append(" = ").Append(element.Kind)
          .Append(" '").Append(EscapeQuote(element.Label)).Append('\'');

        var children = nested.GetValueOrDefault(element.Name);
        var hasTechnology = !string.IsNullOrWhiteSpace(element.Technology);
        var hasDescription = !string.IsNullOrWhiteSpace(element.Description);
        var hasChildren = children?.Count > 0;

        if (!hasTechnology && !hasDescription && !hasChildren)
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

        if (hasChildren)
        {
            foreach (var child in children!)
            {
                WriteElement(sb, child, indent + "  ", nested);
            }
        }

        sb.Append(indent).AppendLine("}");
    }

    private static void WriteViews(StringBuilder sb, LikeC4Model model, LikeC4DiagramOptions options)
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
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    /// <summary>Replaces characters invalid in LikeC4 identifiers with underscores.</summary>
    private static string Sanitize(string name) =>
        string.Create(name.Length, name, static (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                span[i] = char.IsLetterOrDigit(src[i]) || src[i] == '_' ? src[i] : '_';
            }
        });

    /// <summary>Escapes single quotes in string values used inside LikeC4 quoted strings.</summary>
    private static string EscapeQuote(string value) => value.Replace("'", "\\'");
}
